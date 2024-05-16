# set environment variable nugetApiKey
# example: $env:nugetApiKey = "yourApiKey"

param(
    [parameter(Mandatory = $false)]
    [alias("version")][string] $versionStr,
    [switch] $dry
)

# auto increment version
if (-not $versionStr)
{
    $versionStr = (Invoke-RestMethod -Uri "https://api.nuget.org/v3-flatcontainer/slowcow.setup/index.json").versions | Sort-Object -Descending | Select-Object -First 1
    [Version]$version = [Version]$versionStr
    Write-Host "Current version: $version" -BackgroundColor Yellow -ForegroundColor Black

    $version = [Version]::new($version.Major, $version.Minor, $version.Build + 1)
    Write-Host "Auto version: $version" -BackgroundColor Green -ForegroundColor Black
}else{
    [Version]$version = [Version]$versionStr
    Write-Host "Version: $version" -BackgroundColor Green -ForegroundColor Black
}

# build and publish
dotnet build -c Release -p:Version=$version
dotnet pack -c Release -p:Version=$version

# if not dry
if (-not $dry)
{
    nuget push "bin/Release/SlowCow.Setup.$version.nupkg" -Source "https://api.nuget.org/v3/index.json" -ApiKey $env:nugetApiKey
}