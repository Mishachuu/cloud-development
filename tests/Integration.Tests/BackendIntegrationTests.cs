using System.Net;
using System.Text.Json;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Интеграционные тесты всего бэкенда через Aspire AppHost.
/// Проверяют: gateway, кэш Redis, SQS-пайплайн и сохранение файлов в MinIO.
/// </summary>
public sealed class BackendIntegrationTests(AppHostFixture fixture) : IClassFixture<AppHostFixture>
{
    private readonly HttpClient _gatewayClient = fixture.GatewayClient;
    private readonly HttpClient _fileServiceClient = fixture.FileServiceClient;

    [Fact]
    public async Task GetVehicle_ValidId_ReturnsOk()
    {
        var response = await _gatewayClient.GetAsync("/api/vehicles?id=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicle_ValidId_ReturnsExpectedFields()
    {
        var response = await _gatewayClient.GetAsync("/api/vehicles?id=5");
        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.True(root.TryGetProperty("id", out _), "Missing 'id' field");
        Assert.True(root.TryGetProperty("vin", out _), "Missing 'vin' field");
        Assert.True(root.TryGetProperty("manufacturer", out _), "Missing 'manufacturer' field");
        Assert.True(root.TryGetProperty("model", out _), "Missing 'model' field");
        Assert.True(root.TryGetProperty("year", out _), "Missing 'year' field");
    }

    [Fact]
    public async Task GetVehicle_InvalidId_ReturnsBadRequest()
    {
        var response = await _gatewayClient.GetAsync("/api/vehicles?id=0");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-100)]
    public async Task GetVehicle_NegativeId_ReturnsBadRequest(int id)
    {
        var response = await _gatewayClient.GetAsync($"/api/vehicles?id={id}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task GetVehicle_SameId_ReturnsCachedData()
    {
        var r1 = await _gatewayClient.GetAsync("/api/vehicles?id=42");
        var r2 = await _gatewayClient.GetAsync("/api/vehicles?id=42");

        r1.EnsureSuccessStatusCode();
        r2.EnsureSuccessStatusCode();

        var json1 = await r1.Content.ReadAsStringAsync();
        var json2 = await r2.Content.ReadAsStringAsync();

        Assert.Equal(json1, json2);
    }

    [Fact]
    public async Task GetVehicle_DifferentIds_ReturnDifferentData()
    {
        var r1 = await _gatewayClient.GetAsync("/api/vehicles?id=10");
        var r2 = await _gatewayClient.GetAsync("/api/vehicles?id=20");

        r1.EnsureSuccessStatusCode();
        r2.EnsureSuccessStatusCode();

        using var doc1 = JsonDocument.Parse(await r1.Content.ReadAsStringAsync());
        using var doc2 = JsonDocument.Parse(await r2.Content.ReadAsStringAsync());

        Assert.Equal(10, doc1.RootElement.GetProperty("id").GetInt32());
        Assert.Equal(20, doc2.RootElement.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task GetVehicle_FileAppearsInMinio()
    {
        var id = Random.Shared.Next(50_000, 99_999);
        var deadline = DateTime.UtcNow.AddSeconds(30);

        await _gatewayClient.GetAsync($"/api/vehicles?id={id}");

        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(2000);

            var filesResponse = await _fileServiceClient.GetAsync("/files");
            if (!filesResponse.IsSuccessStatusCode) continue;

            var files = JsonSerializer.Deserialize<List<string>>(
                await filesResponse.Content.ReadAsStringAsync()) ?? [];

            if (files.Any(f => f.Contains(id.ToString())))
                return;
        }

        Assert.Fail($"Файл для vehicle id={id} не появился в MinIO за 30 секунд");
    }

    [Fact]
    public async Task FileService_HealthCheck_ReturnsOk()
    {
        var response = await _fileServiceClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Gateway_HealthCheck_ReturnsOk()
    {
        var response = await _gatewayClient.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
