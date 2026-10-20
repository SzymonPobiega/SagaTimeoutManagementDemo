# Packs NServiceBus from the saga-timeout-full-messages-satellite branch (PR #7996) into the local feed used by the demo.
# Bump the version after each change, because NuGet caches packages by version.
param(
    [string]$Repository = "C:\Particular\NServiceBus",
    [string]$Version = "10.3.0-timeouts.4"
)

dotnet build (Join-Path $Repository "src\NServiceBus.Core.Analyzer.Fixes\NServiceBus.Core.Analyzer.Fixes.csproj") -c Release -p:MinVerVersionOverride=$Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

# Pack uses --no-build, so Core must be rebuilt explicitly or a stale binary gets packaged.
$core = Join-Path $Repository "src\NServiceBus.Core\NServiceBus.Core.csproj"
dotnet build $core -c Release --no-incremental -p:MinVerVersionOverride=$Version
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet pack $core -c Release --no-build -p:MinVerVersionOverride=$Version -o (Join-Path $PSScriptRoot "..\packages")
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
