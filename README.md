### Durable Functions Video Processing Pipeline

Sample Video Processing pipeline built with Azure Durable Functions, making use of various Durable Functions features including wating for external events, retries, timeouts and sub-orchestrations.

### Starting the process video workflow

There are two modes it can work in. If `DemoMode` is set to `true`, each step is just a 5 second delay. The URI of the input video doesn't matter and so in this case you can just kick off a new orchestration with a HTTP get request to the the `ProcessVideoStarter` function providing a video parameter:

```
http://localhost:7071/api/ProcessVideoStarter?video=example.mp4
```

If you want to test retrying errors, pass a video name containing the string "bad" and the extract thumbnail activity will fail. After a few retries the workflow will abort.

```
http://localhost:7071/api/ProcessVideoStarter?video=example.mp4
```

If you want to operate in non-demo mode, which will actually use FFMpeg.exe to transcode videos, the easiest way is to upload a (preferably short) video into the `uploads` container in your blob storage account. The generated output files will appear in the `processed` folder. Remember that in consumption mode, Azure Functions are limited to running for 5 minutes, so you won't be able to process large video files. You'd need to use a regular App Service plan to perform longer-running transcodes.




### Local settings

Example contents of a `local.settings.json` file working with local storage (remove DemoMode for actual transcodes)
```
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "AzureWebJobsDashboard": "UseDevelopmentStorage=true",
    "TranscodeProfiles": "[\r\n      {\r\n        \"OutputExtension\": \".mp4\",\r\n        \"FfmpegParams\": \"-vcodec libx264 -strict -2 -c:a aac -pix_fmt yuv420p -crf 28 -preset veryfast -profile:v baseline -f mp4 -movflags faststart\"\r\n      },\r\n      {\r\n        \"OutputExtension\": \".mp3\",\r\n        \"FfmpegParams\": \"-acodec libmp3lame -q:a 6\"\r\n      }\r\n    ]",
    "IntroLocation": "http://127.0.0.1:10000/devstoreaccount1/public/intro.mp4",
    "DemoMode":  "true",
    "ApprovalTimeoutSeconds": 300,
    "SendGridKey": "your-secret-sendgrid-key",
    "ApproverEmail": "your@email.com",
    "SenderEmail":  "example@sender.com" 
  }
}
```
