using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NServiceBus.Testing;
using OrderEndpoint;
using OrderEndpoint.UI;

var session = new TestableMessageSession();
var builder = WebApplication.CreateBuilder();
builder.WebHost.UseTestServer();
builder.Services.AddSingleton<IMessageSession>(session);
var routeHistory = new OrderHistory();
builder.Services.AddSingleton(routeHistory);
await using var app = builder.Build();
app.MapOrderRoutes();
await app.StartAsync();
var client = app.GetTestClient();

foreach (var route in new[] { "/orders/o1", "/orders/o1/postpone", "/orders/o1/expedite" })
{
    var response = await client.PostAsync(route, null);
    Check(response.StatusCode == System.Net.HttpStatusCode.Accepted, $"{route} returns 202");
}

foreach (var route in new[] { "/orders/%20", "/orders/%20/postpone", "/orders/%20/expedite" })
{
    var response = await client.PostAsync(route, null);
    Check(response.StatusCode == System.Net.HttpStatusCode.BadRequest, $"{route} returns 400");
}

Check(session.SentMessages.Length == 3, "three messages sent");
Check(session.SentMessages[0].Message is PlaceOrder { OrderId: "o1" }, "PlaceOrder sent");
Check(session.SentMessages[1].Message is PostponeBuyersRemorse { OrderId: "o1" }, "PostponeBuyersRemorse sent");
Check(session.SentMessages[2].Message is ExpediteBuyersRemorse { OrderId: "o1" }, "ExpediteBuyersRemorse sent");

Check(routeHistory.Snapshot().Length == 0, "queuing commands does not claim they were processed");

var page = await client.GetAsync("/");
Check(page.StatusCode == System.Net.HttpStatusCode.OK && page.Content.Headers.ContentType?.MediaType == "text/html", "root page is served as HTML");
var pageHtml = await page.Content.ReadAsStringAsync();
Check(pageHtml.Contains("Start order") && pageHtml.Contains("Postpone") && pageHtml.Contains("Expedite") && !pageHtml.Contains("innerHTML"), "root page has the three actions and no innerHTML");

routeHistory.Add("o<1>", "Placed", "detail");
var historyJson = System.Text.Json.JsonDocument.Parse(await client.GetStringAsync("/history")).RootElement[0];
Check(historyJson.GetProperty("orderId").GetString() == "o<1>" && historyJson.GetProperty("event").GetString() == "Placed", "history endpoint returns entries as JSON");

// History is bounded, newest first and safe under concurrent writers
var history = new OrderHistory();
Parallel.For(0, 500, i => history.Add($"c{i}", "Placed", ""));
Check(history.Snapshot().Length == OrderHistory.Capacity, "history is bounded");
history = new OrderHistory();
for (var i = 0; i < 150; i++) history.Add($"o{i}", "Placed", "");
var snapshot = history.Snapshot();
Check(snapshot[0].OrderId == "o149" && snapshot[^1].OrderId == "o50", "history is newest first and drops the oldest");
history.Add("next", "Placed", "");
Check(snapshot[0].OrderId == "o149", "snapshots are unaffected by later writes");

// The saga records what it actually handled
var policy = new OrderPolicy(NullLogger<OrderPolicy>.Instance, history = new OrderHistory()) { Data = new OrderPolicyData { OrderId = "s1" } };
await policy.Timeout(new BuyersRemorseIsOver { OrderId = "s1", Note = "n" }, new TestableMessageHandlerContext());
// Postpone, expedite and no-pending-timeout need a timeout-capable persister, which NServiceBus.Testing lacks
Check(history.Snapshot().Select(e => e.Event).SequenceEqual(["Timeout completed"]), "timeout handling emits history");
Check(history.Snapshot()[0].Detail.Contains("note: n"), "timeout history carries the note");

Check(Config(("ConnectionStrings:transport", "amqp://aspire"), ("RabbitMQ:ConnectionString", "host=other")) == "amqp://aspire", "Aspire connection string wins");
Check(Config(("RabbitMQ:ConnectionString", "host=other")) == "host=other", "standalone key is the fallback");
Check(Config() == "host=localhost", "localhost is the default");

await SagaTimeoutChecks.RepeatedStartIsIgnored();
await SagaTimeoutChecks.LegacyMultipleTimeoutsAreAllShifted();
await SagaTimeoutChecks.RepeatedStartDoesNotRecreateCanceledTimeout();

Console.WriteLine("All checks passed");

static string Config(params (string Key, string Value)[] values) =>
    new ConfigurationBuilder().AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value))).Build().GetTransportConnectionString();

static void Check(bool condition, string description)
{
    if (!condition)
    {
        Console.Error.WriteLine($"FAILED: {description}");
        Environment.Exit(1);
    }
}
