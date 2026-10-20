# Packs NServiceBus from the saga-timeout-full-messages-satellite branch (PR #7996) into the local feed used by the demo.
# Bump the version after each change, because NuGet caches packages by version.
param(
    [string]$Repository = "C:\Particular\NServiceBus",
    [string]$Version = "10.3.0-timeouts.2"
)

dotnet build (Join-Path $Repository "src\NServiceBus.Core.Analyzer.Fixes\NServiceBus.Core.Analyzer.Fixes.csproj") -c Release
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet pack (Join-Path $Repository "src\NServiceBus.Core\NServiceBus.Core.csproj") -c Release -p:MinVerVersionOverride=$Version -o (Join-Path $PSScriptRoot "..\packages")
