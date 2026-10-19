using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OrderEndpoint;

var builder = Host.CreateApplicationBuilder(args);

var endpointConfiguration = new EndpointConfiguration("SagaTimeoutDemo.Orders");
endpointConfiguration.UseTransport(new RabbitMQTransport(RoutingTopology.Conventional(QueueType.Quorum), builder.Configuration["RabbitMQ:ConnectionString"] ?? "host=localhost"));
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
builder.Services.AddHostedService<ConsoleCommands>();

await builder.Build().RunAsync();
