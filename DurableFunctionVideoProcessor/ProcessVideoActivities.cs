using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;
using Newtonsoft.Json;

namespace DurableFunctionVideoProcessor
{
    public class TranscodeParams
    {
        public string InputFile { get; set; }
        public string OutputExtension { get; set; }
        public string FfmpegParams { get; set; }
    }

    public class ApprovalInfo
    {
        public string OrchestrationId { get; set; }
        public string VideoLocation { get; set; }
    }

    static class ActivityHelpers
    {
        public static bool IsInDemoMode => ConfigurationManager.AppSettings["DemoMode"] == "true";

        public static string GetTempFolder()
        {
            var outputFolder = Path.Combine(Path.GetTempPath(), "transcodes", $"{DateTime.Today:yyyy-MM-dd}");
            Directory.CreateDirectory(outputFolder);
            return outputFolder;
        }

        public static string GetReadSas(ICloudBlob blob, TimeSpan validDuration)
        {
            var sas = blob.GetSharedAccessSignature(new SharedAccessBlobPolicy()
            {
                Permissions = SharedAccessBlobPermissions.Read,
                SharedAccessStartTime = DateTimeOffset.UtcNow.AddMinutes(-5),
                SharedAccessExpiryTime = DateTimeOffset.UtcNow + validDuration
            });
            var location = blob.StorageUri.PrimaryUri.AbsoluteUri + sas;
            return location;
        }
    }

    public static class ProcessVideoActivities
    {
        [FunctionName("GetTranscodeProfiles")]
        public static TranscodeParams[] GetTranscodeProfiles(
            [ActivityTrigger] object input,
            TraceWriter log)
        {
            var bitrates = ConfigurationManager.AppSettings["TranscodeProfiles"];
            if (String.IsNullOrEmpty(bitrates))
                return new []
                {
                    new TranscodeParams { OutputExtension = ".mp4", FfmpegParams = "-vcodec libx264 -strict -2 -c:a aac -pix_fmt yuv420p -crf 28 -preset veryfast -profile:v baseline -f mp4 -movflags faststart"},
                    new TranscodeParams { OutputExtension = ".flv", FfmpegParams = "-c:v libx264 -crf 28 -t 5" },
                };
            return JsonConvert.DeserializeObject<TranscodeParams[]>(bitrates);
        }

        [FunctionName("TranscodeVideo")]
        public static async Task<string> TranscodeVideo(
            [ActivityTrigger] TranscodeParams transcodeParams,
            [Blob("processed/transcoded")] CloudBlobDirectory dir,
            TraceWriter log)
        {
            var outputBlobName = Path.GetFileNameWithoutExtension(transcodeParams.InputFile) + transcodeParams.OutputExtension;
            log.Info($"Transcoding {transcodeParams.InputFile} with params {transcodeParams.FfmpegParams} with extension {transcodeParams.OutputExtension}");
            if (ActivityHelpers.IsInDemoMode)
            {
                await Task.Delay(5000); // simulate some work
                return $"{Guid.NewGuid()}{transcodeParams.OutputExtension}";
            }

            var outputFilePath = Path.Combine(ActivityHelpers.GetTempFolder(), $"{Guid.NewGuid()}{transcodeParams.OutputExtension}");
            await FfmpegWrapper.Transcode(transcodeParams.InputFile, transcodeParams.FfmpegParams, outputFilePath, log);
            var outputBlob = dir.GetBlockBlobReference(outputBlobName);
            await outputBlob.UploadFromFileAsync(outputFilePath);
            File.Delete(outputFilePath);
            
            return ActivityHelpers.GetReadSas(outputBlob, TimeSpan.FromHours(2));
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

        private static int extractCount = 0; // purely for demo purposes - don't use static variables in real world function app!
        [FunctionName("ExtractThumbnail")]
        public static async Task<string> ExtractThumbnail(
            [ActivityTrigger] string incomingFile,
            [Blob("processed/thumbnails")] CloudBlobDirectory dir,
            TraceWriter log)
        {
            log.Info($"Extracting thumbnail from {incomingFile}");
            var outputBlobName = Path.GetFileNameWithoutExtension(incomingFile) + "-thumbnail.png";
            if (ActivityHelpers.IsInDemoMode)
            {
                extractCount++;
                if (incomingFile.Contains("bad"))
                    throw new InvalidOperationException($"Failed to extract thumbnail on attempt {extractCount}");
                await Task.Delay(5000); // simulate some work
            }
            else
            {
                var outputFilePath = Path.Combine(ActivityHelpers.GetTempFolder(), $"{Guid.NewGuid()}.png");
                await FfmpegWrapper.Transcode(incomingFile, "-vf  \"thumbnail,scale=640:360\" -frames:v 1", outputFilePath, log);
                var blob = dir.GetBlockBlobReference(outputBlobName);
                await blob.UploadFromFileAsync(outputFilePath);
                File.Delete(outputFilePath);
            }
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
