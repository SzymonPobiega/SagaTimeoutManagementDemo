namespace OrderEndpoint;

using Microsoft.Extensions.Logging;

public class OrderPolicy(ILogger<OrderPolicy> logger) : Saga<OrderPolicyData>,
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
        await RequestTimeout(context, TimeSpan.FromMinutes(5), new BuyersRemorseIsOver { OrderId = message.OrderId, Note = "Requested by the saga" });
        logger.LogInformation("Order {OrderId} placed. The buyer's remorse period ends in 5 minutes.", message.OrderId);
    }

    public Task Handle(PostponeBuyersRemorse message, IMessageHandlerContext context) => Shift(message.OrderId, TimeSpan.FromMinutes(1));

    public Task Handle(ExpediteBuyersRemorse message, IMessageHandlerContext context) => Shift(message.OrderId, TimeSpan.FromMinutes(-1));

    public Task Timeout(BuyersRemorseIsOver state, IMessageHandlerContext context)
    {
        logger.LogInformation("The buyer's remorse period for order {OrderId} is over ({Note}). Completing the saga.", state.OrderId, state.Note);
        MarkAsComplete();
        return Task.CompletedTask;
    }

    Task Shift(string orderId, TimeSpan shift)
    {
        var timeout = FindTimeouts<BuyersRemorseIsOver>(_ => true).SingleOrDefault();
        if (timeout is null)
        {
            logger.LogWarning("Order {OrderId} has no pending buyer's remorse timeout.", orderId);
            return Task.CompletedTask;
        }

        var newDueTime = timeout.DueTime + shift;
        // A payload change is stored only because the timeout is rescheduled below
        timeout.Payload.Note = shift > TimeSpan.Zero ? "Postponed by the saga" : "Expedited by the saga";
        RescheduleTimeout(timeout.TimeoutId, newDueTime);
        logger.LogInformation("Order {OrderId}: buyer's remorse timeout moved from {OldDueTime:u} to {NewDueTime:u}.", orderId, timeout.DueTime, newDueTime);
        return Task.CompletedTask;
    }
}

public class OrderPolicyData : ContainSagaData
{
    public string OrderId { get; set; } = null!;
}
