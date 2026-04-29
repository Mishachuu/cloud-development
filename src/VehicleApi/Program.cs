using Amazon.SQS;
using Microsoft.AspNetCore.Mvc;
using VehicleApi.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

// AWS SQS
var sqsConfig = new AmazonSQSConfig
{
    ServiceURL = builder.Configuration["Sqs:ServiceUrl"] ?? "http://localhost:4566"
};

var sqsCredentials = new Amazon.Runtime.BasicAWSCredentials(
    builder.Configuration["Sqs:AccessKey"] ?? "test",
    builder.Configuration["Sqs:SecretKey"] ?? "test");

builder.Services.AddSingleton<IAmazonSQS>(_ => new AmazonSQSClient(sqsCredentials, sqsConfig));
builder.Services.AddSingleton<SqsPublisherService>();

builder.Services.AddScoped<VehicleService>();

builder.AddRedisDistributedCache("cache");

var app = builder.Build();

app.MapDefaultEndpoints();

app.MapGet("/api/vehicles", async (int id, [FromServices] VehicleService vehicleService, ILogger<Program> logger) =>
{
    if (id <= 0)
    {
        logger.LogWarning("Invalid vehicle ID {Id} requested", id);
        return Results.BadRequest("ID must be greater than 0");
    }

    var vehicle = await vehicleService.GetByIdAsync(id);
    return Results.Ok(vehicle);
});

app.Run();
