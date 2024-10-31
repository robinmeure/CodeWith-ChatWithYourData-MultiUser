param (
    [string] [Parameter(Mandatory=$true)] $rgName,
    [string] [Parameter(Mandatory=$true)] $frontEndAppServiceName,
    [string] [Parameter(Mandatory=$true)] $functionName,
    [string] [Parameter(Mandatory=$true)] $backEndAppServiceName,
    [string] [Parameter(Mandatory=$true)] $backendUrl,
    [string] [Parameter(Mandatory=$true)] $backendApiscope,
    [string] [Parameter(Mandatory=$true)] $publicAppId,
    [string] [Parameter(Mandatory=$true)] $publicAuthorityUrl
)

## Call the deploy-api.ps1 script
Write-Output "Deploying API..."
.\deploy-api.ps1 -rgName $rgName -appServiceName $backEndAppServiceName

## Call the deploy-function.ps1 script
Write-Output "Deploying function..."
.\deploy-function.ps1 -rgName $rgName -functionName $functionName

## Call the deploy-frontend.ps1 script
Write-Output "Deploying Frontend..."
.\deploy-frontend.ps1 -rgName $rgName -appServiceName $frontEndAppServiceName -backendUrl $backendUrl -backendApiscope $backendApiscope -publicAppId $publicAppId -publicAuthorityUrl $publicAuthorityUrl