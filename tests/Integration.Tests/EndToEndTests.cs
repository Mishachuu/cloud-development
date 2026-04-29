using System.Net;
using System.Text.Json;
using Minio.DataModel.Args;
using Xunit;

namespace Integration.Tests;

/// <summary>
/// Сквозные (end-to-end) тесты всего бэкенда:
/// HTTP-запрос к VehicleApi → публикация в SQS → потребление FileService → сохранение в MinIO.
/// </summary>
[Collection(nameof(IntegrationCollection))]
public class EndToEndTests(
    VehicleApiFactory vehicleApiFactory,
    FileServiceFactory fileServiceFactory,
    IntegrationFixture fixture)
    : IClassFixture<VehicleApiFactory>, IClassFixture<FileServiceFactory>
{
    private readonly HttpClient _vehicleClient = vehicleApiFactory.CreateClient();

    [Fact]
    public async Task RequestVehicle_FullPipeline_FileAppearsInMinio()
    {
        var vehicleId = Random.Shared.Next(300_000, 399_999);

        var response = await _vehicleClient.GetAsync($"/api/vehicles?id={vehicleId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var json = await response.Content.ReadAsStringAsync();
        var vehicle = JsonDocument.Parse(json).RootElement;
        var returnedVin = vehicle.GetProperty("vin").GetString();

        List<string> objects = [];
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            objects = await ListObjectsAsync(IntegrationFixture.BucketName);
            var found = await FindVehicleInMinioAsync(objects, vehicleId);
            if (found) break;
            await Task.Delay(500);
        }

        var matchingFile = await FindVehicleFileAsync(objects, vehicleId);
        Assert.NotNull(matchingFile);

        var fileContent = await GetObjectContentAsync(IntegrationFixture.BucketName, matchingFile);
        var savedDoc = JsonDocument.Parse(fileContent).RootElement;

        Assert.Equal(vehicleId, savedDoc.GetProperty("id").GetInt32());
        Assert.Equal(returnedVin, savedDoc.GetProperty("vin").GetString());
    }

    [Fact]
    public async Task RequestVehicle_SecondRequest_OnlySingleFileInMinio()
    {
        var vehicleId = Random.Shared.Next(400_000, 499_999);

        await _vehicleClient.GetAsync($"/api/vehicles?id={vehicleId}");
        await Task.Delay(3000);

        var objectsAfterFirst = await ListObjectsAsync(IntegrationFixture.BucketName);
        var firstCount = objectsAfterFirst.Count(o => o.Contains(vehicleId.ToString()) == false
        );

        var totalAfterFirst = objectsAfterFirst.Count;

        await _vehicleClient.GetAsync($"/api/vehicles?id={vehicleId}");
        await Task.Delay(2000);

        var objectsAfterSecond = await ListObjectsAsync(IntegrationFixture.BucketName);

        Assert.Equal(totalAfterFirst, objectsAfterSecond.Count);
    }

    [Fact]
    public async Task MultipleVehicles_AllFilesAppearInMinio()
    {
        var ids = Enumerable.Range(500_000, 3).ToList();
        var initialCount = (await ListObjectsAsync(IntegrationFixture.BucketName)).Count;

        foreach (var id in ids)
            await _vehicleClient.GetAsync($"/api/vehicles?id={id}");

        List<string> objects = [];
        var deadline = DateTime.UtcNow.AddSeconds(15);

        while (DateTime.UtcNow < deadline)
        {
            objects = await ListObjectsAsync(IntegrationFixture.BucketName);
            if (objects.Count >= initialCount + ids.Count) break;
            await Task.Delay(500);
        }

        Assert.True(objects.Count >= initialCount + ids.Count,
            $"Expected at least {initialCount + ids.Count} objects in MinIO, found {objects.Count}");
    }

    private async Task<List<string>> ListObjectsAsync(string bucketName)
    {
        var items = new List<string>();
        var tcs = new TaskCompletionSource<List<string>>();

        var args = new ListObjectsArgs().WithBucket(bucketName).WithRecursive(true);
        var observable = fixture.MinioClient.ListObjectsAsync(args);

        var sub = observable.Subscribe(
            item => items.Add(item.Key),
            ex => tcs.TrySetException(ex),
            () => tcs.TrySetResult(items));

        using (sub)
            return await tcs.Task.WaitAsync(TimeSpan.FromSeconds(10));
    }

    private async Task<bool> FindVehicleInMinioAsync(List<string> objectNames, int vehicleId)
    {
        foreach (var name in objectNames)
        {
            var content = await GetObjectContentAsync(IntegrationFixture.BucketName, name);
            try
            {
                var doc = JsonDocument.Parse(content).RootElement;
                if (doc.TryGetProperty("id", out var idProp) && idProp.GetInt32() == vehicleId)
                    return true;
            }
            catch {}
        }
        return false;
    }

    private async Task<string?> FindVehicleFileAsync(List<string> objectNames, int vehicleId)
    {
        foreach (var name in objectNames)
        {
            var content = await GetObjectContentAsync(IntegrationFixture.BucketName, name);
            try
            {
                var doc = JsonDocument.Parse(content).RootElement;
                if (doc.TryGetProperty("id", out var idProp) && idProp.GetInt32() == vehicleId)
                    return name;
            }
            catch {}
        }
        return null;
    }

    private async Task<string> GetObjectContentAsync(string bucketName, string objectName)
    {
        using var ms = new MemoryStream();
        await fixture.MinioClient.GetObjectAsync(
            new GetObjectArgs()
                .WithBucket(bucketName)
                .WithObject(objectName)
                .WithCallbackStream(stream => stream.CopyTo(ms)));

        ms.Position = 0;
        return System.Text.Encoding.UTF8.GetString(ms.ToArray());
    }
}
