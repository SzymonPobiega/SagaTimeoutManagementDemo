# Packs NServiceBus from the saga-timeout-full-messages branch into the local feed used by the demo.
# Bump the version after each change, because NuGet caches packages by version.
param(
    [string]$Repository = "C:\Particular\NServiceBus",
    [string]$Version = "10.3.0-timeouts.1"
)

dotnet pack (Join-Path $Repository "src\NServiceBus.Core\NServiceBus.Core.csproj") -c Release -p:MinVerVersionOverride=$Version -o (Join-Path $PSScriptRoot "..\packages")
