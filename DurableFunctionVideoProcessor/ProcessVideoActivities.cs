using System;
using System.Configuration;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;

namespace DurableFunctionVideoProcessor
{
    public class VideoFileInfo
    {
        public string Location { get; set; }
        public int BitRate { get; set; }
    }

    public class ApprovalInfo
    {
        public string OrchestrationId { get; set; }
        public string VideoLocation { get; set; }
    }


    public static class ProcessVideoActivities
    {
        [FunctionName("GetTranscodeBitRates")]
        public static int[] GetTranscodeBitRates(
            [ActivityTrigger] object input,
            TraceWriter log)
        {
            var bitrates = ConfigurationManager.AppSettings["TranscodeBitRates"];
            if (String.IsNullOrEmpty(bitrates))
                return new [] {1000, 2000, 3000, 4000};
            return bitrates.Split(',').Select(int.Parse).ToArray();
        }

        [FunctionName("TranscodeVideo")]
        public static async Task<VideoFileInfo> TranscodeVideo(
            [ActivityTrigger] VideoFileInfo incomingFile,
            TraceWriter log)
        {
            log.Info($"Transcoding {incomingFile.Location} to {incomingFile.BitRate}");
            await Task.Delay(5000); // simulate some work
            return new VideoFileInfo
            {
                Location = incomingFile + "-transcoded.mp4",
                BitRate = incomingFile.BitRate
            };
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

        private static int extractCount = 0; // purely for demo purposes
        [FunctionName("ExtractThumbnail")]
        public static async Task<string> ExtractThumbnail(
            [ActivityTrigger] string incomingFile,
            TraceWriter log)
        {
            extractCount++;
            log.Info($"Extracting thumbnail from {incomingFile}");
            await Task.Delay(5000); // simulate some work
            if (incomingFile.Contains("bad"))
                throw new InvalidOperationException($"Failed to extract thumbnail on attempt {extractCount}");
            return incomingFile + "-thumbnail.jpg";
        }

        [FunctionName("Cleanup")]
        public static async Task<string> Cleanup(
            [ActivityTrigger] string incomingFile,
            TraceWriter log)
        {
            log.Info($"Cleaning up {incomingFile}");
            await Task.Delay(5000); // simulate some work
            return "Finished";
        }

        [FunctionName("SendApprovalRequestEmail")]
        public static async Task<string> SendApprovalRequestEmail(
            [ActivityTrigger] ApprovalInfo approvalInfo,
            TraceWriter log)
        {
            log.Info($"Sending approval request for {approvalInfo.VideoLocation}");
            var host = Environment.GetEnvironmentVariable("HTTP_HOST") ?? "localhost:7071";
            var functionAddress = $"http://{host}/api/SubmitVideoApproval?id={approvalInfo.OrchestrationId}";
            var approvedLink = functionAddress + "&result=Approved";
            var rejectedLink = functionAddress + "&result=Rejected";
            var emailContent = $"Please review {approvalInfo.VideoLocation}\n"
                               + $"To approve click {approvedLink}\n"
                               + $"To reject click {rejectedLink}";
            log.Warning(emailContent);
            await Task.Delay(5000); // simulate sending email
            return "Approval request sent";
        }

        [FunctionName("PublishVideo")]
        public static async Task<string> PublishVideo(
            [ActivityTrigger] object videoLocations,
            TraceWriter log)
        {
            log.Info($"Publishing video");
            await Task.Delay(5000); // simulate publishing video
            return "The video is live";
        }

        [FunctionName("RejectVideo")]
        public static async Task<string> RejectVideo(
            [ActivityTrigger] object videoLocations,
            TraceWriter log)
        {
            log.Info($"Rejecting video");
            await Task.Delay(5000); // simulate deleting videos
            return "All temporary files have been deleted";
        }


        [FunctionName("PeriodicActivity")]
        public static void PeriodicActivity(
            [ActivityTrigger] int timesRun,
            TraceWriter log)
        {
            log.Info($"Running the periodic activity {timesRun}");
        }

        // simplistic example of activity function handling its own exceptions
        /*
        public static object TranscodeVideo2([ActivityTrigger] string input)
        {
            try
            {
                var output = PerformTranscode(input);
                return new {Success = true, Output = output};
            } catch (Exception e) {
                return new { Success = false, Error = e.Message };
            }
        } */

    }
}
