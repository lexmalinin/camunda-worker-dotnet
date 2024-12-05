using System.Collections.Generic;
using System.Threading.Tasks;
using Camunda.Worker.Endpoints;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Camunda.Worker.Routing;

public class ExternalTaskRouterTest
{
    private const string _workerId = "testWorker";
    private readonly Mock<IExternalTaskContext> _contextMock = new();
    private readonly Mock<IEndpointResolver> _endpointResolverMock = new();

    public ExternalTaskRouterTest()
    {
        var serviceProvider = new ServiceCollection()
            .AddKeyedSingleton(_workerId, _endpointResolverMock.Object)
            .BuildServiceProvider();

        _contextMock.SetupGet(context => context.ServiceProvider).Returns(serviceProvider);
        _contextMock.SetupGet(context => context.Task).Returns(new ExternalTask("1", _workerId, "testTopic"));
    }

    [Fact]
    public async Task TestRouteAsync()
    {
        // Arrange
        var calls = new List<IExternalTaskContext>();

        var endpoint = new Endpoint(
            context =>
            {
                calls.Add(context);
                return Task.CompletedTask;
            },
            new EndpointMetadata(new []{ "testTopic" }),
            "testWorker"
        );

        _endpointResolverMock.Setup(factory => factory.Resolve(It.IsAny<ExternalTask>()))
            .Returns(endpoint);

        // Act
        await ExternalTaskRouter.RouteAsync(_contextMock.Object);

        // Assert
        Assert.Single(calls);
    }
}
