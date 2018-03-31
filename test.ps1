# scripts to test our durable functions

$resourceGroup = "dfvp-test2"
$appName = "dfvptest2"

# secrets are stored in D:\home\data\Functions\secrets\{functionname}.json
# https://github.com/Azure/azure-functions-host/issues/1952
# https://stackoverflow.com/a/46436102/7532


# get the deployment credentials
function getKuduCreds()
{
    $user = az webapp deployment list-publishing-profiles -n $appName -g $resourceGroup `
            --query "[?publishMethod=='MSDeploy'].userName" -o tsv

    $pass = az webapp deployment list-publishing-profiles -n $appName -g $resourceGroup `
            --query "[?publishMethod=='MSDeploy'].userPWD" -o tsv

    $pair = "$($user):$($pass)"
    $encodedCreds = [System.Convert]::ToBase64String([System.Text.Encoding]::ASCII.GetBytes($pair))
    return $encodedCreds
}

function getFunctionKey([string]$functionName, [string]$encodedCreds)
{
    $jwt = Invoke-RestMethod -Uri "https://$appName.scm.azurewebsites.net/api/functions/admin/token" -Headers @{Authorization=("Basic {0}" -f $encodedCreds)} -Method GET

    $keys = Invoke-RestMethod -Method GET -Headers @{Authorization=("Bearer {0}" -f $jwt)} `
            -Uri "https://$appName.azurewebsites.net/admin/functions/$functionName/keys" 

    $code = $keys.keys[0].value
    return $code
}


# automate retrieval of key with https://github.com/Azure/azure-functions-host/wiki/Key-management-API
$kuduCreds = getKuduCreds
$code = getFunctionKey "ProcessVideoStarter" $kuduCreds
$starterFunc = "https://$appName.azurewebsites.net/api/ProcessVideoStarter?code=$code"

# start the video processing orchestrator
$orchestrationInfo = Invoke-RestMethod -Method POST -Uri "$starterFunc&video=example.mp4"

# check up on it
Invoke-RestMethod -Uri $orchestrationInfo.statusQueryGetUri

# https://docs.microsoft.com/en-us/azure/azure-functions/durable-functions-http-api
Start-Process "$($orchestrationInfo.statusQueryGetUri)&showHistory=true" 
Start-Process "$($orchestrationInfo.statusQueryGetUri)&showHistory=true&showHistoryOutput=true" 

# send an approval
$orchestrationId = $orchestrationInfo.id
$approvalUri = $orchestrationInfo.sendEventPostUri.Replace("{eventName}","ApprovalResult").Replace($orchestrationId, "XYZ$orchestrationId")
Invoke-RestMethod -Method POST -Uri $approvalUri -ContentType "application/json" -Body '"Approved"'

# terminate the orchestration
Invoke-WebRequest -Method POST -Uri $orchestrationInfo.terminatePostUri.Replace("{text}","Abandoned")

# Start the periodic task, (n.b. this one currently requires a get)
$code =  getFunctionKey "StartPeriodicTask" $kuduCreds
$starterFunc = "https://$appName.azurewebsites.net/api/StartPeriodicTask?code=$code"
$orchestrationInfo2 = Invoke-RestMethod -Method GET -Uri "$starterFunc"

# check up on it
Invoke-RestMethod -Uri $orchestrationInfo2.statusQueryGetUri

# terminate the orchestration
Invoke-WebRequest -Method POST -Uri $orchestrationInfo2.terminatePostUri.Replace("{text}","Abandoned")
