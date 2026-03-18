var builder = DistributedApplication.CreateBuilder(args);

var ports = builder.Configuration
    .GetSection("ApiService:Ports")
    .GetChildren()
    .Select(x => int.Parse(x.Value!))
    .ToList();

if (ports.Count == 0)
    ports = [5101, 5102, 5103];

var gatewayPort = int.TryParse(builder.Configuration["ApiGateway:Port"], out var p) ? p : 5200;

var cache = builder.AddRedis("cache")
    .WithRedisInsight();

var gateway = builder.AddProject<Projects.ApiGateway>("apigateway")
    .WithHttpEndpoint(port: gatewayPort, name: "gateway-endpoint", isProxied: false)
    .WithExternalHttpEndpoints();

var serviceId = 1;
foreach (var port in ports)
{
    var replica = builder.AddProject<Projects.VehicleApi>($"vehicleapi-{serviceId++}")
        .WithReference(cache)
        .WithHttpEndpoint(port: port, name: "api-endpoint", isProxied: false)
        .WithExternalHttpEndpoints();

    gateway.WaitFor(replica);
}

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();