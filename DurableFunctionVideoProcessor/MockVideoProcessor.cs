using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DurableFunctionVideoProcessor
{
    class MockVideoProcessor : IVideoProcessor
    {
        public async Task<string> TranscodeAsync(TranscodeParams transcodeParams, ICloudBlob outputBlob, TraceWriter log)
        {
            await Task.Delay(5000); // simulate some work
            return $"{Path.GetFileNameWithoutExtension(transcodeParams.InputFile)}{Guid.NewGuid():N}{transcodeParams.OutputExtension}";
        }

        public async Task<string> PrependIntroAsync(CloudBlockBlob outputBlob, string introLocation, string incomingFile, TraceWriter log)
        {
            await Task.Delay(5000); // simulate some work
            return outputBlob.Name;
        }

        private static int extractCount = 0; // purely for demo purposes - don't use static variables in real world function app!
        public async Task<string> ExtractThumbnailAsync(string incomingFile, CloudBlockBlob outputBlob, TraceWriter log)
        {
            extractCount++;
            if (incomingFile.Contains("error"))
                throw new InvalidOperationException($"Failed to extract thumbnail on attempt {extractCount}");
            await Task.Delay(5000); // simulate some work
            return outputBlob.Name;
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