using System;
using System.Threading.Tasks;
using Camunda.Worker.Client;
using Camunda.Worker.Endpoints;
using Camunda.Worker.Execution;
using Camunda.Worker.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Camunda.Worker;

public class CamundaWorkerBuilderTest
{
    private readonly WorkerIdString _workerId = new("testWorker");
    private readonly ServiceCollection _services = new();
    private readonly CamundaWorkerBuilder _builder;

    public CamundaWorkerBuilderTest()
    {
        _builder = new CamundaWorkerBuilder(_services, _workerId);
    }

    [Fact]
    public void TestAddHandler()
    {
        Task FakeHandlerDelegate(IExternalTaskContext context) => Task.CompletedTask;

        _builder.AddHandler(FakeHandlerDelegate, new EndpointMetadata(new[] {"testTopic"}));

        Assert.Contains(_services, d => d.Lifetime == ServiceLifetime.Singleton &&
                                        d.ImplementationInstance != null);
    }

    [Fact]
    public void TestAddEndpointResolver()
    {
        _builder.AddEndpointResolver((_, _) => new EndpointResolver());

        using var provider = _services.BuildServiceProvider();

        Assert.IsType<EndpointResolver>(provider.GetKeyedService<IEndpointResolver>(_workerId.Value));
    }

    [Fact]
    public void TestAddFetchAndLockRequestProvider()
    {
        _builder.AddFetchAndLockRequestProvider((_, _) => new FetchAndLockRequestProvider());

        using var provider = _services.BuildServiceProvider();

        Assert.IsType<FetchAndLockRequestProvider>(provider.GetKeyedService<IFetchAndLockRequestProvider>(_workerId.Value));
    }

    [Fact]
    public void TestConfigurePipeline()
    {
        _builder.ConfigurePipeline(pipeline => { });

        Assert.Contains(_services, d => d.Lifetime == ServiceLifetime.Singleton &&
                                        d.ServiceType == typeof(IExternalTaskProcessingService));
    }

    [Fact]
    public void TestConfigureEvents()
    {
        _builder.ConfigureEvents(events =>
        {
            events.OnBeforeFetchAndLock = (_, _) => Task.CompletedTask;
        });

        Assert.Contains(_services, d => d.ServiceType == typeof(IConfigureOptions<WorkerEvents>));
    }

    private class EndpointResolver : IEndpointResolver
    {

        public Endpoint? Resolve(ExternalTask externalTask)
        {
            throw new NotImplementedException();
        }
    }

    private class FetchAndLockRequestProvider : IFetchAndLockRequestProvider
    {
        public FetchAndLockRequest GetRequest()
        {
            throw new NotImplementedException();
        }
    }
}
