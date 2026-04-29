using System.Text.Json;
using Amazon.SQS;
using Amazon.SQS.Model;
using VehicleApi.Models;

namespace VehicleApi.Services;

/// <summary>
/// Сервис публикации данных транспортного средства в очередь SQS.
/// </summary>
public class SqsPublisherService(IAmazonSQS sqs, IConfiguration config, ILogger<SqsPublisherService> logger)
{
    private readonly string _queueUrl = config["Sqs:QueueUrl"]
        ?? throw new InvalidOperationException("Sqs:QueueUrl is not configured");

    /// <summary>
    /// Публикует данные транспортного средства в SQS-очередь.
    /// </summary>
    public async Task PublishAsync(Vehicle vehicle)
    {
        var body = JsonSerializer.Serialize(vehicle);

        var request = new SendMessageRequest
        {
            QueueUrl = _queueUrl,
            MessageBody = body
        };

        var response = await sqs.SendMessageAsync(request);

        logger.LogInformation("Published vehicle {Id} to SQS, MessageId: {MessageId}",
            vehicle.Id, response.MessageId);
    }
}
