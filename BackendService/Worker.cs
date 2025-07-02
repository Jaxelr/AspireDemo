using System.Diagnostics;
using System.Text.Json;
using Azure.Messaging.ServiceBus;
using AspireDemo.ServiceDefaults.Models;

namespace BackendService
{
    public class Worker : BackgroundService
    {
        private static readonly ActivitySource ActivitySource = new(nameof(Worker));
        private readonly ServiceBusClient serviceBusClient;
        private readonly ILogger<Worker> logger;
        private ServiceBusProcessor? processor;
        private readonly CosmosService cosmos;
        private readonly BlobService blob;
        private readonly JsonSerializerOptions jsonserializer = new() { PropertyNameCaseInsensitive = true };

        public Worker(ServiceBusClient serviceBusClient, ILogger<Worker> logger, CosmosService cosmos, BlobService blob)
        {
            this.logger = logger;
            this.serviceBusClient = serviceBusClient;
            this.cosmos = cosmos;
            this.blob = blob;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            ActivitySource.StartActivity("worker", ActivityKind.Server);

            processor = serviceBusClient.CreateProcessor("message");

            processor.ProcessMessageAsync += ProcessMessageAsync;
            processor.ProcessErrorAsync += ProcessErrorAsync;

            await processor.StartProcessingAsync(stoppingToken);

            logger.LogInformation("Worker service has started");

            try
            {
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                // Expected when the service is stopping
            }
            finally
            {
                if (processor is not null)
                {
                    await processor.StopProcessingAsync(CancellationToken.None);
                }
            }
        }

        private async Task ProcessMessageAsync(ProcessMessageEventArgs args)
        {
            // Extract diagnostic context from message
            string messageBody = args.Message.Body.ToString();

            // Create activity to track processing
            using var activity = ActivitySource.StartActivity("processMessage", ActivityKind.Server);

            // Extract and link to parent trace if available
            if (args.Message.ApplicationProperties.TryGetValue("Diagnostic-Id", out object? diagnosticId) &&
                diagnosticId is string traceparent)
            {
                activity?.SetParentId(traceparent);
            }

            try
            {
                var message = JsonSerializer.Deserialize<Message>(messageBody, jsonserializer);

                if (message is not null)
                {
                    // Add message details to activity
                    activity?.AddTag("message.id", message.Id);
                    activity?.AddTag("message.sent", message.Timestamp.ToString("o"));

                    var profile = await cosmos.GetProfile(message.ProfileId);

                    if (profile is null)
                    {
                        // Failed
                        message.Status = Status.Failed;
                        logger.LogError("{profile} cant be found {status}, at {sentOn}",
                            message.ProfileId,
                            message.Status,
                            message.Timestamp);
                    }
                    else
                    {
                        //Haz Success
                        message.Status = Status.Success;

                        await blob.Store(message.Id.ToString(), new()
                        {
                            Id = message.Id,
                            Status = message.Status,
                            Value = message.Value
                        });

                        logger.LogInformation(
                            "Processed message {messageId} sent at {SentOn} with trace {TraceId}",
                            message.Id,
                            message.Timestamp,
                            activity?.TraceId);
                    }
                }
                else
                {
                    logger.LogWarning("Received empty or invalid message");
                }

                // Complete the message to remove it from the queue
                await args.CompleteMessageAsync(args.Message);
            }
            catch (Exception ex)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                logger.LogError(ex, "Error processing message");

                // Abandon the message to make it available for reprocessing
                await args.AbandonMessageAsync(args.Message);
            }
        }

        private Task ProcessErrorAsync(ProcessErrorEventArgs args)
        {
            logger.LogError(args.Exception, "Error processing Service Bus message: {ErrorSource}", args.ErrorSource);
            return Task.CompletedTask;
        }
    }
}
