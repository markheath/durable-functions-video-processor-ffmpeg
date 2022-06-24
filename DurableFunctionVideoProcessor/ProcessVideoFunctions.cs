using System;
using System.Linq;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Extensions.Logging;

namespace DurableFunctionVideoProcessor;

public static class ProcessVideoFunctions
{
    [FunctionName(nameof(ProcessVideoStarter))]
    public static async Task<IActionResult> ProcessVideoStarter(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post",
            Route = null)] HttpRequest req,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
        // parse query parameter
        string video = req.GetQueryParameterDictionary()["video"];

        if (video == null)
            return new BadRequestObjectResult("Please pass a video location on the query string");

        log.LogInformation($"Attempting to start video processing for {video}.");
        var instanceId = await starter.StartNewAsync(OrchestratorNames.ProcessVideo, video);
        return starter.CreateCheckStatusResponse(req, instanceId);
    }

    [FunctionName(nameof(SubmitVideoApproval))]
    public static async Task<IActionResult> SubmitVideoApproval(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "SubmitVideoApproval/{id}")]
        HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        [Table("Approvals", "Approval", "{id}", Connection = "AzureWebJobsStorage")] Approval approval,
        ILogger log)
    {
        // nb if the approval code doesn't exist, framework just returns a 404 before we get here
        // parse query parameter
        string result = req.GetQueryParameterDictionary()
            .FirstOrDefault(q => string.Compare(q.Key, "result", true) == 0)
            .Value;

        if (result == null)
            return new BadRequestObjectResult("Need an approval result");

        log.LogWarning($"Sending approval result to {approval.OrchestrationId} of {result}");
        // send the ApprovalResult external event to this orchestration
        await client.RaiseEventAsync(approval.OrchestrationId, EventNames.ApprovalResult, result);

        return new AcceptedResult();
    }

    [FunctionName(nameof(StartPeriodicTask))]
    public static async Task<IActionResult> StartPeriodicTask(
        [HttpTrigger(AuthorizationLevel.Function, "get", Route = null)]
        HttpRequest req,
        [DurableClient] IDurableOrchestrationClient client,
        ILogger log)
    {
        var instanceId = "PeriodicTask"; // use a fixed id, making it easier for us to terminate
        await client.StartNewAsync(OrchestratorNames.PeriodicTask, instanceId, 0);
        return client.CreateCheckStatusResponse(req, instanceId);
    }

    [FunctionName(nameof(AutoProcessUploadedVideos))]
    public static async Task AutoProcessUploadedVideos(
        [BlobTrigger("uploads/{name}")] BlobClient blob, string name,
        [DurableClient] IDurableOrchestrationClient starter,
        ILogger log)
    {
        var orchestrationId = await starter.StartNewAsync(OrchestratorNames.ProcessVideo, 
            blob.GetReadSas(TimeSpan.FromHours(2)));
        log.LogInformation($"Started an orchestration {orchestrationId} for uploaded video {name}");
    }
}
