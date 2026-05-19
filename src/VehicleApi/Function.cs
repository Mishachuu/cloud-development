using Amazon.SQS;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using System.Text.Json.Serialization;
using VehicleApi.Services;

namespace VehicleApi;

public class Handler
{
    private readonly SqsPublisherService _publisher;
    private readonly ILogger<Handler> _logger;

    public Handler()
    {
        var config = new ConfigurationBuilder()
            .AddEnvironmentVariables()
            .Build();

        var services = new ServiceCollection();
        services.AddLogging(b => b.AddConsole());
        services.AddSingleton<IConfiguration>(config);

        var sqsConfig = new AmazonSQSConfig
        {
            ServiceURL = config["SQS_SERVICE_URL"]
                ?? "https://message-queue.api.cloud.yandex.net",
            AuthenticationRegion = "ru-central1"
        };
        var creds = new Amazon.Runtime.BasicAWSCredentials(
            config["SQS_ACCESS_KEY"] ?? "",
            config["SQS_SECRET_KEY"] ?? "");

        services.AddSingleton<IAmazonSQS>(_ =>
            new AmazonSQSClient(creds, sqsConfig));
        services.AddSingleton<SqsPublisherService>();

        var provider = services.BuildServiceProvider();
        _publisher = provider.GetRequiredService<SqsPublisherService>();
        _logger = provider.GetRequiredService<ILogger<Handler>>();
    }

    public async Task<Response> FunctionHandler(Request request)
    {
        var query = request.QueryStringParameters;

        if (query == null || !query.TryGetValue("id", out var idStr)
            || !int.TryParse(idStr, out var id))
        {
            return BadRequest("Missing or invalid 'id' parameter");
        }

        if (id <= 0)
            return BadRequest("ID must be greater than 0");

        var vehicle = VehicleGenerator.Generate(id);

        try
        {
            await _publisher.PublishAsync(vehicle);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to publish to SQS");
        }

        return Ok(JsonSerializer.Serialize(vehicle));
    }

    private static Response BadRequest(string message) => new()
    {
        StatusCode = 400,
        Body = message
    };

    private static Response Ok(string body) => new()
    {
        StatusCode = 200,
        Headers = new Dictionary<string, string>
        {
            ["Content-Type"] = "application/json",
            ["Access-Control-Allow-Origin"] = "*"
        },
        Body = body
    };
}

public class Request
{
    [JsonPropertyName("httpMethod")]
    public string? HttpMethod { get; set; }

    [JsonPropertyName("url")]
    public string? Url { get; set; }

    [JsonPropertyName("path")]
    public string? Path { get; set; }

    [JsonPropertyName("body")]
    public string? Body { get; set; }

    [JsonPropertyName("queryStringParameters")]
    public Dictionary<string, string>? QueryStringParameters { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("pathParameters")]
    public Dictionary<string, string>? PathParameters { get; set; }

    [JsonPropertyName("isBase64Encoded")]
    public bool IsBase64Encoded { get; set; }
}

public class Response
{
    [JsonPropertyName("statusCode")]
    public int StatusCode { get; set; }

    [JsonPropertyName("headers")]
    public Dictionary<string, string>? Headers { get; set; }

    [JsonPropertyName("body")]
    public string Body { get; set; } = "";
}