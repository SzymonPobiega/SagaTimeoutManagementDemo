namespace OrderEndpoint;

using Microsoft.Extensions.Logging;
using NServiceBus.Sagas;
using OrderEndpoint.UI;

public class OrderPolicy(ILogger<OrderPolicy> logger, OrderHistory history) : Saga<OrderPolicyData>,
    IAmStartedByMessages<PlaceOrder>,
    IHandleMessages<PostponeBuyersRemorse>,
    IHandleMessages<ExpediteBuyersRemorse>,
    IHandleTimeouts<BuyersRemorseIsOver>
{
    protected override void ConfigureHowToFindSaga(SagaPropertyMapper<OrderPolicyData> mapper) =>
        mapper.MapSaga(saga => saga.OrderId)
            .ToMessage<PlaceOrder>(message => message.OrderId)
            .ToMessage<PostponeBuyersRemorse>(message => message.OrderId)
            .ToMessage<ExpediteBuyersRemorse>(message => message.OrderId);

    public async Task Handle(PlaceOrder message, IMessageHandlerContext context)
    {
        // Only a new saga requests the initial timeout, even if the existing one's timeouts were canceled
        if (context.Extensions.Get<ActiveSagaInstance>().IsNew)
        {
            await RequestTimeout(context, TimeSpan.FromMinutes(5), new BuyersRemorseIsOver { OrderId = message.OrderId, Note = "Requested by the saga" });
            history.Add(message.OrderId, "Placed", "Buyer's remorse timeout requested, due in 5 minutes");
            logger.LogInformation("Order {OrderId} placed. The buyer's remorse period ends in 5 minutes.", message.OrderId);
        }
        else
        {
            history.Add(message.OrderId, "Already started", "No new timeout requested");
            logger.LogInformation("Order {OrderId} is already started.", message.OrderId);
        }
    }

    public Task Handle(PostponeBuyersRemorse message, IMessageHandlerContext context) => Shift(message.OrderId, TimeSpan.FromMinutes(1));

    public Task Handle(ExpediteBuyersRemorse message, IMessageHandlerContext context) => Shift(message.OrderId, TimeSpan.FromMinutes(-1));

    public Task Timeout(BuyersRemorseIsOver state, IMessageHandlerContext context)
    {
        history.Add(state.OrderId, "Timeout completed", $"Saga completed; note: {state.Note}");
        logger.LogInformation("The buyer's remorse period for order {OrderId} is over ({Note}). Completing the saga.", state.OrderId, state.Note);
        MarkAsComplete();
        return Task.CompletedTask;
    }

    Task Shift(string orderId, TimeSpan shift)
    {
        var timeouts = FindTimeouts<BuyersRemorseIsOver>(_ => true).ToArray();
        foreach (var timeout in timeouts)
        {
            var newDueTime = timeout.DueTime + shift;
            // A payload change is stored only because the timeout is rescheduled below
            timeout.Payload.Note = shift > TimeSpan.Zero ? "Postponed by the saga" : "Expedited by the saga";
            RescheduleTimeout(timeout.TimeoutId, newDueTime);
            history.Add(orderId, shift > TimeSpan.Zero ? "Postponed" : "Expedited", $"Due {timeout.DueTime:u} -> {newDueTime:u}; note: {timeout.Payload.Note}");
            logger.LogInformation("Order {OrderId}: buyer's remorse timeout moved from {OldDueTime:u} to {NewDueTime:u}.", orderId, timeout.DueTime, newDueTime);
        }
        return Task.CompletedTask;
    }
}

public class OrderPolicyData : ContainSagaData
{
    public string OrderId { get; set; } = null!;
}
