using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DurableFunctionVideoProcessor
{
    class FfmpegVideoProcessor : IVideoProcessor
    {
        public async Task<string> TranscodeAsync(TranscodeParams transcodeParams, ICloudBlob outputBlob, TraceWriter log)
        {
            return await Utils.TranscodeAndUpload(transcodeParams, outputBlob, log);
        }

        public async Task<string> PrependIntroAsync(CloudBlockBlob outputBlob, string introLocation, 
            string incomingFile, TraceWriter log)
        {
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

        public async Task<string> ExtractThumbnailAsync(string incomingFile, CloudBlockBlob outputBlob, TraceWriter log)
        {
            var transcodeParams = new TranscodeParams
            {
                OutputExtension = ".png",
                InputFile = incomingFile,
                FfmpegParams = "-vf  \"thumbnail,scale=640:360\" -frames:v 1"
            };
            return await Utils.TranscodeAndUpload(transcodeParams, outputBlob, log);
        }

        public Task PublishVideo(string[] videoLocations)
        {
            // TODO: move files to new location
            return Task.Delay(5000);
        }

        public Task RejectVideo(string[] videoLocations)
        {
            // TODO: move files to rejected location
            return Task.Delay(5000);
        }
    }
}