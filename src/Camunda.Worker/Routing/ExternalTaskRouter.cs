using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace Camunda.Worker.Routing;

internal static class ExternalTaskRouter
{
    internal static async Task RouteAsync(IExternalTaskContext context)
    {
        Guard.NotNull(context, nameof(context));
        var provider = context.ServiceProvider;

        var endpointResolver = provider.GetRequiredKeyedService<IEndpointResolver>(context.Task.WorkerId);
        var endpoint = endpointResolver.Resolve(context.Task);

        if (endpoint is null)
        {
            throw new CamundaWorkerException($"Endpoint for external task {context.Task.Id} could not be resolved");
        }

        await endpoint.HandlerDelegate(context);
    }
}
