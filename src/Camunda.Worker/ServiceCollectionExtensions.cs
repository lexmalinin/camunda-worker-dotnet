using Camunda.Worker.Client;
using Camunda.Worker.Endpoints;
using Camunda.Worker.Execution;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Camunda.Worker;

public static class CamundaWorkerServiceCollectionExtensions
{
    public static ICamundaWorkerBuilder AddCamundaWorker(
        this IServiceCollection services,
        WorkerIdString workerId,
        int numberOfWorkers = Constants.MinimumParallelExecutors
    )
    {
        Guard.GreaterThanOrEqual(numberOfWorkers, Constants.MinimumParallelExecutors, nameof(numberOfWorkers));

        services.AddOptions<FetchAndLockOptions>(workerId.Value);
        services.AddOptions<WorkerEvents>(workerId.Value);
        services.TryAddSingleton<IEndpointsCollection, EndpointsCollection>();
        services.TryAddKeyedTransient<ICamundaWorker>(workerId.Value, (provider, key) => new DefaultCamundaWorker(
            workerId,
            provider.GetRequiredService<IExternalTaskClient>(),
            provider.GetRequiredKeyedService<IFetchAndLockRequestProvider>(key),
            provider.GetRequiredService<IOptionsMonitor<WorkerEvents>>(),
            provider,
            provider.GetRequiredKeyedService<IExternalTaskProcessingService>(key),
            provider.GetService<ILogger<DefaultCamundaWorker>>()
        ));
        services.AddTransient<IHostedService>(provider => new WorkerHostedService(provider, workerId, numberOfWorkers));

        return new CamundaWorkerBuilder(services, workerId)
            .AddDefaultFetchAndLockRequestProvider()
            .AddDefaultEndpointResolver()
            .ConfigurePipeline(_ => { });
    }
}
