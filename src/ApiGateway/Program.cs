using ApiGateway.LoadBalancing;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Configuration.AddJsonFile("ocelot.json", optional: false, reloadOnChange: true);

var urls = new List<Uri>();

for (var i = 1; ; i++)
{
    var url = builder.Configuration[$"services__vehicleapi-{i}__http__0"];
    if (url == null) break;
    urls.Add(new Uri(url));
}

if (urls.Count > 0)
{
    for (var i = 0; i < urls.Count; i++)
    {
        builder.Configuration[$"Routes:0:DownstreamHostAndPorts:{i}:Host"] = urls[i].Host;
        builder.Configuration[$"Routes:0:DownstreamHostAndPorts:{i}:Port"] = urls[i].Port.ToString();
    }
}

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