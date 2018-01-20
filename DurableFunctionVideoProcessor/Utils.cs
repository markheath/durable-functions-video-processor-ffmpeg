using System;
using System.Configuration;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DurableFunctionVideoProcessor
{
    static class Utils
    {
        public static bool IsInDemoMode => ConfigurationManager.AppSettings["DemoMode"] == "true";

        private static string GetTempTranscodeFolder()
        {
            var outputFolder = Path.Combine(Path.GetTempPath(), "transcodes", $"{DateTime.Today:yyyy-MM-dd}");
            Directory.CreateDirectory(outputFolder);
            return outputFolder;
        }

        public static string GetReadSas(this ICloudBlob blob, TimeSpan validDuration)
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

        public static async Task<string> TranscodeAndUpload(TranscodeParams transcodeParams, ICloudBlob outputBlob, TraceWriter log)
        {
            var outputFilePath = Path.Combine(GetTempTranscodeFolder(), $"{Guid.NewGuid()}{transcodeParams.OutputExtension}");
            try
            {
                await FfmpegWrapper.Transcode(transcodeParams.InputFile, transcodeParams.FfmpegParams, outputFilePath, log);
                await outputBlob.UploadFromFileAsync(outputFilePath);
            }
            finally
            {
                try
                {
                    if (File.Exists(outputFilePath))
                    {
                        File.Delete(outputFilePath);
                    }
                }
                catch (Exception e)
                {
                    log.Error($"Failed to clean up temporary file {outputFilePath}", e);
                }
            }

            return GetReadSas(outputBlob, TimeSpan.FromHours(2));

        }
    }
}