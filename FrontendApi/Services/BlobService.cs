using System.Text.Json;
using AspireDemo.ServiceDefaults.Models;
using Azure.Storage.Blobs;

namespace FrontendApi;

public class BlobService
{
    private readonly BlobContainerClient containerClient;
    public BlobService(BlobServiceClient blobClient, ILogger<BlobService> logger)
    {
        BlobContainerClient containerClient = blobClient.GetBlobContainerClient("result");
        containerClient.CreateIfNotExistsAsync();

        this.containerClient = containerClient;
    }

    public async Task<Result?> GetStatus(string id)
    {
        try
        {
            var blobClient = containerClient.GetBlobClient(id);
            var blob = (await blobClient.DownloadAsync()).Value;
            return JsonSerializer.Deserialize<Result>(new StreamReader(blob.Content).ReadToEnd());
        }
        catch
        {
            //Do nothing for now
        }

        return null;
    }
}
