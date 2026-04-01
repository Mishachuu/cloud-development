using Amazon.SQS;
using FileService.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Minio;

namespace Integration.Tests;

/// <summary>
/// Фабрика для поднятия FileService в тестовом окружении.
/// Подменяет MinIO и SQS на тестовые контейнеры.
/// </summary>
public class FileServiceFactory(IntegrationFixture fixture)
    : WebApplicationFactory<global::Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IAmazonSQS>();
            services.RemoveAll<IMinioClient>();
            services.RemoveAll<MinioStorageService>();
            services.RemoveAll<SqsConsumerService>();

            var sqsCreds = new Amazon.Runtime.BasicAWSCredentials("test", "test");
            var sqsConfig = new AmazonSQSConfig { ServiceURL = fixture.SqsServiceUrl };
            services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(sqsCreds, sqsConfig));

            services.AddSingleton<IMinioClient>(_ =>
                new MinioClient()
                    .WithEndpoint(fixture.MinioEndpoint)
                    .WithCredentials(fixture.MinioAccessKey, fixture.MinioSecretKey)
                    .WithSSL(false)
                    .Build());

            services.AddSingleton<MinioStorageService>();
            services.AddHostedService<SqsConsumerService>();
        });

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Sqs:QueueUrl"] = fixture.SqsQueueUrl,
                ["Sqs:ServiceUrl"] = fixture.SqsServiceUrl,
                ["Sqs:AccessKey"] = "test",
                ["Sqs:SecretKey"] = "test",
                ["Sqs:PollingIntervalMs"] = "200",
                ["Minio:Endpoint"] = fixture.MinioEndpoint,
                ["Minio:AccessKey"] = "minioadmin",
                ["Minio:SecretKey"] = "minioadmin",
                ["Minio:BucketName"] = IntegrationFixture.BucketName,
                ["Minio:UseSSL"] = "false"
            });
        });
    }
}
