$RESOURCE_GROUP = "VideoProcessorBicep"
$APP_NAME = "videoprocessor567"
$BICEP_FILE = "deploy.bicep"
$LOCATION = "westeurope"

# create a resource group
az group create -n $RESOURCE_GROUP -l $LOCATION

# deploy the bicep file directly
az deployment group create `
  --name videoprocbicep `
  --resource-group $RESOURCE_GROUP `
  --template-file $BICEP_FILE `
  --parameters "appName=$APP_NAME"

# to clean up
az group delete -n $RESOURCE_GROUP