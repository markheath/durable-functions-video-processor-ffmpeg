# scripts to test our durable functions

$resourceGroup = "dfvp-test2"
$appName = "dfvptest2"

$code = "TODO: your code here"
$starterFunc = "https://$appName.azurewebsites.net/api/ProcessVideoStarter?code=$code"

# start the video processing orchestrator
$orchestrationInfo = Invoke-RestMethod -Method POST -Uri "$starterFunc&video=example.mp4"

# check up on it
Invoke-RestMethod -Uri $orchestrationInfo.statusQueryGetUri

# send an approval
$orchestrationId = $orchestrationInfo.id
$approvalUri = $orchestrationInfo.sendEventPostUri.Replace("{eventName}","ApprovalResult").Replace($orchestrationId, "XYZ$orchestrationId")
Invoke-RestMethod -Method POST -Uri $approvalUri -ContentType "application/json" -Body '"Approved"'

# terminate the orchestration
Invoke-WebRequest -Method POST -Uri $orchestrationInfo.terminatePostUri.Replace("{text}","Abandoned")