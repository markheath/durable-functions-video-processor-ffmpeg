using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace DurableFunctionVideoProcessor
{
    public static class ProcessVideoOrchestrator
    {
        [FunctionName("ProcessVideoOrchestrator")]
        public static async Task<object> Run(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var videoLocation = ctx.GetInput<string>();
            try
            {
                var transcodedLocation = await
                    ctx.CallSubOrchestratorAsync<string>("TranscodeOrchestrator",
                        videoLocation);
                var thumbnailLocation = await ctx.CallActivityWithRetryAsync<string>("ExtractThumbnail",
                    new RetryOptions(TimeSpan.FromSeconds(5), 4), transcodedLocation);
                var withIntroLocation = await ctx.CallActivityAsync<string>
                    ("PrependIntro", transcodedLocation);
                var approvalInfo =
                    new ApprovalInfo {OrchestrationId = ctx.InstanceId, VideoLocation = withIntroLocation};
                var approvalResult = await ctx.CallSubOrchestratorAsync<string>("GetApprovalResultOrchestrator", approvalInfo);

                if (approvalResult == "Approved")
                {
                    await ctx.CallActivityAsync("PublishVideo",
                        new { transcodedLocation, thumbnailLocation, withIntroLocation });
                    return "Approved and published";
                }
                await ctx.CallActivityAsync("RejectVideo",
                    new { transcodedLocation, thumbnailLocation, withIntroLocation });
                return $"Not published because {approvalResult}";

            }
            catch (Exception e)
            {
                log.Error("Failed to process video with error " + e.Message);
                await ctx.CallActivityAsync("Cleanup", videoLocation);
                return new {Error = "Failed to process video", e.Message};
            }
        }

        [FunctionName("TranscodeOrchestrator")]
        public static async Task<object> TranscodeOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var videoLocation = ctx.GetInput<string>();
            var desiredBitrates = await
                ctx.CallActivityAsync<int[]>("GetTranscodeBitrates", null);
            var transcodeTasks = new List<Task<VideoFileInfo>>();
            foreach (var bitrate in desiredBitrates)
            {
                var fileInfo = new VideoFileInfo()
                    {BitRate = bitrate, Location = videoLocation};
                var transcodeTask = ctx.CallActivityAsync<VideoFileInfo>
                    ("TranscodeVideo", fileInfo);
                transcodeTasks.Add(transcodeTask);
            }
            var results = await Task.WhenAll(transcodeTasks);
            var transcodedLocation = results
                .OrderByDescending(r => r.BitRate)
                .First()
                .Location;
            return transcodedLocation;
        }

        [FunctionName("GetApprovalResultOrchestrator")]
        public static async Task<string> GetApprovalResultOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var approvalInfo = ctx.GetInput<ApprovalInfo>();
            await ctx.CallActivityAsync("SendApprovalRequestEmail", approvalInfo);

            string approvalResult;
            using (var cts = new CancellationTokenSource())
            {
                var timeoutAt = ctx.CurrentUtcDateTime.AddSeconds(30);
                var timeoutTask = ctx.CreateTimer(timeoutAt, cts.Token);
                var approvalTask = ctx.WaitForExternalEvent<string>("ApprovalResult");

                var winner = await Task.WhenAny(approvalTask, timeoutTask);
                if (winner == approvalTask)
                {
                    approvalResult = approvalTask.Result;
                    cts.Cancel(); // we should cancel the timeout task
                }
                else
                {
                    approvalResult = "TimedOut";
                }
            }
            return approvalResult;
        }

        [FunctionName("PeriodicTaskOrchestrator")]
        public static async Task<int> PeriodicTaskOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var timesRun = ctx.GetInput<int>();
            timesRun++;
            log.Info($"Starting the PeriodicTaskOrchestrator {ctx.InstanceId}, {timesRun}");
            await ctx.CallActivityAsync("PeriodicActivity", timesRun);
            var nextRun = ctx.CurrentUtcDateTime.AddSeconds(30);
            await ctx.CreateTimer(nextRun, CancellationToken.None);
            ctx.ContinueAsNew(timesRun);
            return timesRun;
        }
    }
}
