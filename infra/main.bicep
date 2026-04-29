targetScope = 'subscription'

@minLength(1)
@maxLength(64)
@description('Name of the the environment which is used to generate a short unique hash used in all resources.')
param environmentName string

@description('References application or service contact information from a Service or Asset Management database')
param serviceManagementReference string = ''

@description('Comma-separated list of client application IDs to pre-authorize for accessing the MCP API (optional)')
param preAuthorizedClientIds string = ''

@description('OAuth2 delegated permissions for App Service Authentication login flow')
param delegatedPermissions array = ['User.Read']

@minLength(1)
@description('Primary location for all resources & Flex Consumption Function App')
@allowed([
  'australiaeast'
  'australiasoutheast'
  'brazilsouth'
  'canadacentral'
  'centralindia'
  'centralus'
  'eastasia'
  'eastus'
  'eastus2'
  'eastus2euap'
  'francecentral'
  'germanywestcentral'
  'italynorth'
  'japaneast'
  'koreacentral'
  'northcentralus'
  'northeurope'
  'norwayeast'
  'southafricanorth'
  'southcentralus'
  'southeastasia'
  'southindia'
  'spaincentral'
  'swedencentral'
  'uaenorth'
  'uksouth'
  'ukwest'
  'westcentralus'
  'westeurope'
  'westus'
  'westus2'
  'westus3'
])
@metadata({
  azd: {
    type: 'location'
  }
})
param location string
param vnetEnabled bool

@description('Which service to deploy. Only one function app is provisioned per deployment.')
@allowed(['tools', 'timer'])
param deployService string = 'tools'

param toolsServiceName string = ''
param toolsUserAssignedIdentityName string = ''
param applicationInsightsName string = ''
param appServicePlanName string = ''
param logAnalyticsName string = ''
param resourceGroupName string = ''
param storageAccountName string = ''
param vNetName string = ''
param timerServiceName string = ''
@description('Id of the user identity to be used for testing and debugging. This is not required in production. Leave empty if not needed.')
param principalId string = deployer().objectId

var abbrs = loadJsonContent('./abbreviations.json')
var resourceToken = toLower(uniqueString(subscription().id, environmentName, location))
var tags = { 'azd-env-name': environmentName }
var deployTools = deployService == 'tools'
var deployTimer = deployService == 'timer'
var toolsFunctionAppName = !empty(toolsServiceName) ? toolsServiceName : '${abbrs.webSitesFunctions}tools-${resourceToken}'
var timerFunctionAppName = !empty(timerServiceName) ? timerServiceName : '${abbrs.webSitesFunctions}timer-${resourceToken}'
var toolsDeploymentStorageContainerName = 'app-package-${take(toolsFunctionAppName, 32)}-${take(toLower(uniqueString(toolsFunctionAppName, resourceToken)), 7)}'
var timerDeploymentStorageContainerName = 'app-package-${take(timerFunctionAppName, 32)}-${take(toLower(uniqueString(timerFunctionAppName, resourceToken)), 7)}'

// Convert comma-separated string to array for pre-authorized client IDs
var preAuthorizedClientIdsArray = !empty(preAuthorizedClientIds) ? map(split(preAuthorizedClientIds, ','), clientId => trim(clientId)) : []

// Organize resources in a resource group
resource rg 'Microsoft.Resources/resourceGroups@2021-04-01' = {
  name: !empty(resourceGroupName) ? resourceGroupName : '${abbrs.resourcesResourceGroups}${environmentName}'
  location: location
  tags: tags
}

// User assigned managed identity to be used by the function app to reach storage and other dependencies
// Assign specific roles to this identity in the RBAC module
module toolsUserAssignedIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.4.1' = if (deployTools) {
  name: 'toolsUserAssignedIdentity'
  scope: rg
  params: {
    location: location
    tags: tags
    name: !empty(toolsUserAssignedIdentityName) ? toolsUserAssignedIdentityName : '${abbrs.managedIdentityUserAssignedIdentities}tools-${resourceToken}'
  }
}

// User assigned managed identity for the timer function app
module timerUserAssignedIdentity 'br/public:avm/res/managed-identity/user-assigned-identity:0.4.1' = if (deployTimer) {
  name: 'timerUserAssignedIdentity'
  scope: rg
  params: {
    location: location
    tags: tags
    name: '${abbrs.managedIdentityUserAssignedIdentities}timer-${resourceToken}'
  }
}

// Create an App Service Plan to group applications under the same payment plan and SKU
module appServicePlan 'br/public:avm/res/web/serverfarm:0.1.1' = {
  name: 'appserviceplan'
  scope: rg
  params: {
    name: !empty(appServicePlanName) ? appServicePlanName : '${abbrs.webServerFarms}${resourceToken}'
    sku: {
      name: 'FC1'
      tier: 'FlexConsumption'
    }
    reserved: true
    location: location
    tags: tags
  }
}

module tools './app/api.bicep' = if (deployTools) {
  name: 'tools'
  scope: rg
  params: {
    name: toolsFunctionAppName
    serviceName: 'tools'
    location: location
    tags: tags
    applicationInsightsName: monitoring.outputs.name
    appServicePlanId: appServicePlan.outputs.resourceId
    runtimeName: 'dotnet-isolated'
    runtimeVersion: '10.0'
    storageAccountName: storage.outputs.name
    enableBlob: storageEndpointConfig.enableBlob
    enableQueue: storageEndpointConfig.enableQueue
    enableTable: storageEndpointConfig.enableTable
    deploymentStorageContainerName: toolsDeploymentStorageContainerName
    identityId: toolsUserAssignedIdentity!.outputs.resourceId
    identityClientId: toolsUserAssignedIdentity!.outputs.clientId
    preAuthorizedClientIds: preAuthorizedClientIdsArray
    appSettings: {
    }
    virtualNetworkSubnetResourceId: vnetEnabled ? serviceVirtualNetwork!.outputs.appSubnetID : ''
        // Authorization parameters
    authClientId: entraApp!.outputs.applicationId
    authIdentifierUri: entraApp!.outputs.identifierUri
    authExposedScopes: entraApp!.outputs.exposedScopes
    authTenantId: tenant().tenantId
    delegatedPermissions: delegatedPermissions
  }
}

// Entra ID application registration for MCP authentication (with predictable hostname)
module entraApp 'app/entra.bicep' = if (deployTools) {
  name: 'entraApp'
  scope: rg
  params: {
    appUniqueName: '${toolsFunctionAppName}-app'
    appDisplayName: 'MCP Authorization App (${toolsFunctionAppName})'
    serviceManagementReference: serviceManagementReference
    functionAppHostname: '${toolsFunctionAppName}.azurewebsites.net'
    preAuthorizedClientIds: preAuthorizedClientIdsArray
    managedIdentityClientId: toolsUserAssignedIdentity!.outputs.clientId
    managedIdentityPrincipalId: toolsUserAssignedIdentity!.outputs.principalId
    tags: tags
  }
}

// Timer App - simple MCP demo (run timer) without authentication
module timer './app/api.bicep' = if (deployTimer) {
  name: 'timer'
  scope: rg
  params: {
    name: timerFunctionAppName
    serviceName: 'timer'
    location: location
    tags: tags
    applicationInsightsName: monitoring.outputs.name
    appServicePlanId: appServicePlan.outputs.resourceId
    runtimeName: 'dotnet-isolated'
    runtimeVersion: '10.0'
    storageAccountName: storage.outputs.name
    enableBlob: storageEndpointConfig.enableBlob
    enableQueue: storageEndpointConfig.enableQueue
    enableTable: storageEndpointConfig.enableTable
    deploymentStorageContainerName: timerDeploymentStorageContainerName
    identityId: timerUserAssignedIdentity!.outputs.resourceId
    identityClientId: timerUserAssignedIdentity!.outputs.clientId
    appSettings: {}
    virtualNetworkSubnetResourceId: vnetEnabled ? serviceVirtualNetwork!.outputs.appSubnetID : ''
  }
}

// Backing storage for Azure functions backend API
module storage 'br/public:avm/res/storage/storage-account:0.8.3' = {
  name: 'storage'
  scope: rg
  params: {
    name: !empty(storageAccountName) ? storageAccountName : '${abbrs.storageStorageAccounts}${resourceToken}'
    allowBlobPublicAccess: false
    allowSharedKeyAccess: false // Disable local authentication methods as per policy
    dnsEndpointType: 'Standard'
    publicNetworkAccess: vnetEnabled ? 'Disabled' : 'Enabled'
    networkAcls: vnetEnabled ? {
      defaultAction: 'Deny'
      bypass: 'None'
    } : {
      defaultAction: 'Allow'
      bypass: 'AzureServices'
    }
    blobServices: {
      containers: deployTools ? [
        {name: toolsDeploymentStorageContainerName}
      ] : [
        {name: timerDeploymentStorageContainerName}
      ]
    }
    minimumTlsVersion: 'TLS1_2'  // Enforcing TLS 1.2 for better security
    location: location
    tags: tags
    skuName: 'Standard_LRS'  // Standard performance with locally redundant storage
  }
}

// Define the configuration object locally to pass to the modules
var storageEndpointConfig = {
  enableBlob: true  // Required for AzureWebJobsStorage, .zip deployment, Event Hubs trigger and Timer trigger checkpointing
  enableQueue: true  // Required for Durable Functions and MCP trigger
  enableTable: false  // Required for Durable Functions and OpenAI triggers and bindings
  enableFiles: false   // Not required, used in legacy scenarios
  allowUserIdentityPrincipal: true   // Allow interactive user identity to access for testing and debugging
}

// Consolidated Role Assignments
module rbac 'app/rbac.bicep' = {
  name: 'rbacAssignments'
  scope: rg
  params: {
    storageAccountName: storage.outputs.name
    appInsightsName: monitoring.outputs.name
    managedIdentityPrincipalId: deployTools ? toolsUserAssignedIdentity!.outputs.principalId : ''
    weatherManagedIdentityPrincipalId: deployTimer ? timerUserAssignedIdentity!.outputs.principalId : ''
    resourcesManagedIdentityPrincipalId: ''
    promptsManagedIdentityPrincipalId: ''
    appsManagedIdentityPrincipalId: ''
    userIdentityPrincipalId: principalId
    enableBlob: storageEndpointConfig.enableBlob
    enableQueue: storageEndpointConfig.enableQueue
    enableTable: storageEndpointConfig.enableTable
    allowUserIdentityPrincipal: storageEndpointConfig.allowUserIdentityPrincipal
  }
}

// Virtual Network & private endpoint to blob storage
module serviceVirtualNetwork 'app/vnet.bicep' =  if (vnetEnabled) {
  name: 'serviceVirtualNetwork'
  scope: rg
  params: {
    location: location
    tags: tags
    vNetName: !empty(vNetName) ? vNetName : '${abbrs.networkVirtualNetworks}${resourceToken}'
  }
}

module storagePrivateEndpoint 'app/storage-PrivateEndpoint.bicep' = if (vnetEnabled) {
  name: 'servicePrivateEndpoint'
  scope: rg
  params: {
    location: location
    tags: tags
    virtualNetworkName: !empty(vNetName) ? vNetName : '${abbrs.networkVirtualNetworks}${resourceToken}'
    subnetName: vnetEnabled ? serviceVirtualNetwork!.outputs.peSubnetName : '' // Keep conditional check for safety, though module won't run if !vnetEnabled
    resourceName: storage.outputs.name
    enableBlob: storageEndpointConfig.enableBlob
    enableQueue: storageEndpointConfig.enableQueue
    enableTable: storageEndpointConfig.enableTable
  }
}

// Monitor application with Azure Monitor - Log Analytics and Application Insights
module logAnalytics 'br/public:avm/res/operational-insights/workspace:0.11.1' = {
  name: '${uniqueString(deployment().name, location)}-loganalytics'
  scope: rg
  params: {
    name: !empty(logAnalyticsName) ? logAnalyticsName : '${abbrs.operationalInsightsWorkspaces}${resourceToken}'
    location: location
    tags: tags
    dataRetention: 30
  }
}
 
module monitoring 'br/public:avm/res/insights/component:0.6.0' = {
  name: '${uniqueString(deployment().name, location)}-appinsights'
  scope: rg
  params: {
    name: !empty(applicationInsightsName) ? applicationInsightsName : '${abbrs.insightsComponents}${resourceToken}'
    location: location
    tags: tags
    workspaceResourceId: logAnalytics.outputs.resourceId
    disableLocalAuth: true
  }
}

// App outputs
output APPLICATIONINSIGHTS_CONNECTION_STRING string = monitoring.outputs.connectionString
output AZURE_LOCATION string = location
output AZURE_TENANT_ID string = tenant().tenantId
output SERVICE_TOOLS_NAME string = deployTools ? tools!.outputs.SERVICE_API_NAME : ''
output SERVICE_TOOLS_DEFAULT_HOSTNAME string = deployTools ? tools!.outputs.SERVICE_MCP_DEFAULT_HOSTNAME : ''
output AZURE_FUNCTION_NAME string = deployTools ? tools!.outputs.SERVICE_API_NAME : ''

// Entra App outputs (using the initial app for core properties)
output ENTRA_APPLICATION_ID string = deployTools ? entraApp!.outputs.applicationId : ''
output ENTRA_APPLICATION_OBJECT_ID string = deployTools ? entraApp!.outputs.applicationObjectId : ''
output ENTRA_SERVICE_PRINCIPAL_ID string = deployTools ? entraApp!.outputs.servicePrincipalId : ''
output ENTRA_IDENTIFIER_URI string = deployTools ? entraApp!.outputs.identifierUri : ''

// Authorization outputs
output AUTH_ENABLED bool = deployTools ? tools!.outputs.AUTH_ENABLED : false
output CONFIGURED_SCOPES string = deployTools ? tools!.outputs.CONFIGURED_SCOPES : ''

// Pre-authorized applications
output PRE_AUTHORIZED_CLIENT_IDS string = preAuthorizedClientIds

// Entra App redirect URI outputs (using predictable hostname)
output CONFIGURED_REDIRECT_URIS array = deployTools ? entraApp!.outputs.configuredRedirectUris : []
output AUTH_REDIRECT_URI string = deployTools ? entraApp!.outputs.authRedirectUri : ''

// Timer App outputs
output SERVICE_TIMER_NAME string = deployTimer ? timer!.outputs.SERVICE_API_NAME : ''
output SERVICE_TIMER_DEFAULT_HOSTNAME string = deployTimer ? timer!.outputs.SERVICE_MCP_DEFAULT_HOSTNAME : ''
