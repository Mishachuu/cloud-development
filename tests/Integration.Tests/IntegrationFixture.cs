using Amazon.SQS;
using Amazon.SQS.Model;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Minio;
using Minio.DataModel.Args;
using Testcontainers.Minio;
using Testcontainers.Redis;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Общая тестовая фикстура: поднимает Redis, LocalStack (SQS) и MinIO
/// один раз для всей коллекции интеграционных тестов.
/// </summary>
public class IntegrationFixture : IAsyncLifetime
{
    public const string QueueName = "vehicles";
    public const string BucketName = "vehicles";

    public string MinioAccessKey { get; private set; } = null!;
    public string MinioSecretKey { get; private set; } = null!;

    private readonly RedisContainer _redis = new RedisBuilder()
        .WithImage("redis:7-alpine")
        .Build();

    private readonly IContainer _localstack = new ContainerBuilder()
        .WithImage("localstack/localstack:latest")
        .WithEnvironment("SERVICES", "sqs")
        .WithEnvironment("DEFAULT_REGION", "us-east-1")
        .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
        .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
        .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
        .WithPortBinding(0, 4566)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r =>
            r.ForPort(4566).ForPath("/_localstack/health")))
        .Build();

    private readonly MinioContainer _minio = new MinioBuilder()
        .WithImage("minio/minio:latest")
        .Build();

    public IAmazonSQS SqsClient { get; private set; } = null!;
    public IMinioClient MinioClient { get; private set; } = null!;
    public string RedisConnectionString { get; private set; } = null!;
    public string SqsServiceUrl { get; private set; } = null!;
    public string SqsQueueUrl { get; private set; } = null!;
    public string MinioEndpoint { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Task.WhenAll(
            _redis.StartAsync(),
            _localstack.StartAsync(),
            _minio.StartAsync());

        RedisConnectionString = _redis.GetConnectionString();

        SqsServiceUrl = $"http://localhost:{_localstack.GetMappedPublicPort(4566)}";

        var sqsCreds = new Amazon.Runtime.BasicAWSCredentials("test", "test");
        var sqsConfig = new AmazonSQSConfig { ServiceURL = SqsServiceUrl };
        SqsClient = new AmazonSQSClient(sqsCreds, sqsConfig);

        var createQueueResponse = await SqsClient.CreateQueueAsync(new CreateQueueRequest
        {
            QueueName = QueueName
        });
        SqsQueueUrl = createQueueResponse.QueueUrl;

        MinioEndpoint = $"{_minio.Hostname}:{_minio.GetMappedPublicPort(9000)}";
        MinioAccessKey = _minio.GetAccessKey();
        MinioSecretKey = _minio.GetSecretKey();

        MinioClient = new MinioClient()
            .WithEndpoint(MinioEndpoint)
            .WithCredentials(MinioAccessKey, MinioSecretKey)
            .WithSSL(false)
            .Build();

        await MinioClient.MakeBucketAsync(new MakeBucketArgs().WithBucket(BucketName));
    }

    public async Task DisposeAsync()
    {
        SqsClient.Dispose();

        await Task.WhenAll(
            _redis.StopAsync(),
            _localstack.StopAsync(),
            _minio.StopAsync());
    }
}

/// <summary>
/// Коллекция, позволяющая переиспользовать одну фикстуру между тест-классами.
/// </summary>
[CollectionDefinition(nameof(IntegrationCollection))]
public class IntegrationCollection : ICollectionFixture<IntegrationFixture>;
