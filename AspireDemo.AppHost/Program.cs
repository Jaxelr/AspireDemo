using Azure.Provisioning.ServiceBus;
using Azure.Provisioning.Storage;

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
var storage = builder.AddAzureStorage("storage");
var blobs = storage.AddBlobs("blobs");
storage.RunAsEmulator(em => em.WithLifetime(ContainerLifetime.Persistent));

builder.AddProject<Projects.FrontendApi>("frontend")
    .WithReference(cosmos)
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WithRoleAssignments(serviceBus, ServiceBusBuiltInRole.AzureServiceBusDataSender)
    .WithRoleAssignments(storage, StorageBuiltInRole.StorageBlobDataContributor);

builder.AddProject<Projects.BackendService>("backend")
    .WithReference(cosmos)
    .WithReference(blobs)
    .WithReference(serviceBus)
    .WithRoleAssignments(serviceBus, ServiceBusBuiltInRole.AzureServiceBusDataReceiver)
    .WithRoleAssignments(storage, StorageBuiltInRole.StorageBlobDataContributor);

builder.Build().Run();
