# This script creates all the infrastrcutre using Azure CLI commands
# Prerequisites:
# - Azure CLI https://docs.microsoft.com/en-us/cli/azure/install-azure-cli
# - Azure Functions Core Tools https://docs.microsoft.com/en-us/azure/azure-functions/functions-run-local?tabs=windows%2Ccsharp%2Cbash#install-the-azure-functions-core-tools
# More information https://markheath.net/post/deploying-azure-functions-with-azure-cli

# step 1 - log in 
az login

# step 2 - ensure you are using the correct subscription
az account set -s "MySubscription"

# step 3 - pick unique names
$RESOURCE_GROUP = "VideoProcessor789"
$FUNCTION_APP_NAME = "videoprocessor789"
$STORAGE_ACCOUNT_NAME = "videoprocessor789"
$APP_INSIGHTS_NAME = "videoprocessor789"
$LOCATION = "westeurope"

# step 4 - create the resource group
az group create -n $RESOURCE_GROUP -l $LOCATION

# step 5 - create the storage account
az storage account create -n $STORAGE_ACCOUNT_NAME -l $LOCATION -g $RESOURCE_GROUP --sku Standard_LRS

# step 6 - create an Application Insights Instance
az resource create `
  -g $RESOURCE_GROUP -n $APP_INSIGHTS_NAME `
  --resource-type "Microsoft.Insights/components" `
  --properties '{\"Application_Type\":\"web\"}'


# step 7 - create the function app, connected to the storage account and app insights
az functionapp create `
  -n $FUNCTION_APP_NAME `
  --storage-account $STORAGE_ACCOUNT_NAME `
  --consumption-plan-location $LOCATION `
  --app-insights $APP_INSIGHTS_NAME `
  --runtime dotnet `
  --functions-version 3 `
  --os-type Windows `
  -g $RESOURCE_GROUP

# step 8 - (optional - publish any settings)
az functionapp config appsettings set -n $FUNCTION_APP_NAME -g $RESOURCE_GROUP `
    --settings "MySetting1=Hello" "MySetting2=World"

# step 9 - publish the applciation code
func azure functionapp publish $FUNCTION_APP_NAME

# CLEANUP
az group delete -n $RESOURCE_GROUP 