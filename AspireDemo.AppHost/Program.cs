var builder = DistributedApplication.CreateBuilder(args);

var cosmos = builder.AddAzureCosmosDB("cosmos-db")
 .RunAsEmulator(emulator => emulator.WithDataVolume());

builder.Build().Run();
