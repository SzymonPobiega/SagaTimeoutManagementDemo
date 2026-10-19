# Builds the ServicePulse frontend from C:\Particular\ServicePulse-timeouts-spike and hosts it on
# http://localhost:5291, proxying /api to the ServiceControl primary instance. Monitoring is turned off.
param(
    [string]$Repository = "C:\Particular\ServicePulse-timeouts-spike",
    [string]$ServiceControlUrl = "http://localhost:33333/api",
    [switch]$SkipFrontendBuild
)

if (-not $SkipFrontendBuild) {
    Push-Location (Join-Path $Repository "src\Frontend")
    try {
        if (-not (Test-Path node_modules)) { npm ci; if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE } }
        # The frontend tooling relies on TypeScript type stripping, which is built in from Node 24
        if ([int]((node --version).TrimStart('v').Split('.')[0]) -lt 24) { $env:NODE_OPTIONS = "--experimental-strip-types" }
        npm run build
        if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
    }
    finally {
        Pop-Location
    }
}

$env:SERVICECONTROL_URL = $ServiceControlUrl
$env:MONITORING_URL = "!"
dotnet run --project (Join-Path $Repository "src\ServicePulse\ServicePulse.csproj") --launch-profile http
