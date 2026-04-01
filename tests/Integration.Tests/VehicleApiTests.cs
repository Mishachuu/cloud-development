using System.Net;
using System.Text.Json;
using Amazon.SQS.Model;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Интеграционные тесты VehicleApi: HTTP-эндпоинт, кэш Redis и публикация в SQS.
/// </summary>
[Collection(nameof(IntegrationCollection))]
public class VehicleApiTests : IClassFixture<VehicleApiFactory>
{
    private readonly HttpClient _client;
    private readonly IntegrationFixture _fixture;

    public VehicleApiTests(VehicleApiFactory factory, IntegrationFixture fixture)
    {
        _client = factory.CreateClient();
        _fixture = fixture;
    }

    [Fact]
    public async Task GetVehicle_ValidId_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/vehicles?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicle_ValidId_ReturnsExpectedFields()
    {
        var response = await _client.GetAsync("/api/vehicles?id=5");
        var json = await response.Content.ReadAsStringAsync();
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(root.TryGetProperty("vin", out _), "Missing 'vin' field");
        Assert.True(root.TryGetProperty("manufacturer", out _), "Missing 'manufacturer' field");
        Assert.True(root.TryGetProperty("model", out _), "Missing 'model' field");
        Assert.True(root.TryGetProperty("year", out _), "Missing 'year' field");
        Assert.True(root.TryGetProperty("bodyType", out _), "Missing 'bodyType' field");
        Assert.True(root.TryGetProperty("fuelType", out _), "Missing 'fuelType' field");
        Assert.True(root.TryGetProperty("color", out _), "Missing 'color' field");
        Assert.True(root.TryGetProperty("mileage", out _), "Missing 'mileage' field");
        Assert.True(root.TryGetProperty("lastServiceDate", out _), "Missing 'lastServiceDate' field");
    }

    [Fact]
    public async Task GetVehicle_SameId_ReturnsSameData()
    {
        var r1 = await _client.GetAsync("/api/vehicles?id=42");
        var r2 = await _client.GetAsync("/api/vehicles?id=42");

        var json1 = await r1.Content.ReadAsStringAsync();
        var json2 = await r2.Content.ReadAsStringAsync();

        Assert.Equal(json1, json2);
    }

    [Fact]
    public async Task GetVehicle_DifferentIds_ReturnsDifferentData()
    {
        var r1 = await _client.GetAsync("/api/vehicles?id=10");
        var r2 = await _client.GetAsync("/api/vehicles?id=20");

        var json1 = await r1.Content.ReadAsStringAsync();
        var json2 = await r2.Content.ReadAsStringAsync();

        Assert.NotEqual(json1, json2);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetVehicle_InvalidId_ReturnsBadRequest(int id)
    {
        var response = await _client.GetAsync($"/api/vehicles?id={id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicle_OnCacheMiss_PublishesMessageToSqs()
    {
        var uniqueId = Random.Shared.Next(100_000, 999_999);

        await _client.GetAsync($"/api/vehicles?id={uniqueId}");

        await Task.Delay(300);

        var messages = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _fixture.SqsQueueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 2
        });

        var vehicleMessage = messages.Messages.FirstOrDefault(m =>
        {
            var doc = JsonDocument.Parse(m.Body);
            return doc.RootElement.TryGetProperty("id", out var idProp)
                   && idProp.GetInt32() == uniqueId;
        });

        Assert.NotNull(vehicleMessage);
    }

    [Fact]
    public async Task GetVehicle_OnCacheHit_DoesNotPublishDuplicateToSqs()
    {
        var uniqueId = Random.Shared.Next(200_000, 299_999);

        await _client.GetAsync($"/api/vehicles?id={uniqueId}");
        await Task.Delay(300);

        var firstBatch = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _fixture.SqsQueueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 2
        });

        foreach (var msg in firstBatch.Messages)
            await _fixture.SqsClient.DeleteMessageAsync(_fixture.SqsQueueUrl, msg.ReceiptHandle);

        await _client.GetAsync($"/api/vehicles?id={uniqueId}");
        await Task.Delay(300);

        var secondBatch = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _fixture.SqsQueueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 1
        });

        var duplicateMessage = secondBatch.Messages.FirstOrDefault(m =>
        {
            var doc = JsonDocument.Parse(m.Body);
            return doc.RootElement.TryGetProperty("id", out var idProp)
                   && idProp.GetInt32() == uniqueId;
        });

        Assert.Null(duplicateMessage);
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
