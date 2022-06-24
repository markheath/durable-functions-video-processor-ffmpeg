using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;

namespace DurableFunctionVideoProcessor;

public static class ProcessVideoOrchestrators
{
    [FunctionName(OrchestratorNames.ProcessVideo)]
    public static async Task<object> ProcessVideo(
        [OrchestrationTrigger] IDurableOrchestrationContext ctx,
        ILogger log)
    {
        var videoLocation = ctx.GetInput<string>();
        try
        {
            var transcodedLocations = await
                ctx.CallSubOrchestratorAsync<string[]>(OrchestratorNames.Transcode,
                    videoLocation);
            var transcodedLocation = transcodedLocations.First(x => x.Contains(".mp4")); // these are SAS tokens

            var thumbnailLocation = await ctx.CallActivityWithRetryAsync<string>(ActivityNames.ExtractThumbnail,
                new RetryOptions(TimeSpan.FromSeconds(5), 4), // {Handle = ex => ex.InnerException is InvalidOperationException}, - currently not possible #84
                transcodedLocation);
            var withIntroLocation = await ctx.CallActivityAsync<string>
                (ActivityNames.PrependIntro, transcodedLocation);
            // we need to give our suborchestrator its own id so we can send it events
            // could be a new guid, but by basing it on the parent instance id we make it predictable
            var approvalInfo =
                new ApprovalInfo {OrchestrationId = "XYZ" + ctx.InstanceId, VideoLocation = withIntroLocation};
            var approvalResult = await ctx.CallSubOrchestratorAsync<string>(OrchestratorNames.GetApprovalResult, approvalInfo.OrchestrationId, approvalInfo);

            if (approvalResult == "Approved")
            {
                await ctx.CallActivityAsync(ActivityNames.PublishVideo,
                    new [] { transcodedLocation, thumbnailLocation, withIntroLocation });
                return "Approved and published";
            }
            await ctx.CallActivityAsync(ActivityNames.RejectVideo,
                new [] { transcodedLocation, thumbnailLocation, withIntroLocation });
            return $"Not published because {approvalResult}";

        }
        catch (Exception e)
        {
            if (!ctx.IsReplaying)
                log.LogError("Failed to process video with error " + e.Message);
            await ctx.CallActivityAsync(ActivityNames.Cleanup, videoLocation);
            return new {Error = "Failed to process video", e.Message};
        }
    }

    [FunctionName(OrchestratorNames.Transcode)]
    public static async Task<string[]> Transcode(
        [OrchestrationTrigger] IDurableOrchestrationContext ctx,
        ILogger log)
    {
        var videoLocation = ctx.GetInput<string>();
        var transcodeProfiles = await
            ctx.CallActivityAsync<TranscodeParams[]>(ActivityNames.GetTranscodeProfiles, null);
        var transcodeTasks = new List<Task<string>>();
        foreach (var transcodeProfile in transcodeProfiles)
        {
            transcodeProfile.InputFile = videoLocation;
            var transcodeTask = ctx.CallActivityAsync<string>
                (ActivityNames.TranscodeVideo, transcodeProfile);
            transcodeTasks.Add(transcodeTask);
        }
        var locations = await Task.WhenAll(transcodeTasks);
        return locations;
    }

    [FunctionName(OrchestratorNames.GetApprovalResult)]
    public static async Task<string> GetApprovalResult(
        [OrchestrationTrigger] IDurableOrchestrationContext ctx,
        ILogger log)
    {
        var approvalInfo = ctx.GetInput<ApprovalInfo>();
        var emailTimeoutSeconds = await ctx.CallActivityAsync<int>(ActivityNames.SendApprovalRequestEmail, approvalInfo);

        string approvalResult;
        using (var cts = new CancellationTokenSource())
        {
            var timeoutAt = ctx.CurrentUtcDateTime.AddSeconds(emailTimeoutSeconds);
            var timeoutTask = ctx.CreateTimer(timeoutAt, cts.Token);
            var approvalTask = ctx.WaitForExternalEvent<string>(EventNames.ApprovalResult);

            var winner = await Task.WhenAny(approvalTask, timeoutTask);
            if (winner == approvalTask)
            {
                approvalResult = approvalTask.Result;
                if (!ctx.IsReplaying) log.LogWarning($"Received an approval result of {approvalResult}");
                cts.Cancel(); // we should cancel the timeout task
            }
            else
            {
                if (!ctx.IsReplaying) log.LogWarning($"Timed out waiting {emailTimeoutSeconds}s for an approval result");
                approvalResult = "TimedOut";
            }
        }
        return approvalResult;
    }

    [FunctionName(OrchestratorNames.PeriodicTask)]
    public static async Task<int> PeriodicTask(
        [OrchestrationTrigger] IDurableOrchestrationContext ctx,
        ILogger log)
    {
        var timesRun = ctx.GetInput<int>();
        timesRun++;
        if (!ctx.IsReplaying)
            log.LogInformation($"Starting the PeriodicTask orchestrator {ctx.InstanceId}, {timesRun}");
        await ctx.CallActivityAsync(ActivityNames.PeriodicActivity, timesRun);
        var nextRun = ctx.CurrentUtcDateTime.AddSeconds(30);
        await ctx.CreateTimer(nextRun, CancellationToken.None);
        ctx.ContinueAsNew(timesRun);
        return timesRun;
    }
}
