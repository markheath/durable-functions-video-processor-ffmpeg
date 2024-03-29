﻿using System;
using System.IO;
using System.Threading.Tasks;
using Azure.Storage.Blobs;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.WebJobs.Extensions.DurableTask;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SendGrid.Helpers.Mail;

namespace DurableFunctionVideoProcessor;

public static class ProcessVideoActivities
{
    private static readonly IVideoProcessor videoProcessor = Utils.IsInDemoMode ? 
        (IVideoProcessor)new MockVideoProcessor() : new FfmpegVideoProcessor();

    [FunctionName(ActivityNames.GetTranscodeProfiles)]
    public static TranscodeParams[] GetTranscodeProfiles(
        [ActivityTrigger] object input,
        ILogger log)
    {
        var bitrates = Environment.GetEnvironmentVariable("TranscodeProfiles");
        if (String.IsNullOrEmpty(bitrates))
            return new []
            {
                new TranscodeParams { OutputExtension = ".mp4",
                    FfmpegParams = "-vcodec libx264 -strict -2 -c:a aac -pix_fmt yuv420p -crf 28 -preset veryfast -profile:v baseline -f mp4 -movflags faststart"},
                new TranscodeParams { OutputExtension = ".flv",
                    FfmpegParams = "-c:v libx264 -crf 28 -t 5" },
            };
        return JsonConvert.DeserializeObject<TranscodeParams[]>(bitrates);
    }

    [FunctionName(ActivityNames.TranscodeVideo)]
    public static async Task<string> TranscodeVideo(
        [ActivityTrigger] TranscodeParams transcodeParams,
        [Blob("processed")] BlobContainerClient dir,
        ILogger log)
    {
        var outputBlobName = Path.GetFileNameWithoutExtension(transcodeParams.InputFile) + 
                             transcodeParams.OutputExtension;
        log.LogInformation($"Transcoding {transcodeParams.InputFile} with params " +
                 $"{transcodeParams.FfmpegParams} with extension {transcodeParams.OutputExtension}");
        var outputBlob = dir.GetBlobClient($"transcoded/{outputBlobName}");

        return await videoProcessor.TranscodeAsync(transcodeParams, outputBlob, log);
    }


    [FunctionName(ActivityNames.PrependIntro)]
    public static async Task<string> PrependIntro(
        [ActivityTrigger] string incomingFile,
        [Blob("processed")] BlobContainerClient dir,
        ILogger log)
    {
        log.LogInformation($"Prepending intro to {incomingFile}");
        var outputBlobName = Path.GetFileNameWithoutExtension(incomingFile) + ".mp4";
        var introLocation = Environment.GetEnvironmentVariable("IntroLocation");
        if (string.IsNullOrEmpty(introLocation)) 
            throw new InvalidOperationException("Missing intro video location");

        var outputBlob = dir.GetBlobClient($"final/{outputBlobName}");
        return await videoProcessor.PrependIntroAsync(outputBlob, introLocation, incomingFile, log);
    }

    [FunctionName(ActivityNames.ExtractThumbnail)]
    public static async Task<string> ExtractThumbnail(
        [ActivityTrigger] string incomingFile,
        [Blob("processed")] BlobContainerClient dir,
        ILogger log)
    {
        log.LogInformation($"Extracting thumbnail from {incomingFile}");
        var outputBlobName = Path.GetFileNameWithoutExtension(incomingFile) + "-thumbnail.png";
        var outputBlob = dir.GetBlobClient($"thumbnails/{outputBlobName}");
        return await videoProcessor.ExtractThumbnailAsync(incomingFile, outputBlob, log);
    }

    [FunctionName(ActivityNames.Cleanup)]
    public static async Task<string> Cleanup(
        [ActivityTrigger] string incomingFile,
        ILogger log)
    {
        log.LogInformation($"Cleaning up {incomingFile}");
        await Task.Delay(5000); // simulate some work
        return "Finished";
    }

    // sendgrid binding - see https://docs.microsoft.com/en-us/azure/azure-functions/functions-bindings-sendgrid
    // install SendGrid and Microsoft.Azure.WebJobs.Extensions
    [FunctionName(ActivityNames.SendApprovalRequestEmail)]
    public static int SendApprovalRequestEmail(
        [ActivityTrigger] ApprovalInfo approvalInfo,
        [SendGrid(ApiKey = "SendGridKey")] out SendGridMessage message,
        [Table("Approvals", "AzureWebJobsStorage")] out Approval approval,
        ILogger log)
    {
        var approvalCode = Guid.NewGuid().ToString("N");
        approval = new Approval
        {
            PartitionKey = "Approval",
            RowKey = approvalCode,
            OrchestrationId = approvalInfo.OrchestrationId
        };
        var approverEmail = new EmailAddress(Environment.GetEnvironmentVariable("ApproverEmail"));
        var senderEmail = new EmailAddress(Environment.GetEnvironmentVariable("SenderEmail"));
        var subject = "A video is awaiting approval";
        log.LogInformation($"Sending approval request for {approvalInfo.VideoLocation}");
        var host = Environment.GetEnvironmentVariable("WEBSITE_HOSTNAME") ?? "localhost:7071";
        var functionAddress = $"http://{host}/api/SubmitVideoApproval/{approvalCode}";
        var approvedLink = functionAddress + "?result=Approved";
        var rejectedLink = functionAddress + "?result=Rejected";
        var body = $"Please review {approvalInfo.VideoLocation}<br>"
                           + $"<a href=\"{approvedLink}\">Approve</a><br>"
                           + $"<a href=\"{rejectedLink}\">Reject</a>";
        message = new SendGridMessage();
        message.Subject = subject;
        message.From = senderEmail;
        message.AddTo(approverEmail);
        message.HtmlContent = body;

        log.LogWarning(body);
        var approvalTimeoutSeconds = Environment.GetEnvironmentVariable("ApprovalTimeoutSeconds");
        if (string.IsNullOrEmpty(approvalTimeoutSeconds))
            return 30;
        return Int32.Parse(approvalTimeoutSeconds);
    }

    [FunctionName(ActivityNames.PublishVideo)]
    public static async Task<string> PublishVideo(
        [ActivityTrigger] string[] mediaLocations,
        ILogger log)
    {
        log.LogInformation("Publishing video");
        await videoProcessor.PublishVideo(mediaLocations);
        return "The video is live";
    }

    [FunctionName(ActivityNames.RejectVideo)]
    public static async Task<string> RejectVideo(
        [ActivityTrigger] string[] mediaLocations,
        ILogger log)
    {
        log.LogInformation("Rejecting video");
        await videoProcessor.RejectVideo(mediaLocations);
        return "All temporary files have been deleted";
    }


    [FunctionName(ActivityNames.PeriodicActivity)]
    public static void PeriodicActivity(
        [ActivityTrigger] int timesRun,
        ILogger log)
    {
        log.LogInformation($"Running the periodic activity {timesRun}");
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
