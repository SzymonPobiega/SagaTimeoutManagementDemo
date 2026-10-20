# Saga timeout management demo

An NServiceBus endpoint on RabbitMQ with one saga, `OrderPolicy`. Starting an order requests a buyer's remorse timeout due in 5 minutes. The saga can postpone the timeout by 1 minute or expedite it by 1 minute. ServicePulse shows the timeout and can reschedule, trigger, or cancel it.

The demo uses spike builds:

- NServiceBus from `C:\Particular\NServiceBus`, branch `saga-timeout-full-messages-satellite` ([PR #7996](https://github.com/Particular/NServiceBus/pull/7996)), packed into `packages` as version `10.3.0-timeouts.4`
- NServiceBus.Persistence.Sql `9.1.0-timeouts.1` from [PR #2139](https://github.com/Particular/NServiceBus.Persistence.Sql/pull/2139) (commit `a16fd73b244271c963cfd1a587487a8c6563ce59`), packed into `packages`; see [Rebuilding the SQL persistence](#rebuilding-the-sql-persistence)
- ServiceControl from `C:\Particular\ServiceControl-timeouts-spike`, primary instance with RavenDB
- ServicePulse from `C:\Particular\ServicePulse-timeouts-spike`

## Run with Aspire

The AppHost in `src\AppHost` starts everything in containers except the endpoint: RabbitMQ (quorum queues, conventional routing), SQL Server (database `sagas`, kept in a data volume, created before the endpoint starts) for the endpoint's saga persistence, RavenDB, the ServiceControl error instance (`ghcr.io/particular/servicecontrol:pr-6025`) and ServicePulse (`ghcr.io/particular/servicepulse:pr-3166`). There are no audit or monitoring instances. The endpoint runs as a project and gets its RabbitMQ and `ConnectionStrings:sagas` SQL Server connection strings from the AppHost. The ServiceControl RavenDB database is separate. The AppHost uses user secrets for its generated SQL password; retain those secrets while keeping the SQL data volume.

Prerequisites:

- A container runtime such as Docker; host port 15672 must be free for RabbitMQ's management API (stop a locally running RabbitMQ first)
- .NET 10 SDK
- A Particular license. The AppHost passes it on from the `PARTICULARSOFTWARE_LICENSE` environment variable (the license text) or from the default license file location (`%PROGRAMDATA%\ParticularSoftware\license.xml` on Windows)
- The spike packages `NServiceBus` `10.3.0-timeouts.4` and `NServiceBus.Persistence.Sql` `9.1.0-timeouts.1` in `packages` (see [Rebuilding NServiceBus](#rebuilding-nservicebus) and [Rebuilding the SQL persistence](#rebuilding-the-sql-persistence)); the endpoint does not restore without them

```powershell
dotnet run --project src\AppHost
```

Open the Aspire dashboard URL that is printed, then ServicePulse from the resource list and go to its Timeouts page.

The endpoint serves a page at its root URL. Copy the endpoint's URL from the dashboard (the `orderendpoint` resource, which uses http://localhost:5180 when run standalone) and open it in a browser. Enter an order id (up to 200 characters, matching the SQL correlation column) and use the Start, Postpone and Expedite buttons. The page shows a recent history of what the saga handled, newest first: placed, postponed, expedited and timeout completed. A message shown as accepted is only queued; history entries are added when the saga handles it.

Starting an order that is already started is only logged (history shows "Already started") and never requests another timeout, even if the earlier one was canceled. Postpone and expedite move every pending timeout of the order, which covers saga data that already holds several.

The history keeps the last 100 entries in memory and is cleared when the endpoint restarts. It records handler observations before the saga persistence commits, so retries can produce duplicates; it is not a durable audit. It is also available as JSON at `GET /history`. The page uses the same HTTP routes, which can be called directly:

```bash
curl -X POST <endpoint-url>/orders/order-1            # start an order
curl -X POST <endpoint-url>/orders/order-1/postpone   # postpone the timeout by 1 minute
curl -X POST <endpoint-url>/orders/order-1/expedite   # expedite the timeout by 1 minute
```

The routes answer 202 Accepted once the message is sent to the endpoint's own queue. They are unauthenticated demo commands; keep them local.

The saga and its timeouts are stored with the SQL persistence prerelease in SQL Server. The prerelease keeps the timeouts in the saga table's `Metadata` column, so there is no separate timeout API or table. The endpoint's installers (`EnableInstallers`) create the `[dbo].[SagaTimeoutDemo_Orders_OrderPolicy]` saga table in the `sagas` database; the AppHost creates the database itself. The generated creation script for the saga is `src\OrderEndpoint\obj\Debug\net10.0\NServiceBus.Persistence.Sql\MsSqlServer\Sagas\OrderPolicy_Create.sql` after a build. State that earlier runs kept in the learning persistence's `.sagas` folder is not migrated.

To check the root page, history, HTTP routes, the connection string handling and the saga timeout behavior, run `dotnet run --project src\OrderEndpoint.Tests`. The saga timeout checks run against the learning persistence with test sagas (SQL script generation is disabled in the test project); they are not SQL Server runtime tests.

## Run manually

The spike builds below can also be run without Aspire.

### Prerequisites

- RabbitMQ on `host=localhost`
- .NET 10 SDK and Node.js (Node 22 works; the script sets `--experimental-strip-types` for Node versions below 24)
- A Particular license in `C:\ProgramData\ParticularSoftware\license.xml`
- A SQL Server with a dedicated database for the sagas. The endpoint does not start without the `sagas` connection string, and does not fall back to another persistence

### Steps

Use a separate PowerShell window for each step.

1. Start ServiceControl. It creates its queues and indexes on the first run, then serves http://localhost:33333/api.

   ```powershell
   .\scripts\start-servicecontrol.ps1
   ```

2. Start ServicePulse. It builds the frontend and serves http://localhost:5291. Add `-SkipFrontendBuild` when the frontend has not changed.

   ```powershell
   .\scripts\start-servicepulse.ps1
   ```

3. Start the endpoint with the connection string of that database.

   ```powershell
   $env:ConnectionStrings__sagas = 'Server=localhost;Database=sagas;User Id=sa;Password=<password>;TrustServerCertificate=true'
   dotnet run --project src\OrderEndpoint
   ```

4. Open http://localhost:5291/#/timeouts. If ServicePulse was opened before with other connection settings, open http://localhost:5291/?scu=/api/&mu=! once.

When the timeout fires, the saga logs the note from the timeout payload and completes. ServiceControl then removes the saga's timeouts.

## What to try

- Start an order and watch it appear in the Pending tab.
- Postpone and expedite it from the endpoint's page. Each change appears in the detail page history with source "saga".
- Reschedule it from ServicePulse, optionally with a changed `Note` in the payload. The endpoint applies the change through its `SagaTimeoutDemo.Orders.SagaTimeouts` satellite queue, and the history shows source "operator".
- Change the timeout from the endpoint while the detail page is open, then apply an action from the page. The action is rejected because the version changed.
- Use Trigger now. The saga fires the timeout and logs the operator's note if you changed it.

## Rebuilding NServiceBus

Check out `saga-timeout-full-messages-satellite` (PR #7996) in your NServiceBus repository, then run `scripts\pack-nservicebus.ps1 -Repository <path-to-NServiceBus>` to produce the initial `10.3.0-timeouts.4` package. The base `saga-timeout-full-messages` branch does not contain the reporting opt-in API this demo uses.

After changing NServiceBus, pack it with a new version and update `src\OrderEndpoint\OrderEndpoint.csproj` to that version:

```powershell
.\scripts\pack-nservicebus.ps1 -Version 10.3.0-timeouts.5
```

## Rebuilding the SQL persistence

The package is built from the PR #2139 branch of the NServiceBus.Persistence.Sql repository, which depends on a different NServiceBus prerelease than this demo's. Check out the PR branch (commit `a16fd73b244271c963cfd1a587487a8c6563ce59`), then change the `NServiceBus` reference in `src\SqlPersistence\SqlPersistence.csproj` to `10.3.0-timeouts.4`. That is the only source-code change. The existing `9.1.0-timeouts.1` package was built against `10.3.0-timeouts.2`, but its dependency range `[10.3.0-timeouts.2, 11.0.0)` accepts `10.3.0-timeouts.4`, so it does not need rebuilding. Add this demo's `packages` folder to the SQL persistence repository's `nuget.config` using the source name `local packages`, which its package source mapping already recognizes. For a rebuild, use a new SQL package version to avoid NuGet's cache, then update the endpoint's SQL package reference if you use that rebuilt package:

```powershell
dotnet nuget add source <path-to-demo>\packages --name "local packages" --configfile nuget.config
dotnet build src\SqlPersistence\SqlPersistence.csproj -c Release -p:MinVerVersionOverride=9.1.0-timeouts.2
dotnet pack src\SqlPersistence\SqlPersistence.csproj -c Release --no-build -p:MinVerVersionOverride=9.1.0-timeouts.2 -o <path-to-demo>\packages
```

## Reset

Stop all processes, delete `.servicecontrol-db` from this folder and delete the demo queues in RabbitMQ.

The SQL saga state lives in the `sagas` database. With Aspire, delete the AppHost's `sqlserver` data volume (or only the `sagas` database in that server) to start clean. For a manual run, drop `[dbo].[SagaTimeoutDemo_Orders_OrderPolicy]` or the dedicated database you created; do not touch unrelated databases. Old `.sagas` folders (`src\OrderEndpoint\bin\Debug\net10.0\.sagas`) belong to the replaced learning persistence and can be deleted.
