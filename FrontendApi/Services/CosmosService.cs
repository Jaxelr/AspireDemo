using System.Diagnostics;
using AspireDemo.ServiceDefaults.Models;
using Bogus;
using Microsoft.Azure.Cosmos;

namespace FrontendApi;

public class CosmosService
{
    private readonly ActivitySource activitySource = new("CosmosService");

    private readonly Container container;

    public CosmosService(CosmosClient client)
    {

        var database = client.GetDatabase("sample");
        container = database.GetContainer("profile");
    }

    public async Task UpsertProfile(Profile profile, CancellationToken token) => await container.UpsertItemAsync(profile, cancellationToken: token);


    public async Task<Profile?> GetProfile(string profileId)
    {
        var query = new QueryDefinition("SELECT * FROM c WHERE c.id = @profileId").WithParameter("@profileId", profileId);

        using var iterator = container.GetItemQueryIterator<Profile>(query);

        while (iterator.HasMoreResults)
        {
            foreach (var item in await iterator.ReadNextAsync())
            {
                return item;
            }
        }

        return null;
    }

    public async Task UpsertProfile(string profileId, CancellationToken token)
    {
        using var activity = activitySource.StartActivity("upsertProfile", ActivityKind.Internal);

        var faker = new Faker();
        string name = faker.Name.FullName();
        await UpsertProfile(new Profile() { Id = profileId, Name = name }, token);
        activity?.SetStatus(ActivityStatusCode.Ok);
    }
}
