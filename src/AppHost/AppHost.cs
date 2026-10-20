using Particular.Aspire.Hosting.ServicePlatform.Platform;
using Particular.Aspire.Hosting.ServicePlatform.Transport;

var builder = DistributedApplication.CreateBuilder(args);

var rabbitMq = builder.AddRabbitMQ("transport",
        builder.AddParameter("transport-user", "guest"),
        builder.AddParameter("transport-password", "guest")
    )
    .WithManagementPlugin(port: 15672);

var platform = builder.AddParticularPlatform("particular")
    .WithTransportRabbitMQ(RabbitMqRouting.QuorumConventionalRouting, rabbitMq);
var raven = platform.AddPersistenceRavenDb("raven");

// Timeout management is only in the PR builds, so only the error instance and ServicePulse use them
var serviceControl = platform.AddServiceControlErrorInstance("servicecontrol", raven)
    .WithImageRegistry("ghcr.io")
    .WithImageTag("pr-6025")
    .WithErrorQueueName("error")
    .WithRunMode(PlatformRunMode.SetupAndRun);

platform.AddServicePulse("servicepulse", serviceControl)
    .WithImageRegistry("ghcr.io")
    .WithImageTag("pr-3166");

builder.AddProject<Projects.OrderEndpoint>("orderendpoint")
    .WithParticularPlatform(platform)
    .WaitFor(rabbitMq)
    .WaitFor(serviceControl);

builder.Build().Run();