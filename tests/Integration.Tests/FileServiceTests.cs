using System.Text.Json;
using Amazon.SQS.Model;
using Minio.DataModel.Args;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Интеграционные тесты FileService: потребление из SQS и сохранение в MinIO.
/// </summary>
[Collection(nameof(IntegrationCollection))]
public class FileServiceTests : IClassFixture<FileServiceFactory>
{
    private readonly IntegrationFixture _fixture;
    private readonly HttpClient _fileServiceClient;

    public FileServiceTests(FileServiceFactory factory, IntegrationFixture fixture)
    {
        _fixture = fixture;
        _fileServiceClient = factory.CreateClient();
    }

    [Fact]
    public async Task FileService_ConsumesMessage_SavesFileToMinio()
    {
        var vehicle = new
        {
            id = 777,
            vin = "1HGBH41JXMN109186",
            manufacturer = "Honda",
            model = "Civic",
            year = 2015,
            bodyType = "Sedan",
            fuelType = "Gasoline",
            color = "White",
            mileage = 75000.0,
            lastServiceDate = "2023-06-01"
        };

        var messageBody = JsonSerializer.Serialize(vehicle);

        await _fixture.SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _fixture.SqsQueueUrl,
            MessageBody = messageBody
        });

        await Task.Delay(3000);

        var objectsInBucket = await ListObjectsAsync(IntegrationFixture.BucketName);

        Assert.NotEmpty(objectsInBucket);

        var savedFile = objectsInBucket.First();
        var content = await GetObjectContentAsync(IntegrationFixture.BucketName, savedFile);
        var savedDoc = JsonDocument.Parse(content);

        Assert.Equal(777, savedDoc.RootElement.GetProperty("id").GetInt32());
        Assert.Equal("1HGBH41JXMN109186", savedDoc.RootElement.GetProperty("vin").GetString());
    }

    [Fact]
    public async Task FileService_ConsumesMultipleMessages_SavesAllFilesToMinio()
    {
        var vehicleIds = new[] { 801, 802, 803 };

        foreach (var id in vehicleIds)
        {
            var vehicle = new { id, vin = $"VIN{id:D10}", manufacturer = "Toyota", model = "Corolla" };
            await _fixture.SqsClient.SendMessageAsync(new SendMessageRequest
            {
                QueueUrl = _fixture.SqsQueueUrl,
                MessageBody = JsonSerializer.Serialize(vehicle)
            });
        }

        await Task.Delay(4000);

        var objects = await ListObjectsAsync(IntegrationFixture.BucketName);

        Assert.True(objects.Count >= vehicleIds.Length,
            $"Expected at least {vehicleIds.Length} objects in MinIO, found {objects.Count}");
    }

    [Fact]
    public async Task FileService_DeletesMessageAfterProcessing()
    {
        var vehicle = new { id = 900, vin = "DELETEME123456789", manufacturer = "Ford" };

        await _fixture.SqsClient.SendMessageAsync(new SendMessageRequest
        {
            QueueUrl = _fixture.SqsQueueUrl,
            MessageBody = JsonSerializer.Serialize(vehicle)
        });

        await Task.Delay(3000);

        var remaining = await _fixture.SqsClient.ReceiveMessageAsync(new ReceiveMessageRequest
        {
            QueueUrl = _fixture.SqsQueueUrl,
            MaxNumberOfMessages = 10,
            WaitTimeSeconds = 1
        });

        var ourMessage = remaining.Messages.FirstOrDefault(m => m.Body.Contains("DELETEME123456789"));

        Assert.Null(ourMessage);
    }

    [Fact]
    public async Task FileService_HealthCheck_ReturnsHealthy()
    {
        var response = await _fileServiceClient.GetAsync("/health");

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<List<string>> ListObjectsAsync(string bucketName)
    {
        var result = new List<string>();

        var args = new ListObjectsArgs()
            .WithBucket(bucketName)
            .WithRecursive(true);

        var tcs = new TaskCompletionSource<List<string>>();
        var items = new List<string>();

        var observable = _fixture.MinioClient.ListObjectsAsync(args);
        var subscription = observable.Subscribe(
            item => items.Add(item.Key),
            ex => tcs.TrySetException(ex),
            () => tcs.TrySetResult(items));

        using (subscription)
        {
            result = await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }

        return result;
    }

    private async Task<string> GetObjectContentAsync(string bucketName, string objectName)
    {
        using var ms = new MemoryStream();

        await _fixture.MinioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(ms)));

        ms.Position = 0;
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
