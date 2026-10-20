namespace OrderEndpoint.UI;

public static class OrderRoutes
{
    // HTTP equivalents of the console commands, for hosts without stdin such as Aspire
    public static void MapOrderRoutes(this IEndpointRouteBuilder routes)
    {
        routes.MapGet("/", () => Results.Content(OrderPage.Html, "text/html"));
        routes.MapGet("/history", (OrderHistory history) => history.Snapshot());
        routes.MapPost("/orders/{orderId}", async (string orderId, IMessageSession session, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Results.BadRequest();
            }

            await session.SendLocal(new PlaceOrder { OrderId = orderId }, cancellationToken);
            return Results.Accepted();
        });
        routes.MapPost("/orders/{orderId}/postpone", async (string orderId, IMessageSession session, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Results.BadRequest();
            }

            await session.SendLocal(new PostponeBuyersRemorse { OrderId = orderId }, cancellationToken);
            return Results.Accepted();
        });
        routes.MapPost("/orders/{orderId}/expedite", async (string orderId, IMessageSession session, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(orderId))
            {
                return Results.BadRequest();
            }

            await session.SendLocal(new ExpediteBuyersRemorse { OrderId = orderId }, cancellationToken);
            return Results.Accepted();
        });
    }

    // Aspire injects ConnectionStrings:transport; the standalone key and localhost remain the fallback
    public static string GetTransportConnectionString(this IConfiguration configuration) =>
        configuration.GetConnectionString("transport") ?? configuration["RabbitMQ:ConnectionString"] ?? "host=localhost";
}
