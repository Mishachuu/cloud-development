using System.Text.Json;
using System.Text.Json.Serialization;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace FileService;

public class Handler
{
    private readonly ILogger<Handler> _logger;
    private readonly AmazonS3Client _s3;
    private readonly string _bucketName;

    public Handler()
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        var provider = services.BuildServiceProvider();
        _logger = provider.GetRequiredService<ILogger<Handler>>();

        _bucketName = config["S3_BUCKET"] ?? "vehicles-storage";

        var s3Config = new AmazonS3Config
        {
            ServiceURL = "https://storage.yandexcloud.net",
            ForcePathStyle = true
        };
        var creds = new Amazon.Runtime.BasicAWSCredentials(
            config["S3_ACCESS_KEY"] ?? "",
            config["S3_SECRET_KEY"] ?? "");

        _s3 = new AmazonS3Client(creds, s3Config);
    }

    public async Task FunctionHandler(QueueRequest request)
    {
        _logger.LogInformation("Got {Count} messages", request.Messages.Count);

        foreach (var evt in request.Messages)
        {
            var message = evt.Details?.Message;
            if (message == null)
            {
                _logger.LogWarning("Message is null, skipping");
                continue;
            }

            _logger.LogInformation("MessageId: {Id}, Body: {Body}",
                message.MessageId, message.Body);

            try
            {
                var body = message.Body;
                var shortId = message.MessageId.Length >= 8
                    ? message.MessageId[..8]
                    : message.MessageId;
                var objectName = $"vehicle-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss-fff}-{shortId}.json";

                using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(body));
                await _s3.PutObjectAsync(new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = objectName,
                    InputStream = stream,
                    ContentType = "application/json"
                });

                _logger.LogInformation("Saved {ObjectName}", objectName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process message {MessageId}", message.MessageId);
            }
        }
    }
}

public class QueueRequest
{
    [JsonPropertyName("messages")]
    public List<QueueEvent> Messages { get; set; } = new();
}

public class QueueEvent
{
    [JsonPropertyName("details")]
    public QueueEventDetails? Details { get; set; }
}

public class QueueEventDetails
{
    [JsonPropertyName("message")]
    public QueueMessage? Message { get; set; }
}

public class QueueMessage
{
    [JsonPropertyName("message_id")]
    public string MessageId { get; set; } = "";

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";
}