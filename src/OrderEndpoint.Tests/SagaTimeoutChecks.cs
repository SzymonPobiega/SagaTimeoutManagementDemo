using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NServiceBus;
using OrderEndpoint;
using OrderEndpoint.UI;

// Runs the real saga on learning transport and persistence; NServiceBus.Testing cannot FindTimeouts
static class SagaTimeoutChecks
{
    public static Task RepeatedStartIsIgnored() => Run(c => c.AddSaga<OrderPolicy>(), async (send, history, wait) =>
    {
        await send(new PlaceOrder { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Placed"));
        await send(new PlaceOrder { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Already started"));
        Check(history.Snapshot().Select(e => e.Event).SequenceEqual(["Already started", "Placed"]), "second start is only logged");

        await send(new PostponeBuyersRemorse { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Postponed"));
        Check(history.Snapshot().Count(e => e.Event == "Postponed") == 1, "only one timeout exists");
        var due = history.Snapshot().First(e => e.Event == "Postponed").Detail.Split(" -> ")[1].Split(';')[0];

        await send(new PlaceOrder { OrderId = "o1" });
        await wait(() => history.Snapshot().Count(e => e.Event == "Already started") == 2);
        await send(new ExpediteBuyersRemorse { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Expedited"));
        var expedited = history.Snapshot().Where(e => e.Event == "Expedited").ToArray();
        Check(expedited.Length == 1 && expedited[0].Detail.StartsWith($"Due {due} ->"), "ignored start left the pending timeout alone");
    });

    // Saga data written before the duplicate-start guard can hold several timeouts
    public static Task LegacyMultipleTimeoutsAreAllShifted() => Run(c => c.AddSaga<LegacyOrderPolicy>(), async (send, history, wait) =>
    {
        await send(new PlaceOrder { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Placed"));
        await send(new RequestSecondTimeout { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Second timeout"));

        await send(new PostponeBuyersRemorse { OrderId = "o1" });
        await wait(() => history.Snapshot().Count(e => e.Event == "Postponed") == 2);
        var postponed = history.Snapshot().Where(e => e.Event == "Postponed").ToArray();
        Check(postponed.All(e => e.Detail.EndsWith("note: Postponed by the saga")), "postpone shifts every pending timeout");

        await send(new ExpediteBuyersRemorse { OrderId = "o1" });
        await wait(() => history.Snapshot().Count(e => e.Event == "Expedited") == 2);
        var expedited = history.Snapshot().Where(e => e.Event == "Expedited").ToArray();
        Check(expedited.All(e => e.Detail.EndsWith("note: Expedited by the saga")), "expedite shifts every pending timeout");
        // Each expedite starts from a postponed due time, so it undoes exactly one postpone
        Check(postponed.All(p => expedited.Any(e => e.Detail.StartsWith($"Due {p.Detail.Split(" -> ")[1].Split(';')[0]} ->"))), "expedite found the postponed timeouts");
        Check(!history.Snapshot().Any(e => e.Event == "No pending timeout"), "no pending timeout was reported");
    });

    public static Task RepeatedStartDoesNotRecreateCanceledTimeout() => Run(c => c.AddSaga<LegacyOrderPolicy>(), async (send, history, wait) =>
    {
        await send(new PlaceOrder { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Placed"));
        await send(new CancelTimeouts { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Canceled"));

        await send(new PlaceOrder { OrderId = "o1" });
        await wait(() => history.Snapshot().Any(e => e.Event == "Already started"));
        await send(new PostponeBuyersRemorse { OrderId = "o1" });
        await wait(() => history.Snapshot().Count(e => e.Event == "No pending timeout") == 1);
        await send(new ExpediteBuyersRemorse { OrderId = "o1" });
        await wait(() => history.Snapshot().Count(e => e.Event == "No pending timeout") == 2);
        Check(history.Snapshot().Count(e => e.Event == "Placed") == 1, "repeated start did not request a new timeout");
    });

    static async Task Run(Action<EndpointConfiguration> addSaga, Func<Func<object, Task>, OrderHistory, Func<Func<bool>, Task>, Task> body)
    {
        var root = Path.Combine(Path.GetTempPath(), "saga-timeout-checks-" + Guid.NewGuid().ToString("N"));
        try
        {
            var builder = Host.CreateApplicationBuilder();
            builder.Logging.ClearProviders();
            var configuration = new EndpointConfiguration("SagaTimeoutChecks.Orders");
            configuration.UseTransport(new LearningTransport { StorageDirectory = Path.Combine(root, "transport") });
            configuration.UseSerialization<SystemJsonSerializer>();
            configuration.UsePersistence<LearningPersistence>().SagaStorageDirectory(Path.Combine(root, "sagas"));
            configuration.AssemblyScanner().Disable = true;
            addSaga(configuration);
            configuration.SendFailedMessagesTo("error");
            configuration.EnableInstallers();
            builder.Services.AddNServiceBusEndpoint(configuration);
            var history = new OrderHistory();
            builder.Services.AddSingleton(history);

            using var host = builder.Build();
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            await host.StartAsync(cancellation.Token);
            try
            {
                var session = host.Services.GetRequiredService<IMessageSession>();
                await body(
                    message => session.SendLocal(message, cancellation.Token),
                    history,
                    async done =>
                    {
                        var deadline = DateTime.UtcNow.AddSeconds(10);
                        while (!done() && DateTime.UtcNow < deadline)
                        {
                            await Task.Delay(50, cancellation.Token);
                        }

                        if (!done())
                        {
                            throw new TimeoutException("Condition was not met within 10 seconds");
                        }
                    });
            }
            finally
            {
                await host.StopAsync(CancellationToken.None);
            }
        }
        finally
        {
            try { Directory.Delete(root, true); } catch (IOException) { }
        }
    }

    static void Check(bool condition, string description)
    {
        if (!condition)
        {
            throw new InvalidOperationException($"FAILED: {description}");
        }
    }
}

public class RequestSecondTimeout
{
    public string OrderId { get; set; } = null!;
}

public class CancelTimeouts
{
    public string OrderId { get; set; } = null!;
}

// Test-only subclass reuses the real handlers and their singleton history to seed legacy data
#pragma warning disable NSB0014, CS9107
public class LegacyOrderPolicy(ILogger<OrderPolicy> logger, OrderHistory history) : OrderPolicy(logger, history), IHandleMessages<RequestSecondTimeout>, IHandleMessages<CancelTimeouts>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<OrderPolicyData> mapper) =>
        mapper.MapSaga(saga => saga.OrderId)
            .ToMessage<PlaceOrder>(message => message.OrderId)
            .ToMessage<PostponeBuyersRemorse>(message => message.OrderId)
            .ToMessage<ExpediteBuyersRemorse>(message => message.OrderId)
            .ToMessage<RequestSecondTimeout>(message => message.OrderId)
            .ToMessage<CancelTimeouts>(message => message.OrderId);

    public async Task Handle(RequestSecondTimeout message, IMessageHandlerContext context)
    {
        await RequestTimeout(context, TimeSpan.FromMinutes(5), new BuyersRemorseIsOver { OrderId = message.OrderId, Note = "Requested by the saga" });
        history.Add(message.OrderId, "Second timeout", "");
    }

    public Task Handle(CancelTimeouts message, IMessageHandlerContext context)
    {
        foreach (var timeout in FindTimeouts<BuyersRemorseIsOver>(_ => true).ToArray())
        {
            CancelTimeout(timeout.TimeoutId);
        }

        history.Add(message.OrderId, "Canceled", "");
        return Task.CompletedTask;
    }
}
#pragma warning restore NSB0014, CS9107
