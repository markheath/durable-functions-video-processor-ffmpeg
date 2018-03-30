# check we're logged in with the right account first!
az account show --query name -o tsv

$resourceGroup = "dfvp-test2"
$location = "westeurope"
$appName = "dfvptest2"

# Create resource group
az group create -n $resourceGroup -l $location

# Deploy the template
# creates: app service plan (consumption), function app, storage account, app insights
az group deployment create -g $resourceGroup `
        --template-file deploy.json `
        --parameters "appName=$appName"

# --parameters @MySite.parameters.json \


#az functionapp config appsettings set IntroLocation=$introLocation


# to build (n.b. don't know why RunCodeAnalysis has got turned on - can't work out how to disable)
. "C:\Program Files (x86)\Microsoft Visual Studio\2017\Enterprise\MSBuild\15.0\Bin\MSBuild.exe" /p:Configuration=Release /p:RunCodeAnalysis=False

# create a zip
$publishFolder = "$(pwd)\DurableFunctionVideoProcessor\bin\Release\net461"
$destination = "$(pwd)\publish.zip"
If (Test-Path $destination){ Remove-Item $destination }
Add-Type -assembly "system.io.compression.filesystem"
[io.compression.zipfile]::CreateFromDirectory($publishFolder, $destination)

az functionapp deployment source config-zip `
    -n $appName -g $resourceGroup --src $destination