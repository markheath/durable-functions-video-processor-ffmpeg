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
                var transcodedLocations = await
                    ctx.CallSubOrchestratorAsync<string[]>("TranscodeOrchestrator",
                        videoLocation);
                var transcodedLocation = transcodedLocations.First(x => x.EndsWith(".mp4"));

                var thumbnailLocation = await ctx.CallActivityWithRetryAsync<string>("ExtractThumbnail",
                    new RetryOptions(TimeSpan.FromSeconds(5), 4), // {Handle = ex => ex.InnerException is InvalidOperationException}, - currently not possible #84
                    transcodedLocation);
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
                if (!ctx.IsReplaying)
                    log.Error("Failed to process video with error " + e.Message);
                await ctx.CallActivityAsync("Cleanup", videoLocation);
                return new {Error = "Failed to process video", e.Message};
            }
        }

        [FunctionName("TranscodeOrchestrator")]
        public static async Task<string[]> TranscodeOrchestrator(
            [OrchestrationTrigger] DurableOrchestrationContext ctx,
            TraceWriter log)
        {
            var videoLocation = ctx.GetInput<string>();
            var transcodeProfiles = await
                ctx.CallActivityAsync<TranscodeParams[]>("GetTranscodeProfiles", null);
            var transcodeTasks = new List<Task<string>>();
            foreach (var transcodeProfile in transcodeProfiles)
            {
                transcodeProfile.InputFile = videoLocation;
                var transcodeTask = ctx.CallActivityAsync<string>
                    ("TranscodeVideo", transcodeProfile);
                transcodeTasks.Add(transcodeTask);
            }
            var locations = await Task.WhenAll(transcodeTasks);
            return locations;
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
            if (!ctx.IsReplaying)
                log.Info($"Starting the PeriodicTaskOrchestrator {ctx.InstanceId}, {timesRun}");
            await ctx.CallActivityAsync("PeriodicActivity", timesRun);
            var nextRun = ctx.CurrentUtcDateTime.AddSeconds(30);
            await ctx.CreateTimer(nextRun, CancellationToken.None);
            ctx.ContinueAsNew(timesRun);
            return timesRun;
        }


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
    }
}
