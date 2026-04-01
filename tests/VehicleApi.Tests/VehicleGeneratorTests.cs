using VehicleApi.Services;
using Xunit;

namespace VehicleApi.Tests;

public class VehicleGeneratorTests
{
    [Fact]
    public void Generate_SameId_ReturnsSameData()
    {
        var vehicle1 = VehicleGenerator.Generate(42);
        var vehicle2 = VehicleGenerator.Generate(42);

        Assert.Equal(vehicle1.Vin, vehicle2.Vin);
        Assert.Equal(vehicle1.Manufacturer, vehicle2.Manufacturer);
        Assert.Equal(vehicle1.Model, vehicle2.Model);
        Assert.Equal(vehicle1.Year, vehicle2.Year);
        Assert.Equal(vehicle1.BodyType, vehicle2.BodyType);
        Assert.Equal(vehicle1.FuelType, vehicle2.FuelType);
        Assert.Equal(vehicle1.Color, vehicle2.Color);
        Assert.Equal(vehicle1.Mileage, vehicle2.Mileage);
    }

    [Theory]
    [InlineData(1, 2)]
    [InlineData(10, 20)]
    [InlineData(42, 100)]
    public void Generate_DifferentIds_ReturnsDifferentData(int id1, int id2)
    {
        var vehicle1 = VehicleGenerator.Generate(id1);
        var vehicle2 = VehicleGenerator.Generate(id2);

        Assert.NotEqual(vehicle1.Vin, vehicle2.Vin);
    }

    [Fact]
    public void Generate_YearConstraint_IsValid()
    {
        for (var i = 1; i <= 100; i++)
        {
            var vehicle = VehicleGenerator.Generate(i);

            Assert.True(vehicle.Year >= 1990, $"Vehicle {i} has Year {vehicle.Year} which is less than 1990");
            Assert.True(vehicle.Year <= DateTime.Now.Year, $"Vehicle {i} has Year {vehicle.Year} which exceeds current year {DateTime.Now.Year}");
        }
    }

    [Fact]
    public void Generate_MileageConstraint_IsValid()
    {
        for (var i = 1; i <= 100; i++)
        {
            var vehicle = VehicleGenerator.Generate(i);

            Assert.True(vehicle.Mileage >= 0, $"Vehicle {i} has Mileage {vehicle.Mileage} which is less than 0");
            Assert.True(vehicle.Mileage <= 500000, $"Vehicle {i} has Mileage {vehicle.Mileage} which exceeds 500,000");
        }
    }

    [Fact]
    public void Generate_LastServiceDateConstraint_IsValid()
    {
        for (var i = 1; i <= 100; i++)
        {
            var vehicle = VehicleGenerator.Generate(i);

            Assert.True(vehicle.LastServiceDate.Year >= vehicle.Year,
                $"Vehicle {i} has LastServiceDate year {vehicle.LastServiceDate.Year} which is before the vehicle's manufacturing year {vehicle.Year}");
            Assert.True(vehicle.LastServiceDate <= DateOnly.FromDateTime(DateTime.Now),
                $"Vehicle {i} has LastServiceDate {vehicle.LastServiceDate} which is in the future");
        }
    }
}
