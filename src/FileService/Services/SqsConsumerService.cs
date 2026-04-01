using Amazon.SQS;
using Amazon.SQS.Model;

namespace FileService.Services;

/// <summary>
/// Фоновый сервис, непрерывно опрашивающий очередь SQS и сохраняющий
/// полученные данные транспортных средств в объектное хранилище.
/// </summary>
public class SqsConsumerService(
    IAmazonSQS sqs,
    MinioStorageService storage,
    IConfiguration config,
    ILogger<SqsConsumerService> logger) : BackgroundService
{
    private readonly string _queueUrl = config["Sqs:QueueUrl"]
        ?? throw new InvalidOperationException("Sqs:QueueUrl is not configured");

    private readonly int _pollingIntervalMs = config.GetValue<int>("Sqs:PollingIntervalMs", 1000);
    private readonly int _maxMessages = config.GetValue<int>("Sqs:MaxMessages", 10);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("SQS consumer started. Queue: {QueueUrl}", _queueUrl);

        await EnsureQueueExistsAsync();

        await storage.EnsureBucketExistsAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await PollAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Error while polling SQS queue");
                await Task.Delay(_pollingIntervalMs, stoppingToken);
            }
        }

        logger.LogInformation("SQS consumer stopped");
    }

    private async Task PollAsync(CancellationToken ct)
    {
        var request = new ReceiveMessageRequest
        {
            QueueUrl = _queueUrl,
            MaxNumberOfMessages = _maxMessages,
            WaitTimeSeconds = 5
        };

        var response = await sqs.ReceiveMessageAsync(request, ct);

        if (response.Messages.Count == 0)
        {
            await Task.Delay(_pollingIntervalMs, ct);
            return;
        }

        logger.LogInformation("Received {Count} messages from SQS", response.Messages.Count);

        foreach (var message in response.Messages)
        {
            await ProcessMessageAsync(message, ct);
        }
    }

    private async Task ProcessMessageAsync(Message message, CancellationToken ct)
    {
        try
        {
            var objectName = $"vehicle-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{message.MessageId[..8]}.json";

            await storage.SaveAsync(objectName, message.Body);

            await sqs.DeleteMessageAsync(_queueUrl, message.ReceiptHandle, ct);

            logger.LogInformation("Processed message {MessageId} → saved as '{ObjectName}'",
                message.MessageId, objectName);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
        }
    }
    private async Task EnsureQueueExistsAsync()
{
    try
    {
        await sqs.CreateQueueAsync(new Amazon.SQS.Model.CreateQueueRequest
        {
            QueueName = _queueUrl.Split('/').Last()
        });
        logger.LogInformation("SQS queue ensured");
    }
    catch (Exception ex)
    {
        logger.LogWarning("Could not create queue: {Message}", ex.Message);
    }
}
}
