using Azure.Provisioning.ServiceBus;

var builder = DistributedApplication.CreateBuilder(args);

// Cosmos
var cosmos = builder.AddAzureCosmosDB("cosmos-db");
cosmos.RunAsEmulator(em => em.WithLifetime(ContainerLifetime.Persistent));

var database = cosmos.AddCosmosDatabase("sample");
database.AddContainer("profile", partitionKeyPath: "/Id");

// SB
var serviceBus = builder.AddAzureServiceBus("local-bus");
serviceBus.AddServiceBusQueue("message");
serviceBus.RunAsEmulator(em => em.WithLifetime(ContainerLifetime.Persistent));

// Storage
var blobs = builder.AddAzureStorage("storage")
                   .RunAsEmulator(em => em.WithLifetime(ContainerLifetime.Persistent))
                   .AddBlobs("blobs");

builder.AddProject<Projects.FrontendApi>("frontend")
    .WithReference(cosmos)
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WithRoleAssignments(serviceBus, ServiceBusBuiltInRole.AzureServiceBusDataSender);

builder.AddProject<Projects.BackendService>("backend")
    .WithReference(cosmos)
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WithRoleAssignments(serviceBus, ServiceBusBuiltInRole.AzureServiceBusDataReceiver);

builder.Build().Run();
