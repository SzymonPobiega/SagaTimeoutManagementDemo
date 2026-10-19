# Saga timeout management demo

An NServiceBus endpoint on RabbitMQ with one saga, `OrderPolicy`. Starting an order requests a buyer's remorse timeout due in 5 minutes. The saga can postpone the timeout by 1 minute or expedite it by 1 minute. ServicePulse shows the timeout and can reschedule, trigger, or cancel it.

The demo uses spike builds:

- NServiceBus from `C:\Particular\NServiceBus`, branch `saga-timeout-full-messages`, packed into `packages` as version `10.3.0-timeouts.1`
- ServiceControl from `C:\Particular\ServiceControl-timeouts-spike`, primary instance with RavenDB
- ServicePulse from `C:\Particular\ServicePulse-timeouts-spike`

## Prerequisites

- RabbitMQ on `host=localhost`
- .NET 10 SDK and Node.js (Node 22 works; the script sets `--experimental-strip-types` for Node versions below 24)
- A Particular license in `C:\ProgramData\ParticularSoftware\license.xml`

## Run

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

Type these in the endpoint window. The order id is optional and defaults to the last started order.

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

After changing the NServiceBus branch, pack it with a new version and update `src\OrderEndpoint\OrderEndpoint.csproj` to that version:

```powershell
.\scripts\pack-nservicebus.ps1 -Version 10.3.0-timeouts.2
```

## Reset

Stop all processes, delete `.servicecontrol-db` from this folder and `src\OrderEndpoint\bin\Debug\net10.0\.sagas`, and delete the demo queues in RabbitMQ.
