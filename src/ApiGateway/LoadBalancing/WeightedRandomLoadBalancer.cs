using Ocelot.LoadBalancer.LoadBalancers;
using Ocelot.Responses;
using Ocelot.Values;

namespace ApiGateway.LoadBalancing;

public class WeightedRandomLoadBalancer(List<Service> services, double[] weights) : ILoadBalancer
{
    public string Type => nameof(WeightedRandomLoadBalancer);

    public Task<Response<ServiceHostAndPort>> LeaseAsync(HttpContext httpContext)
    {
        var cumulative = 0.0;
        var roll = Random.Shared.NextDouble();

        for (var i = 0; i < services.Count; i++)
        {
            cumulative += weights[i];
            if (roll < cumulative)
                return Task.FromResult<Response<ServiceHostAndPort>>(
                    new OkResponse<ServiceHostAndPort>(services[i].HostAndPort));
        }

        return Task.FromResult<Response<ServiceHostAndPort>>(
            new OkResponse<ServiceHostAndPort>(services[Random.Shared.Next(services.Count)].HostAndPort));
    }

    public void Release(ServiceHostAndPort hostAndPort) { }
}