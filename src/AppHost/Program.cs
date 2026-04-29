var builder = DistributedApplication.CreateBuilder(args);

var ports = builder.Configuration
    .GetSection("ApiService:Ports")
    .GetChildren()
    .Select(x => int.Parse(x.Value!))
    .ToList();

if (ports.Count == 0)
    ports = [5101, 5102, 5103];

var gatewayPort = int.TryParse(builder.Configuration["ApiGateway:Port"], out var p) ? p : 5200;
var fileServicePort = int.TryParse(builder.Configuration["FileService:Port"], out var fp) ? fp : 5300;

var cache = builder.AddRedis("cache")
    .WithRedisInsight();

var minio = builder.AddContainer("minio", "minio/minio")
    .WithArgs("server", "/data", "--console-address", ":9001")
    .WithEnvironment("MINIO_ROOT_USER", "minioadmin")
    .WithEnvironment("MINIO_ROOT_PASSWORD", "minioadmin")
    .WithHttpEndpoint(port: 9000, targetPort: 9000, name: "api")
    .WithHttpEndpoint(port: 9001, targetPort: 9001, name: "console");

var localstack = builder.AddContainer("localstack", "localstack/localstack", "3.0.0")
    .WithEnvironment("SERVICES", "sqs")
    .WithEnvironment("DEFAULT_REGION", "us-east-1")
    .WithEnvironment("AWS_DEFAULT_REGION", "us-east-1")
    .WithEnvironment("AWS_ACCESS_KEY_ID", "test")
    .WithEnvironment("AWS_SECRET_ACCESS_KEY", "test")
    .WithEnvironment("LOCALSTACK_ACKNOWLEDGE_ACCOUNT_REQUIREMENT", "1")
    .WithHttpEndpoint(port: 4566, targetPort: 4566, name: "api");

var fileService = builder.AddProject<Projects.FileService>("fileservice")
    .WithEnvironment("Sqs__ServiceUrl", "http://localhost:4566")
    .WithEnvironment("Sqs__QueueUrl", "http://localhost:4566/000000000000/vehicles")
    .WithEnvironment("Minio__Endpoint", "localhost:9000")
    .WaitFor(minio)
    .WaitFor(localstack);

var gateway = builder.AddProject<Projects.ApiGateway>("apigateway")
    .WithHttpEndpoint(port: gatewayPort, name: "gateway-endpoint", isProxied: false)
    .WithExternalHttpEndpoints();

var serviceId = 1;
foreach (var port in ports)
{
    var replica = builder.AddProject<Projects.VehicleApi>($"vehicleapi-{serviceId++}")
    .WithReference(cache)
    .WithEnvironment("Sqs__ServiceUrl", "http://localhost:4566")
    .WithEnvironment("Sqs__QueueUrl", "http://localhost:4566/000000000000/vehicles")
    .WithHttpEndpoint(port: port, name: "api-endpoint", isProxied: false)
    .WithExternalHttpEndpoints()
    .WaitFor(cache)
    .WaitFor(localstack);

    gateway.WithReference(replica)
    .WaitFor(replica);
    fileService.WaitFor(replica);
}

builder.AddProject<Projects.Client_Wasm>("client")
    .WithReference(gateway)
    .WaitFor(gateway);

builder.Build().Run();
