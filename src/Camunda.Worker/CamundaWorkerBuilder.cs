using System;
using Camunda.Worker.Endpoints;
using Camunda.Worker.Execution;
using Camunda.Worker.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Camunda.Worker;

public class CamundaWorkerBuilder : ICamundaWorkerBuilder
{
    public CamundaWorkerBuilder(IServiceCollection services, WorkerIdString workerId)
    {
        Services = services;
        WorkerId = workerId;
    }

    public IServiceCollection Services { get; }

    public WorkerIdString WorkerId { get; }

    internal CamundaWorkerBuilder AddDefaultEndpointResolver()
    {
        AddEndpointResolver((workerId, provider) => new TopicBasedEndpointResolver(
            workerId,
            provider.GetRequiredService<IEndpointsCollection>()
        ));

        return this;
    }

    public ICamundaWorkerBuilder AddEndpointResolver(WorkerServiceFactory<IEndpointResolver> factory)
    {
        Services.AddKeyedSingleton(WorkerId.Value, (provider, _) => factory(WorkerId, provider));

        return this;
    }

    internal CamundaWorkerBuilder AddDefaultFetchAndLockRequestProvider()
    {
        AddFetchAndLockRequestProvider((workerId, provider) => new FetchAndLockRequestProvider(
            workerId,
            provider.GetRequiredService<IOptionsMonitor<FetchAndLockOptions>>(),
            provider.GetRequiredService<IEndpointsCollection>()
        ));

        return this;
    }

    public ICamundaWorkerBuilder AddFetchAndLockRequestProvider(
        WorkerServiceFactory<IFetchAndLockRequestProvider> factory
    )
    {
        Services.AddKeyedSingleton(WorkerId.Value, (provider, _) => factory(WorkerId, provider));

        return this;
    }

    public ICamundaWorkerBuilder AddHandler(ExternalTaskDelegate handler, EndpointMetadata endpointMetadata)
    {
        Guard.NotNull(handler, nameof(handler));
        Guard.NotNull(endpointMetadata, nameof(endpointMetadata));

        var endpoint = new Endpoint(handler, endpointMetadata, WorkerId);

        Services.AddSingleton(endpoint);
        return this;
    }

    public ICamundaWorkerBuilder ConfigurePipeline(Action<IPipelineBuilder> configureAction)
    {
        Guard.NotNull(configureAction, nameof(configureAction));
        Services.AddKeyedSingleton<IExternalTaskProcessingService>(WorkerId.Value, (provider, _) =>
        {
            var externalTaskDelegate = new PipelineBuilder(provider, WorkerId)
                .Also(configureAction)
                .Build(ExternalTaskRouter.RouteAsync);
            return new ExternalTaskProcessingService(provider, externalTaskDelegate);
        });
        return this;
    }

    public ICamundaWorkerBuilder ConfigureEvents(Action<WorkerEvents> configureAction)
    {
        Services.Configure(WorkerId.Value, configureAction);
        return this;
    }
}
