using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using VehicleApi.Models;

namespace VehicleApi.Services;

public class SqsPublisherService(IAmazonSQS sqs, IConfiguration config, ILogger<SqsPublisherService> logger)
{
    private readonly string _queueUrl = config["SQS_QUEUE_URL"]
        ?? throw new InvalidOperationException("SQS_QUEUE_URL is not configured");

    public async Task PublishAsync(Vehicle vehicle)
    {
        var body = JsonSerializer.Serialize(vehicle);
        var response = await sqs.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = body
        });
        logger.LogInformation("Published vehicle {Id}, MessageId: {MessageId}",
            vehicle.Id, response.MessageId);
    }
}