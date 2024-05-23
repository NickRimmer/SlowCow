# set environment variable nugetApiKey
# example: $env:nugetApiKey = "yourApiKey"

param(
    [parameter(Mandatory = $true)][string] $packageId,
    [parameter(Mandatory = $true)][string] $projectPath,
    [parameter(Mandatory = $false)][alias("version")][string] $versionStr,
    [switch] $dry
)

# auto increment version
if (-not $versionStr -or $versionStr -eq "")
{
    $packageIdLowerCased = $packageId.ToLower()
    $versionStr = (Invoke-RestMethod -Uri "https://api.nuget.org/v3-flatcontainer/$packageIdLowerCased/index.json").versions | Sort-Object -Descending | Select-Object -First 1
    [Version]$version = [Version]$versionStr
    Write-Host "Current version: $version" -BackgroundColor Yellow -ForegroundColor Black

    $version = [Version]::new($version.Major, $version.Minor, $version.Build + 1)
    Write-Host "Auto version: $version" -BackgroundColor Green -ForegroundColor Black
}else{
    [Version]$version = [Version]$versionStr
    Write-Host "Version: $version" -BackgroundColor Green -ForegroundColor Black
}

# build and publish
# dotnet clean "$packageId.csproj" -c Release
dotnet build "$projectPath" -c Release -p:Version=$version
dotnet pack "$projectPath" -c Release -p:Version=$version -o "./packages"

# if not dry
if (-not $dry)
{
    nuget push "./packages/$packageId.$version.nupkg" -Source "https://api.nuget.org/v3/index.json" -ApiKey $env:nugetApiKey
}
