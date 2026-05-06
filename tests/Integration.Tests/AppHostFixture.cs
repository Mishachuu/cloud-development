using Aspire.Hosting;
using Aspire.Hosting.Testing;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Поднимает весь Aspire AppHost один раз для всей коллекции тестов.
/// </summary>
public sealed class AppHostFixture : IAsyncLifetime
{
    public HttpClient GatewayClient { get; private set; } = null!;
    public HttpClient FileServiceClient { get; private set; } = null!;

    private DistributedApplication _app = null!;

    public async Task InitializeAsync()
    {
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.AppHost>();
        _app = await builder.BuildAsync();
        await _app.StartAsync();

        GatewayClient = _app.CreateHttpClient("apigateway", "gateway-endpoint");
        FileServiceClient = _app.CreateHttpClient("fileservice");

        using var cts = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        while (true)
        {
            try
            {
                var response = await GatewayClient.GetAsync("/api/vehicles?id=1", cts.Token);
                if ((int)response.StatusCode < 500)
                    break;
            }
            catch
            {
                // ещё не готов
            }
            await Task.Delay(2000, cts.Token);
        }
    }

    public async Task DisposeAsync()
    {
        GatewayClient?.Dispose();
        FileServiceClient?.Dispose();
        await _app.DisposeAsync();
    }
}
