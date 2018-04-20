using System.Threading.Tasks;
using Microsoft.Azure.WebJobs.Host;
using Microsoft.WindowsAzure.Storage.Blob;

namespace DurableFunctionVideoProcessor
{
    interface IVideoProcessor
    {
        Task<string> TranscodeAsync(TranscodeParams transcodeParams, ICloudBlob outputBlob, TraceWriter log);
        Task<string> PrependIntroAsync(CloudBlockBlob outputBlob, string introLocation, string incomingFile, TraceWriter log);
        Task<string> ExtractThumbnailAsync(string incomingFile, CloudBlockBlob outputBlob, TraceWriter log);
        Task PublishVideo(string[] videoLocations);
        Task RejectVideo(string[] videoLocations);
    }
}