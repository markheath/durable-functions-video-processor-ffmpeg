using System.Configuration;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace DurableFunctionVideoProcessor
{
    public static class ProcessVideoActivities
    {
        [FunctionName("TranscodeVideo")]
        public static async Task<string> TranscodeVideo(
            [ActivityTrigger] string incomingFile,
            TraceWriter log)
        {
            log.Info($"Transcoding {incomingFile}");
            await Task.Delay(5000); // simulate some work
            return incomingFile + "-transcoded.mp4";
        }

        [FunctionName("PrependIntro")]
        public static async Task<string> PrependIntro(
            [ActivityTrigger] string incomingFile,
            TraceWriter log)
        {
            log.Info($"Prepending intro to {incomingFile}");
            var introLocation = ConfigurationManager.AppSettings["IntroLocation"];
            await Task.Delay(5000); // simulate some work
            return incomingFile + "-with-intro.mp4";
        }

        [FunctionName("ExtractThumbnail")]
        public static async Task<string> ExtractThumbnail(
            [ActivityTrigger] string incomingFile,
            TraceWriter log)
        {
            log.Info($"Extracting thumbnail from {incomingFile}");
            await Task.Delay(5000); // simulate some work
            return incomingFile + "-thumbnail.jpg";
        }


    }
}
