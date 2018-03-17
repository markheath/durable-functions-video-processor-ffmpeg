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
