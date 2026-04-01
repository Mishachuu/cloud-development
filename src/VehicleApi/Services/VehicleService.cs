using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using VehicleApi.Models;

namespace VehicleApi.Services;

public class VehicleService(IDistributedCache cache, ILogger<VehicleService> logger, IConfiguration config)
{
    private DistributedCacheEntryOptions GetCacheOptions()
    {
        var minutes = config.GetValue<int>("Cache:AbsoluteExpirationMinutes", 10);
        return new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(minutes)
        };
    }

    public async Task<Vehicle> GetByIdAsync(int id)
    {
        var cacheKey = $"vehicle:{id}";
        var cachedData = await cache.GetAsync(cacheKey);

        if (cachedData != null)
        {
            logger.LogInformation("Cache hit for vehicle ID {Id}", id);
            var cached = JsonSerializer.Deserialize<Vehicle>(cachedData);
            if (cached != null)
                return cached;
        }

        logger.LogInformation("Cache miss for vehicle ID {Id}", id);
        var vehicle = VehicleGenerator.Generate(id);

        var serialized = JsonSerializer.SerializeToUtf8Bytes(vehicle);
        await cache.SetAsync(cacheKey, serialized, GetCacheOptions());
        logger.LogInformation("Vehicle {Id} cached", id);

        return vehicle;
    }
}