using Amazon.SQS;
using FileService.Services;
using Minio;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// MinIO
var minioEndpoint = builder.Configuration["Minio:Endpoint"] ?? "localhost:9000";
var minioAccessKey = builder.Configuration["Minio:AccessKey"] ?? "minioadmin";
var minioSecretKey = builder.Configuration["Minio:SecretKey"] ?? "minioadmin";
var minioUseSSL = builder.Configuration.GetValue<bool>("Minio:UseSSL", false);

builder.Services.AddSingleton<IMinioClient>(_ =>
    new MinioClient()
        .WithEndpoint(minioEndpoint)
        .WithCredentials(minioAccessKey, minioSecretKey)
        .WithSSL(minioUseSSL)
        .Build());

// AWS SQS
var sqsConfig = new AmazonSQSConfig
{
    ServiceURL = builder.Configuration["Sqs:ServiceUrl"] ?? "http://localhost:4566"
};

var sqsCredentials = new Amazon.Runtime.BasicAWSCredentials(
    builder.Configuration["Sqs:AccessKey"] ?? "test",
    builder.Configuration["Sqs:SecretKey"] ?? "test");

builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(sqsCredentials, sqsConfig));

builder.Services.AddSingleton<MinioStorageService>();
builder.Services.AddHostedService<SqsConsumerService>();

var app = builder.Build();

app.MapDefaultEndpoints();

app.Run();
