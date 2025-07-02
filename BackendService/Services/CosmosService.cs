using System.Diagnostics;
using AspireDemo.ServiceDefaults.Models;
using Microsoft.Azure.Cosmos;

namespace BackendService;

public class CosmosService
{
    private readonly ActivitySource activitySource = new("CosmosService");

    private readonly Container container;

    public CosmosService(CosmosClient client)
    {
        var database = client.GetDatabase("sample");
        container = database.GetContainer("profile");
    }

    public async Task<Profile?> GetProfile(string profileId)
    {
        using var activity = activitySource.StartActivity("getProfile", ActivityKind.Internal);
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @profileId").WithParameter("@profileId", profileId);

        using var iterator = container.GetItemQueryIterator<Profile>(query);

        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                activity?.SetStatus(ActivityStatusCode.Ok)
                .SetBaggage("profile", item?.Id);
                return item;
            }
        }

        return null;
    }
}
