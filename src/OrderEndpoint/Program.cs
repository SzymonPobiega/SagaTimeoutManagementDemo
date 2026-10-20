using Microsoft.Data.SqlClient;
using NServiceBus.Persistence.Sql;
using OrderEndpoint;
using OrderEndpoint.UI;

[assembly: SqlPersistenceSettings(MsSqlServerScripts = true)]

var builder = WebApplication.CreateBuilder(args);

var endpointConfiguration = new EndpointConfiguration("SagaTimeoutDemo.Orders");
endpointConfiguration.UseTransport(new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), builder.Configuration.GetTransportConnectionString()));
endpointConfiguration.UseSerialization<SystemJsonSerializer>();
// The SQL persistence prerelease stores saga timeouts in the saga's Metadata column
var sagaConnectionString = builder.Configuration.GetSagaConnectionString();
var persistence = endpointConfiguration.UsePersistence<SqlPersistence>();
persistence.SqlDialect<SqlDialect.MsSqlServer>();
persistence.ConnectionBuilder(() => new SqlConnection(sagaConnectionString));
endpointConfiguration.AssemblyScanner().Disable = true;
endpointConfiguration.AddSaga<OrderPolicy>();
endpointConfiguration.SendFailedMessagesTo("error");
endpointConfiguration.ServiceControlErrorInstanceQueue("Particular.ServiceControl");

// #######
//  - Opt in to timeout management in service control
endpointConfiguration.ReportSagaTimeoutsToServiceControl();

endpointConfiguration.EnableInstallers();

builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
builder.Services.AddSingleton<OrderHistory>();

var app = builder.Build();
app.MapOrderRoutes();
await app.RunAsync();
