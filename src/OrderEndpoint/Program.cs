using OrderEndpoint;
using OrderEndpoint.UI;

var builder = WebApplication.CreateBuilder(args);

var endpointConfiguration = new EndpointConfiguration("SagaTimeoutDemo.Orders");
endpointConfiguration.UseTransport(new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), builder.Configuration.GetTransportConnectionString()));
endpointConfiguration.UseSerialization<SystemJsonSerializer>();
// The learning persister is the only one that implements ISupportTimeoutManagement so far
endpointConfiguration.UsePersistence<LearningPersistence>();
endpointConfiguration.AssemblyScanner().Disable = true;
endpointConfiguration.AddSaga<OrderPolicy>();
endpointConfiguration.SendFailedMessagesTo("error");
endpointConfiguration.ServiceControlErrorInstanceQueue("Particular.ServiceControl");
endpointConfiguration.ReportSagaTimeoutsToServiceControl();
endpointConfiguration.EnableInstallers();

builder.Services.AddNServiceBusEndpoint(endpointConfiguration);
builder.Services.AddSingleton<OrderHistory>();

var app = builder.Build();
app.MapOrderRoutes();
await app.RunAsync();
