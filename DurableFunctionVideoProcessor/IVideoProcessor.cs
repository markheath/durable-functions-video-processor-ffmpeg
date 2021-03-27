using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.Extensions.Logging;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DurableFunctionVideoProcessor
{
    interface IVideoProcessor
    {
        Task<string> TranscodeAsync(TranscodeParams transcodeParams, ICloudBlob outputBlob, ILogger log);
        Task<string> PrependIntroAsync(CloudBlockBlob outputBlob, string introLocation, string incomingFile, ILogger log);
        Task<string> ExtractThumbnailAsync(string incomingFile, CloudBlockBlob outputBlob, ILogger log);
        Task PublishVideo(string[] videoLocations);
        Task RejectVideo(string[] videoLocations);
    }
}