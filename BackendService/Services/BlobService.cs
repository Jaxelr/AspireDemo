using System.Diagnostics;
using AspireDemo.ServiceDefaults.Models;
using Azure.Storage.Blobs;

namespace BackendService;

public class BlobService
{
    private readonly ActivitySource activitySource = new("BlobService");
    private readonly ILogger<BlobService> logger;
    private readonly BlobContainerClient containerClient;

    public BlobService(BlobServiceClient blobClient, ILogger<BlobService> logger)
    {
        BlobContainerClient containerClient = blobClient.GetBlobContainerClient("result");
        containerClient.CreateIfNotExistsAsync();

        this.containerClient = containerClient;
        this.logger = logger;
    }

    public async Task Store(string id, Result result)
    {
        using var activity = activitySource.StartActivity("storeResult", ActivityKind.Server);

        try
        {
            BlobClient blobClient = containerClient.GetBlobClient(id);

            await blobClient.UploadAsync(BinaryData.FromObjectAsJson(result), overwrite: true);

            activity?.SetStatus(ActivityStatusCode.Ok)
                .SetBaggage("operationId", id);
        }
        catch (Exception ex)
        {
            logger.LogError("Error uploading {Id}, {ex}", id, ex);
        }
    }
}
