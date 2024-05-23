param(
    [parameter(Mandatory = $true)][string] $version,
    [parameter(Mandatory = $false)][switch] $skipSetup,
    [parameter(Mandatory = $false)][switch] $skipApp,
    [parameter(Mandatory = $false)][switch] $skipUpload
)

# build Setup
if (!$skipSetup)
{
    Write-Host "Building Setup" -ForegroundColor Black -BackgroundColor Blue
    if (Test-Path ./_temp/setup)
    {
        Remove-Item -Recurse -Force ./_temp/setup
    }

    dotnet publish ./Example.Setup/Example.Setup.csproj `
        -c Release `
        -r win-x64 `
        -o "./_temp/setup" `
        -p:PublishSingleFile=true `
        -p:SelfContained=true `
        -p:IncludeNativeLibrariesForSelfExtract=true `
        -p:Version=$version `
        -p:AssemblyVersion=$version
}

# build latest Application
if (!$skipApp)
{
    Write-Host "Building Application" -ForegroundColor Black -BackgroundColor Blue
    if (Test-Path ./_temp/app)
    {
        Remove-Item -Recurse -Force ./_temp/app
    }

    dotnet publish ./Example.App/Example.App.csproj `
        -c Release `
        -r win-x64 `
        -o ./_temp/app `
        -p:SelfContained=true `
        -p:Version=$version `
        -p:AssemblyVersion=$version
}

# publish to GitHub
if (!$skipUpload)
{
    Write-Host "Uploading to GitHub" -ForegroundColor Black -BackgroundColor Blue
    
    # $env:GITHUB_PAT must be present
    if (!$env:GITHUB_PAT)
    {
        Write-Host "GITHUB_PAT is not set" -ForegroundColor Black -BackgroundColor Red
        exit
    }
    
    ./_temp/setup/Example.Setup.exe --upload=./upload.json --version $version
}

Write-Host "Completed" -ForegroundColor Black -BackgroundColor Green