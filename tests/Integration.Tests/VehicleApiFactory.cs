using Amazon.SQS;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using VehicleApi.Services;

namespace Integration.Tests;

/// <summary>
/// Фабрика для поднятия VehicleApi в тестовом окружении.
/// Подменяет Redis, SQS и ServiceDiscovery на тестовые контейнеры.
/// </summary>
public class VehicleApiFactory(IntegrationFixture fixture)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.RemoveAll<IConnectionMultiplexer>();
            services.RemoveAll<IAmazonSQS>();
            services.RemoveAll<SqsPublisherService>();

            services.AddSingleton<IConnectionMultiplexer>(_ =>
                ConnectionMultiplexer.Connect(fixture.RedisConnectionString));

            services.AddStackExchangeRedisCache(opts =>
                opts.Configuration = fixture.RedisConnectionString);

            var sqsCreds = new Amazon.Runtime.BasicAWSCredentials("test", "test");
            var sqsConfig = new AmazonSQSConfig { ServiceURL = fixture.SqsServiceUrl };
            var sqsClient = new AmazonSQSClient(sqsCreds, sqsConfig);

            services.AddSingleton<IAmazonSQS>(_ => sqsClient);
            services.AddSingleton<SqsPublisherService>();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sqs:QueueUrl"] = fixture.SqsQueueUrl,
                ["Sqs:ServiceUrl"] = fixture.SqsServiceUrl,
                ["Sqs:AccessKey"] = "test",
                ["Sqs:SecretKey"] = "test",
                ["Cors:AllowedOrigins:0"] = "http://localhost"
            });
        });
    }
}
