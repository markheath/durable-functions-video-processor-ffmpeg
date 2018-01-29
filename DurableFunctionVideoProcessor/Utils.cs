using System;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net.Http;
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

        private static HttpClient client;
        public static async Task<string> DownloadToLocalFileAsync(string uri)
        {
            var extension = Path.GetExtension(new Uri(uri).LocalPath);
            var outputFilePath = Path.Combine(GetTempTranscodeFolder(), $"{Guid.NewGuid()}{extension}");
            client = client??new HttpClient();
            using (var downloadStream = await client.GetStreamAsync(uri))
            using (var s = File.OpenWrite(outputFilePath))
            {
                await downloadStream.CopyToAsync(s);
            }
            return outputFilePath;
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
                TryDeleteFiles(log, outputFilePath);
            }

            return GetReadSas(outputBlob, TimeSpan.FromHours(2));
        }

        public static void TryDeleteFiles(TraceWriter log, params string[] files)
        {
            foreach (var file in files)
            {
                try
                {
                    if (!String.IsNullOrEmpty(file) && File.Exists(file))
                    {
                        File.Delete(file);
                    }
                }
                catch (Exception e)
                {
                    log.Error($"Failed to clean up temporary file {file}", e);
                }
            }
        }

        public static string CreateLocalConcat(params string[] inputs)
        {
            var fileList = Path.Combine(GetTempTranscodeFolder(), $"{Guid.NewGuid()}.txt");
            File.WriteAllLines(fileList, inputs.Select(f => $"file '{f}'"));
            return fileList;
        }
    }
}