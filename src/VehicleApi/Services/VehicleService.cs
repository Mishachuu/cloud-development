using VehicleApi.Models;

namespace VehicleApi.Services;

public class VehicleService
{
    public Task<Vehicle> GetByIdAsync(int id)
    {
        var vehicle = VehicleGenerator.Generate(id);
        return Task.FromResult(vehicle);
    }
}