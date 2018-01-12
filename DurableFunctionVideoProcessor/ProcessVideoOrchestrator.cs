using System;
using System.Collections.Generic;
using System.Linq;
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
                var desiredBitrates = new[] {1000,2000,3000,4000};
                var transcodeTasks = new List<Task<VideoFileInfo>>();
                foreach (var bitrate in desiredBitrates)
                {
                    var fileInfo = new VideoFileInfo()
                        { BitRate = bitrate, Location = videoLocation};
                    var transcodeTask = ctx.CallActivityAsync<VideoFileInfo>
                        ("TranscodeVideo", fileInfo);
                    transcodeTasks.Add(transcodeTask);
                }
                var results = await Task.WhenAll(transcodeTasks);
                var transcodedLocation = results
                    .First(r => r.BitRate == 4000)
                    .Location;
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
