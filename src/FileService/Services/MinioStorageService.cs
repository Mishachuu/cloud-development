using Minio;
using Minio.DataModel.Args;

namespace FileService.Services;

/// <summary>
/// Сервис для сохранения файлов в объектном хранилище MinIO.
/// </summary>
public class MinioStorageService(IMinioClient minio, IConfiguration config, ILogger<MinioStorageService> logger)
{
    private readonly string _bucketName = config["Minio:BucketName"] ?? "vehicles";

    /// <summary>
    /// Инициализирует bucket при старте сервиса, создавая его если не существует.
    /// </summary>
    public async Task EnsureBucketExistsAsync()
    {
        var exists = await minio.BucketExistsAsync(new BucketExistsArgs().WithBucket(_bucketName));

        if (!exists)
        {
            await minio.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucketName));
            logger.LogInformation("Bucket '{Bucket}' created", _bucketName);
        }
        else
        {
            logger.LogInformation("Bucket '{Bucket}' already exists", _bucketName);
        }
    }

    /// <summary>
    /// Сохраняет JSON-содержимое как файл в объектном хранилище.
    /// </summary>
    /// <param name="objectName">Имя объекта (файла) в bucket.</param>
    /// <param name="jsonContent">JSON-содержимое для сохранения.</param>
    public async Task SaveAsync(string objectName, string jsonContent)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(jsonContent);
        using var stream = new MemoryStream(bytes);

        var args = new PutObjectArgs()
            .WithBucket(_bucketName)
            .WithObject(objectName)
            .WithStreamData(stream)
            .WithObjectSize(bytes.Length)
            .WithContentType("application/json");

        await minio.PutObjectAsync(args);

        logger.LogInformation("Saved object '{Object}' to bucket '{Bucket}'", objectName, _bucketName);
    }
}
