using System;
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
                var transcodedLocation = await ctx.CallActivityAsync<string>
                    ("TranscodeVideo", videoLocation);
                var thumbnailLocation = await ctx.CallActivityWithRetryAsync<string>("ExtractThumbnail",
                    new RetryOptions(TimeSpan.FromSeconds(5), 4), transcodedLocation);
                var withIntroLocation = await ctx.CallActivityAsync<string>
                    ("PrependIntro", transcodedLocation);
                return new { transcodedLocation, thumbnailLocation, withIntroLocation };
            }
            catch (Exception e)
            {
                log.Error("Failed to process video with error " + e.Message);
                await ctx.CallActivityAsync("Cleanup", videoLocation);
                return new {Error = "Failed to process video", e.Message};
            }
        }
    }
}
