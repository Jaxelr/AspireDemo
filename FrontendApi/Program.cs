using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using AspireDemo.ServiceDefaults.Models;
using Azure.Messaging.ServiceBus;
using FrontendApi;
using Microsoft.Azure.Cosmos;

var builder = WebApplication.CreateBuilder(args);

ActivitySource ActivitySource = new("FrontendApi");

// Bootstrapping

builder.AddServiceDefaults();

builder.Services.AddOpenApi();

builder.AddAzureServiceBusClient(connectionName: "local-bus");
builder.AddAzureCosmosClient(connectionName: "cosmos-db", configureClientOptions: clientoptions =>
{
    clientoptions.SerializerOptions = new()
    {
        PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
    };
});
builder.AddAzureBlobClient(connectionName: "blobs");

builder.Services.AddSingleton<CosmosService>();
builder.Services.AddSingleton<BlobService>();

builder.Services.AddApplicationTracing(tracerProviderBuilder =>
{
    tracerProviderBuilder
        .AddSource("submit");
});

var app = builder.Build();

app.UseHttpsRedirection();

// Api Endpoints

app.MapGet("/submit/{profileId}", async
    (
        string profileId,
        ILogger<Program> logger,
        ServiceBusClient serviceBusClient,
        CosmosService cosmos,
        CancellationToken token
    ) =>
{
    using Activity? activity = ActivitySource.StartActivity("submit", ActivityKind.Server);
    var sender = serviceBusClient.CreateSender("message");

    var profile = await cosmos.GetProfile(profileId);

    if (profile is null)
    {
        await cosmos.UpsertProfile(profileId, token);
    }

    var message = new Message()
    {
        Id = Guid.NewGuid(),
        ProfileId = profileId,
        Value = ToHexString(SHA256.HashData(GetRandomByteArray(32))),
        Status = Status.InProgress,
        Timestamp = DateTime.UtcNow
    };

    var serviceBusMessage = new ServiceBusMessage(SerializeToJson(message))
    {
        ContentType = "application/json"
    };

    if (activity is not null)
    {
        serviceBusMessage.ApplicationProperties.Add("Diagnostic-Id", activity.Id);
    }

    logger.LogInformation("Publishing message {MessageId} with trace {TraceId}", message.Id, activity?.TraceId);

    await sender.SendMessageAsync(serviceBusMessage);

    activity?
    .SetStatus(ActivityStatusCode.Ok)
    .SetEndTime(DateTime.UtcNow);

    return Results.Ok($"Request submitted successfully {message.Id}!");
});

// Status

app.MapGet("/status/{id}", async (string id, BlobService blob) =>
{
    using Activity? activity = ActivitySource.StartActivity("status", ActivityKind.Server);

    var result = await blob.GetStatus(id);

    if (result is null)
    {
        return Results.Ok("No status found.");
    }

    return Results.Ok($"Request {result.Id} status {result.Status} retrieved successfully {result.Value}!");
});

await app.RunAsync();

static byte[] GetRandomByteArray(int length)
{
    byte[] nonce = new byte[length];
    RandomNumberGenerator.Fill(nonce);

    return nonce;
}

// Local Functions

static string ToHexString(byte[] bytes)
{
    char[] hexDigits = { '0', '1', '2', '3', '4', '5', '6', '7',
                                    '8', '9', 'A', 'B', 'C', 'D', 'E', 'F'};

    if (bytes == null) return "";
    char[] chars = new char[bytes.Length * 2];
    int b, i;
    for (i = 0; i < bytes.Length; i++)
    {
        b = bytes[i];
        chars[i * 2] = hexDigits[b >> 4];
        chars[i * 2 + 1] = hexDigits[b & 0xF];
    }
    return new string(chars);
}

static string SerializeToJson<T>(T message) where T : class
{
    return JsonSerializer.Serialize(message, new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    });
}
