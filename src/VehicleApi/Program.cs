using VehicleApi.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

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
