using ApiGateway.LoadBalancing;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var weights = builder.Configuration
    .GetSection("WeightedRandom:Weights")
    .Get<double[]>() ?? [0.5, 0.3, 0.2];

builder.Services.AddOcelot(builder.Configuration)
    .AddCustomLoadBalancer<WeightedRandomLoadBalancer>((serviceProvider, route, serviceDiscoveryProvider) =>
    {
        var services = serviceDiscoveryProvider.GetAsync().GetAwaiter().GetResult().ToList();
        return new WeightedRandomLoadBalancer(services, weights);
    });

var app = builder.Build();

app.UseCors();
app.MapDefaultEndpoints();  

await app.UseOcelot();

app.Run();