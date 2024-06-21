param(
    [switch] $publish = $false,
    [switch] $build = $false,
    [string] $version
)

$ScriptDirectory = Split-Path -Parent $MyInvocation.MyCommand.Definition
$TempDirectory = "$ScriptDirectory/../_temp"

# create temp directory if not exists
if (!(Test-Path $TempDirectory )) {
    New-Item -ItemType Directory -Path $TempDirectory  -Force
}

$outputFolder = "$TempDirectory/nuget"
$project = "SlowCow.Apps.Publisher"

if ($build) {
    Write-Host ""
    Write-Host "Packing $project" -ForegroundColor Green
    dotnet pack "$ScriptDirectory/../src/$project/$project.csproj" `
        -c Release `
        -o "$outputFolder" `
        -p:PackageVersion=$version `
        -p:Version=$version
}

if ($publish) {
    Write-Host ""
    Write-Host "Publishing $project" -ForegroundColor Green
    dotnet nuget push "$outputFolder/SlowCow.$version.nupkg" `
        --source "https://api.nuget.org/v3/index.json" `
        --api-key $env:NUGET_API_KEY_SLOWCOW
}