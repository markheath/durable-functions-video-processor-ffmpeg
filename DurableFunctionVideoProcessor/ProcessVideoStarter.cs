using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.Http;
using Microsoft.Azure.WebJobs.Host;

namespace DurableFunctionVideoProcessor
{
    public static class ProcessVideoStarterFunction
    {
        [FunctionName("ProcessVideoStarter")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", "post",
                Route = null)] HttpRequestMessage req,
            [OrchestrationClient] DurableOrchestrationClient starter,
            TraceWriter log)
        {
            // parse query parameter
            string video = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "video", true) == 0)
                .Value;

            // Get request body
            dynamic data = await req.Content.ReadAsAsync<object>();

            // Set name to query string or body data
            video = video ?? data?.video;

            if (video == null)
                return req.CreateResponse(HttpStatusCode.BadRequest,
                    "Please pass a video location on the query string or in the request body");

            log.Info($"Attempting to start video processing for {video}.");
            var instanceId = await starter.StartNewAsync("ProcessVideoOrchestrator", video);
            return starter.CreateCheckStatusResponse(req, instanceId);
        }
    }

    public static class SubmitVideoApprovalFunction
    {
        [FunctionName("SubmitVideoApproval")]
        public static async Task<HttpResponseMessage> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = null)]
            HttpRequestMessage req,
            [OrchestrationClient] DurableOrchestrationClient client,
            TraceWriter log)
        {
            // parse query parameter
            string result = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "result", true) == 0)
                .Value;

            string orchestrationId = req.GetQueryNameValuePairs()
                .FirstOrDefault(q => string.Compare(q.Key, "id", true) == 0)
                .Value;

            if (result == null || orchestrationId == null)
                return req.CreateResponse(HttpStatusCode.BadRequest,
                    "Need an approval result and an orchestration id");

            // send the ApprovalResult external event to this orchestration
            await client.RaiseEventAsync(orchestrationId, "ApprovalResult", result);

            return req.CreateResponse(HttpStatusCode.Accepted);
        }
    }
}
