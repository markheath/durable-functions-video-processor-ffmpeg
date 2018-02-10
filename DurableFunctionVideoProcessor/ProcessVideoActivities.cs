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
    public static class ProcessVideoActivities
    {
        [FunctionName(ActivityNames.GetTranscodeProfiles)]
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

        [FunctionName(ActivityNames.TranscodeVideo)]
        public static async Task<string> TranscodeVideo(
            [ActivityTrigger] TranscodeParams transcodeParams,
            [Blob("processed/transcoded")] CloudBlobDirectory dir,
            TraceWriter log)
        {
            var outputBlobName = Path.GetFileNameWithoutExtension(transcodeParams.InputFile) + transcodeParams.OutputExtension;
            log.Info($"Transcoding {transcodeParams.InputFile} with params {transcodeParams.FfmpegParams} with extension {transcodeParams.OutputExtension}");
            if (Utils.IsInDemoMode)
            {
                await Task.Delay(5000); // simulate some work
                return $"{Path.GetFileNameWithoutExtension(transcodeParams.InputFile)}{Guid.NewGuid():N}{transcodeParams.OutputExtension}";
            }

            var outputBlob = dir.GetBlockBlobReference(outputBlobName);
            return await Utils.TranscodeAndUpload(transcodeParams, outputBlob, log);
        }


        [FunctionName(ActivityNames.PrependIntro)]
        public static async Task<string> PrependIntro(
            [ActivityTrigger] string incomingFile,
            [Blob("processed/final")] CloudBlobDirectory dir,
            TraceWriter log)
        {
            log.Info($"Prepending intro to {incomingFile}");
            var outputBlobName = Path.GetFileNameWithoutExtension(incomingFile) + ".mp4";
            var introLocation = ConfigurationManager.AppSettings["IntroLocation"];
            if (string.IsNullOrEmpty(introLocation)) 
                throw new InvalidOperationException("Missing intro video location");

            if (Utils.IsInDemoMode)
            {
                await Task.Delay(5000); // simulate some work
                return outputBlobName;
            }
            var outputBlob = dir.GetBlockBlobReference(outputBlobName);

            var localIntro = "";
            var localIncoming = "";
            var localConcat = "";

            try
            {
                localIntro = await Utils.DownloadToLocalFileAsync(introLocation);
                localIncoming = await Utils.DownloadToLocalFileAsync(incomingFile);
                localConcat = Utils.CreateLocalConcat(localIntro, localIncoming);
                var transcodeParams = new TranscodeParams
                {
                    OutputExtension = ".mp4",
                    InputFile = incomingFile,
                    FfmpegParams = $"-f concat -safe 0 -i \"{localConcat}\" -codec copy "

                    //InputFile = $"concat:{introLocation}|{incomingFile}", // doesn't work with Uris
                    //FfmpegParams = "-codec copy"
                    //InputFile = introLocation,
                    //FfmpegParams = $"-i \"{incomingFile}\" -filter_complex \"[0:0] [0:1] [1:0] [1:1] concat=n=2:v=1:a=1 [v] [a]\" -map \"[v]\" -map \"[a]\""
                };
                return await Utils.TranscodeAndUpload(transcodeParams, outputBlob, log);
            }
            finally
            {
                Utils.TryDeleteFiles(log, localIntro, localIncoming, localConcat);
            }
        }

        private static int extractCount = 0; // purely for demo purposes - don't use static variables in real world function app!
        [FunctionName(ActivityNames.ExtractThumbnail)]
        public static async Task<string> ExtractThumbnail(
            [ActivityTrigger] string incomingFile,
            [Blob("processed/thumbnails")] CloudBlobDirectory dir,
            TraceWriter log)
        {
            log.Info($"Extracting thumbnail from {incomingFile}");
            var outputBlobName = Path.GetFileNameWithoutExtension(incomingFile) + "-thumbnail.png";
            if (Utils.IsInDemoMode)
            {
                extractCount++;
                if (incomingFile.Contains("bad"))
                    throw new InvalidOperationException($"Failed to extract thumbnail on attempt {extractCount}");
                await Task.Delay(5000); // simulate some work
                return outputBlobName;
            }

            var outputBlob = dir.GetBlockBlobReference(outputBlobName);
            var transcodeParams = new TranscodeParams
            {
                OutputExtension = ".png",
                InputFile = incomingFile,
                FfmpegParams = "-vf  \"thumbnail,scale=640:360\" -frames:v 1"
            };
            return await Utils.TranscodeAndUpload(transcodeParams, outputBlob, log);
        }

        [FunctionName(ActivityNames.Cleanup)]
        public static async Task<string> Cleanup(
            [ActivityTrigger] string incomingFile,
            TraceWriter log)
        {
            log.Info($"Cleaning up {incomingFile}");
            await Task.Delay(5000); // simulate some work
            return "Finished";
        }

        [FunctionName(ActivityNames.SendApprovalRequestEmail)]
        public static async Task<int> SendApprovalRequestEmail(
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
            var approvalTimeoutSeconds = ConfigurationManager.AppSettings["ApprovalTimeoutSeconds"];
            if (string.IsNullOrEmpty(approvalTimeoutSeconds))
                return 30;
            return Int32.Parse(approvalTimeoutSeconds);

        }

        [FunctionName(ActivityNames.PublishVideo)]
        public static async Task<string> PublishVideo(
            [ActivityTrigger] object videoLocations,
            TraceWriter log)
        {
            log.Info($"Publishing video");
            await Task.Delay(5000); // simulate publishing video
            return "The video is live";
        }

        [FunctionName(ActivityNames.RejectVideo)]
        public static async Task<string> RejectVideo(
            [ActivityTrigger] object videoLocations,
            TraceWriter log)
        {
            log.Info($"Rejecting video");
            await Task.Delay(5000); // simulate deleting videos
            return "All temporary files have been deleted";
        }


        [FunctionName(ActivityNames.PeriodicActivity)]
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
