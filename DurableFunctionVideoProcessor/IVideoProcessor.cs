using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Logging;

namespace DurableFunctionVideoProcessor;

interface IVideoProcessor
{
    Task<string> TranscodeAsync(TranscodeParams transcodeParams, BlobClient outputBlob, ILogger log);
    Task<string> PrependIntroAsync(BlobClient outputBlob, string introLocation, string incomingFile, ILogger log);
    Task<string> ExtractThumbnailAsync(string incomingFile, BlobClient outputBlob, ILogger log);
    Task PublishVideo(string[] videoLocations);
    Task RejectVideo(string[] videoLocations);
}