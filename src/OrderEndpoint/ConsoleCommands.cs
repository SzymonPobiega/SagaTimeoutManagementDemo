namespace OrderEndpoint;

using Microsoft.Extensions.Hosting;

sealed class ConsoleCommands(IMessageSession messageSession, IHostApplicationLifetime lifetime) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Prints the menu after the endpoint has started and its startup log lines are written
        var started = new TaskCompletionSource();
        await using var registration = lifetime.ApplicationStarted.Register(() => started.TrySetResult());
        await started.Task.WaitAsync(stoppingToken);

        PrintMenu();
        string? lastOrderId = null;

        while (!stoppingToken.IsCancellationRequested)
        {
            var line = await Task.Run(Console.ReadLine, stoppingToken);
            if (line is null)
            {
                return;
            }

            var parts = line.Trim().Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 0)
            {
                continue;
            }

            var orderId = parts.Length > 1 ? parts[1] : lastOrderId;

            switch (parts[0].ToLowerInvariant())
            {
                case "s":
                    lastOrderId = parts.Length > 1 ? parts[1] : $"order-{Random.Shared.Next(10000, 99999)}";
                    await messageSession.SendLocal(new PlaceOrder { OrderId = lastOrderId }, stoppingToken);
                    Console.WriteLine($"Sent PlaceOrder for {lastOrderId}");
                    break;
                case "p" when orderId is not null:
                    await messageSession.SendLocal(new PostponeBuyersRemorse { OrderId = orderId }, stoppingToken);
                    Console.WriteLine($"Sent PostponeBuyersRemorse for {orderId}");
                    break;
                case "e" when orderId is not null:
                    await messageSession.SendLocal(new ExpediteBuyersRemorse { OrderId = orderId }, stoppingToken);
                    Console.WriteLine($"Sent ExpediteBuyersRemorse for {orderId}");
                    break;
                case "p" or "e":
                    Console.WriteLine("Start an order first, or pass an order id, for example: p order-12345");
                    break;
                default:
                    PrintMenu();
                    break;
            }
        }
    }

    static void PrintMenu() => Console.WriteLine("""

        Commands (the order id is optional and defaults to the last started order):
          s [orderId]   start an order saga; it requests a buyer's remorse timeout due in 5 minutes
          p [orderId]   postpone the timeout by 1 minute (the saga reschedules it)
          e [orderId]   expedite the timeout by 1 minute (the saga reschedules it)
        """);
}
