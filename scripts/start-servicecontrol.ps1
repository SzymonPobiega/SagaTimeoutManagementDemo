# Starts the ServiceControl primary instance built from C:\Particular\ServiceControl-timeouts-spike
# with RabbitMQ (host=localhost) and embedded RavenDB, on http://localhost:33333/api.
param(
    [string]$Repository = "C:\Particular\ServiceControl-timeouts-spike",
    [string]$ConnectionString = "host=localhost"
)

$env:SERVICECONTROL_TRANSPORTTYPE = "RabbitMQ.QuorumConventionalRouting"
$env:SERVICECONTROL_CONNECTIONSTRING = $ConnectionString
$env:SERVICECONTROL_PERSISTENCETYPE = "RavenDB"
$env:SERVICECONTROL_DBPATH = Join-Path $PSScriptRoot "..\.servicecontrol-db"
$env:SERVICECONTROL_PORT = "33333"
$env:SERVICECONTROL_DATABASEMAINTENANCEPORT = "33334"
# No audit instance is needed for timeout management
$env:SERVICECONTROL_REMOTEINSTANCES = "[]"
$env:SERVICECONTROL_LOGGINGPROVIDERS = "NLog"
$env:SERVICEBUS_ERRORQUEUE = "error"

$project = Join-Path $Repository "src\ServiceControl\ServiceControl.csproj"

# --setup creates the queues and the RavenDB indexes
dotnet run --project $project --no-launch-profile -- --setup
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }

dotnet run --project $project --no-launch-profile --no-build
