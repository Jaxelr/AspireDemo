using BackendService;

var builder = Host.CreateApplicationBuilder(args);

// Bootstrap
builder.AddServiceDefaults();

builder.AddAzureServiceBusClient(connectionName: "local-bus");
builder.AddAzureCosmosClient(connectionName: "cosmos-db");
builder.AddAzureBlobClient(connectionName: "blobs");
builder.Services.AddSingleton<CosmosService>();
builder.Services.AddSingleton<BlobService>();

builder.Services.AddApplicationTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder
        .AddSource("worker")
        .AddSource("processMessage");
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
