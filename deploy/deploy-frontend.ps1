param (
    [string] [Parameter(Mandatory=$true)] $rgName,
    [string] [Parameter(Mandatory=$true)] $appServiceName
)

$initialDirectory = Get-Location

try {
    # Change to the react source directory
    cd ..\src\react

    # Run the build process
    npm run build
    if ($LASTEXITCODE -ne 0) {
        throw "npm run build failed"
    }

    # Change to the dist directory
    cd .\dist

    # Compress the build output
    Compress-Archive -Path * -DestinationPath app.zip -Force

    # Deploy to Azure App Service
    az webapp deploy --resource-group $rgName --name $appServiceName --src-path app.zip --async true
    if ($LASTEXITCODE -ne 0) {
        throw "Azure deployment failed"
    }

    Write-Output "Frontend deployed successfully."

} catch {
    Write-Error $_.Exception.Message
    exit 1
} finally {
    Set-Location $initialDirectory
}