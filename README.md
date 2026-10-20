# Saga timeout management demo

An NServiceBus endpoint on RabbitMQ with one saga, `OrderPolicy`. Starting an order requests a buyer's remorse timeout due in 5 minutes. The saga can postpone the timeout by 1 minute or expedite it by 1 minute. ServicePulse shows the timeout and can reschedule, trigger, or cancel it.

The demo uses spike builds:

- NServiceBus from `C:\Particular\NServiceBus`, branch `saga-timeout-full-messages-satellite` ([PR #7996](https://github.com/Particular/NServiceBus/pull/7996)), packed into `packages` as version `10.3.0-timeouts.2`
- ServiceControl from `C:\Particular\ServiceControl-timeouts-spike`, primary instance with RavenDB
- ServicePulse from `C:\Particular\ServicePulse-timeouts-spike`

## Run with Aspire

The AppHost in `src\AppHost` starts everything in containers except the endpoint: RabbitMQ (quorum queues, conventional routing), RavenDB, the ServiceControl error instance (`ghcr.io/particular/servicecontrol:pr-6025`) and ServicePulse (`ghcr.io/particular/servicepulse:pr-3166`). There are no audit or monitoring instances. The endpoint runs as a project and gets its RabbitMQ connection string from the AppHost.

Prerequisites:

- A container runtime such as Docker; host port 15672 must be free for RabbitMQ's management API (stop a locally running RabbitMQ first)
- .NET 10 SDK
- A Particular license. The AppHost passes it on from the `PARTICULARSOFTWARE_LICENSE` environment variable (the license text) or from the default license file location (`%PROGRAMDATA%\ParticularSoftware\license.xml` on Windows)
- The spike NServiceBus package `10.3.0-timeouts.2` in `packages` (see [Rebuilding NServiceBus](#rebuilding-nservicebus)); the endpoint does not restore without it

```powershell
dotnet run --project src\AppHost
```

Open the Aspire dashboard URL that is printed, then ServicePulse from the resource list and go to its Timeouts page.

The dashboard cannot send input to a process, so the console commands below are not available in Aspire. The endpoint serves a page at its root URL instead. Copy the endpoint's URL from the dashboard (the `orderendpoint` resource, which uses http://localhost:5180 when run standalone) and open it in a browser. Enter an order id and use the Start, Postpone and Expedite buttons. The page shows a recent history of what the saga handled, newest first: placed, postponed, expedited, timeout completed and no pending timeout. A message shown as accepted is only queued; history entries are added when the saga handles it.

Starting an order that is already started is only logged (history shows "Already started") and never requests another timeout, even if the earlier one was canceled. Postpone and expedite move every pending timeout of the order, which covers saga data that already holds several.

The history keeps the last 100 entries in memory and is cleared when the endpoint restarts. It records handler observations before the saga persistence commits, so retries can produce duplicates; it is not a durable audit. It is also available as JSON at `GET /history`. The page uses the same HTTP routes, which can be called directly:

```bash
curl -X POST <endpoint-url>/orders/order-1            # start an order
curl -X POST <endpoint-url>/orders/order-1/postpone   # postpone the timeout by 1 minute
curl -X POST <endpoint-url>/orders/order-1/expedite   # expedite the timeout by 1 minute
```

The routes answer 202 Accepted once the message is sent to the endpoint's own queue. They are unauthenticated demo commands; keep them local.

The saga timeouts use the learning persistence, which keeps its data in the `.sagas` folder next to the endpoint binaries. It is not the SQL persistence.

To check the root page, history, HTTP routes and the transport connection string handling, run `dotnet run --project src\OrderEndpoint.Tests`.

## Run manually

The spike builds below can also be run without Aspire.

### Prerequisites

- RabbitMQ on `host=localhost`
- .NET 10 SDK and Node.js (Node 22 works; the script sets `--experimental-strip-types` for Node versions below 24)
- A Particular license in `C:\ProgramData\ParticularSoftware\license.xml`

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

3. Start the endpoint.

   ```powershell
   dotnet run --project src\OrderEndpoint
   ```

4. Open http://localhost:5291/#/timeouts. If ServicePulse was opened before with other connection settings, open http://localhost:5291/?scu=/api/&mu=! once.

## Endpoint commands

Only when the endpoint runs standalone. Type these in the endpoint window. The order id is optional and defaults to the last started order.

| Command | Effect |
|---|---|
| `s [orderId]` | Starts an order saga, which requests a timeout due in 5 minutes |
| `p [orderId]` | The saga postpones the timeout by 1 minute |
| `e [orderId]` | The saga expedites the timeout by 1 minute |

When the timeout fires, the saga logs the note from the timeout payload and completes. ServiceControl then removes the saga's timeouts.

## What to try

- Start an order and watch it appear in the Pending tab.
- Postpone and expedite it from the endpoint. Each change appears in the detail page history with source "saga".
- Reschedule it from ServicePulse, optionally with a changed `Note` in the payload. The endpoint applies the change through its `SagaTimeoutDemo.Orders.SagaTimeouts` satellite queue, and the history shows source "operator".
- Change the timeout from the endpoint while the detail page is open, then apply an action from the page. The action is rejected because the version changed.
- Use Trigger now. The saga fires the timeout and logs the operator's note if you changed it.

## Rebuilding NServiceBus

Check out `saga-timeout-full-messages-satellite` (PR #7996) in your NServiceBus repository, then run `scripts\pack-nservicebus.ps1 -Repository <path-to-NServiceBus>` to produce the initial `10.3.0-timeouts.2` package. The base `saga-timeout-full-messages` branch does not contain the reporting opt-in API this demo uses.

After changing NServiceBus, pack it with a new version and update `src\OrderEndpoint\OrderEndpoint.csproj` to that version:

```powershell
.\scripts\pack-nservicebus.ps1 -Version 10.3.0-timeouts.3
```

## Reset

Stop all processes, delete `.servicecontrol-db` from this folder and `src\OrderEndpoint\bin\Debug\net10.0\.sagas`, and delete the demo queues in RabbitMQ.
