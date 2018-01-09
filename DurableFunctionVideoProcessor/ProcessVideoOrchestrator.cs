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
            var transcodedLocation = await ctx.CallActivityAsync<string>
                                                ("TranscodeVideo", videoLocation);
            var thumbnailLocation = await ctx.CallActivityAsync<string>
                                                ("ExtractThumbnail", transcodedLocation);
            var withIntroLocation = await ctx.CallActivityAsync<string>
                                                ("PrependIntro", transcodedLocation);
            return new { transcodedLocation, thumbnailLocation, withIntroLocation };
        }
    }
}
