using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace DurableFunctionVideoProcessor
{
    public static class ProcessVideoOrchestrators
    {
        [FunctionName(OrchestratorNames.ProcessVideo)]
        public static async Task<object> ProcessVideo(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
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
                    log.Error("Failed to process video with error " + e.Message);
                await ctx.CallActivityAsync(ActivityNames.Cleanup, videoLocation);
                return new {Error = "Failed to process video", e.Message};
            }
        }

        [FunctionName(OrchestratorNames.Transcode)]
        public static async Task<string[]> Transcode(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
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
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
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
                    if (!ctx.IsReplaying) log.Warning($"Received an approval result of {approvalResult}");
                    cts.Cancel(); // we should cancel the timeout task
                }
                else
                {
                    if (!ctx.IsReplaying) log.Warning($"Timed out waiting {emailTimeoutSeconds}s for an approval result");
                    approvalResult = "TimedOut";
                }
            }
            return approvalResult;
        }

        [FunctionName(OrchestratorNames.PeriodicTask)]
        public static async Task<int> PeriodicTask(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var timesRun = ctx.GetInput<int>();
            timesRun++;
            if (!ctx.IsReplaying)
                log.Info($"Starting the PeriodicTask orchestrator {ctx.InstanceId}, {timesRun}");
            await ctx.CallActivityAsync(ActivityNames.PeriodicActivity, timesRun);
            var nextRun = ctx.CurrentUtcDateTime.AddSeconds(30);
            await ctx.CreateTimer(nextRun, CancellationToken.None);
            ctx.ContinueAsNew(timesRun);
            return timesRun;
        }

        /** EXAMPLES

        /// <summary>
        /// In this example we call an activity that triggers some external action. 
        /// When that external action completes an event is raised which our orchestration is waiting for
        /// Then we decide whether to continue or not
        /// </summary>
        [FunctionName("ContinueAsNewExample2")]
        public static async Task<string> ContinueAsNewExample2(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var lastRunResult = ctx.GetInput<string>();
            if (!ctx.IsReplaying)
                log.Info("Starting external action");
            await ctx.CallActivityAsync("StartExternalAction", lastRunResult);
            var externalActionResult = await ctx.WaitForExternalEvent<string>("ExternalActionCompleted");
            if (externalActionResult != "end")
                ctx.ContinueAsNew(externalActionResult);
            return externalActionResult;
        }

        /// <summary>
        /// This is the stateful singleton pattern that is currently NOT recoemmended
        /// due to this race condition on GitHub which causes some events to get lost
        /// https://github.com/Azure/azure-functions-durable-extension/issues/67
        /// </summary>
        [FunctionName("ContinueAsNewExample3")]
        public static async Task<int> ContinueAsNewExample3(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var counterState = ctx.GetInput<int>();
            if (!ctx.IsReplaying)
                log.Info($"Current counter state is {counterState}. Waiting for next operation.");
            var operation = await ctx.WaitForExternalEvent<string>("operation");
            if (!ctx.IsReplaying)
                log.Info($"Received '{operation}' operation.");
            operation = operation?.ToLowerInvariant();
            if (operation == "incr")
            {
                counterState++;
            }
            else if (operation == "decr")
            {
                counterState--;
            }
            if (operation != "end")
            {
                ctx.ContinueAsNew(counterState);
            }
            return counterState;
        }

        **/
    }
}
