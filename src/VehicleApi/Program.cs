using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using VehicleApi.Models;
using VehicleApi.Services;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddScoped<VehicleService>();

builder.AddRedisDistributedCache("cache");

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>() ?? [];

        policy.WithOrigins(allowedOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

app.UseCors();

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
