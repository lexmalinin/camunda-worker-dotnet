using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bogus;
using Camunda.Worker.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Camunda.Worker.Execution;

public class DefaultCamundaWorkerTest : IDisposable
{
    private readonly WorkerIdString _workerId;
    private readonly Mock<IExternalTaskClient> _clientMock = new();
    private readonly Mock<IFetchAndLockRequestProvider> _fetchAndLockRequestProviderMock = new();
    private readonly Mock<IWorkerEvents> _workerEventsMock = new();
    private readonly Mock<FakeExternalTaskProcessingService> _processingServiceMock = new();
    private readonly ServiceProvider _serviceProvider;
    private readonly DefaultCamundaWorker _worker;

    public DefaultCamundaWorkerTest()
    {
        _workerId = new("test-worker");

        _serviceProvider = new ServiceCollection().BuildServiceProvider();

        _fetchAndLockRequestProviderMock
            .Setup(provider => provider.GetRequest())
            .Returns(new FetchAndLockRequest("test"));

        var workerEventsOptionsMock = new Mock<IOptionsMonitor<WorkerEvents>>();
        workerEventsOptionsMock
            .Setup(it => it.Get(_workerId.Value))
            .Returns(new WorkerEvents
            {
                OnFailedFetchAndLock = _workerEventsMock.Object.OnFailedFetchAndLock,
                OnAfterProcessingAllTasks = _workerEventsMock.Object.OnAfterProcessingAllTasks
            });

        _worker = new DefaultCamundaWorker(
            _workerId,
            _clientMock.Object,
            _fetchAndLockRequestProviderMock.Object,
            workerEventsOptionsMock.Object,
            _serviceProvider,
            _processingServiceMock.Object,
            NullLogger<DefaultCamundaWorker>.Instance
        );
    }

    public void Dispose()
    {
        _serviceProvider.Dispose();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(10)]
    public async Task TestRun(int numberOfExternalTasks)
    {
        // Arrange
        var externalTasks = new Faker<ExternalTask>()
            .CustomInstantiator(faker => new ExternalTask(
                faker.Random.Guid().ToString(),
                faker.Random.Word(),
                faker.Random.Word())
            )
            .GenerateLazy(numberOfExternalTasks)
            .ToList();

        var cts = new CancellationTokenSource();

        _workerEventsMock
            .Setup(e => e.OnAfterProcessingAllTasks(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Callback(cts.Cancel)
            .Returns(Task.CompletedTask);

        _clientMock
            .Setup(client => client.FetchAndLockAsync(It.IsAny<FetchAndLockRequest>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(externalTasks)
            .Verifiable();

        var processedTasks = new ConcurrentBag<ExternalTask>();

        _processingServiceMock
            .Setup(service => service.ProcessAsync(It.IsAny<ExternalTask>(), _clientMock.Object, It.IsAny<CancellationToken>()))
            .Callback((ExternalTask externalTask, IExternalTaskClient _, CancellationToken _) =>
            {
                processedTasks.Add(externalTask);
            })
            .Returns(Task.CompletedTask);

        // Act
        await _worker.RunAsync(cts.Token);

        // Assert
        _processingServiceMock.Verify(
            service => service.ProcessAsync(It.IsAny<ExternalTask>(), _clientMock.Object, It.IsAny<CancellationToken>()),
            Times.Exactly(numberOfExternalTasks)
        );
        Assert.Equal(
            externalTasks.ToHashSet(new ExternalTaskComparer()),
            processedTasks.ToHashSet(new ExternalTaskComparer())
        );
    }

    [Fact]
    public async Task TestCancelledSelection()
    {
        var cts = new CancellationTokenSource();
        var tcs = new TaskCompletionSource<List<ExternalTask>>();

        await using var reg = cts.Token.Register(() => tcs.SetCanceled());

        _clientMock
            .Setup(client =>
                client.FetchAndLockAsync(It.IsAny<FetchAndLockRequest>(), It.IsAny<CancellationToken>()))
            .Returns(tcs.Task);

        var resultTask = _worker.RunAsync(cts.Token);

        cts.Cancel();

        await Assert.ThrowsAsync<TaskCanceledException>(() => resultTask);
        _clientMock.VerifyAll();
    }

    [Fact]
    public async Task TestFailedFetchAndLock()
    {
        // Arrange
        var cts = new CancellationTokenSource();

        _workerEventsMock
            .Setup(e => e.OnAfterProcessingAllTasks(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()))
            .Callback(cts.Cancel)
            .Returns(Task.CompletedTask);

        _clientMock
            .Setup(client => client.FetchAndLockAsync(It.IsAny<FetchAndLockRequest>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new Exception("Some exception"))
            .Verifiable();

        // Act
        await _worker.RunAsync(cts.Token);

        // Assert
        _workerEventsMock.Verify(e => e.OnFailedFetchAndLock(It.IsAny<IServiceProvider>(), It.IsAny<CancellationToken>()), Times.Once());
    }

    public interface IWorkerEvents
    {
        public Task OnAfterProcessingAllTasks(IServiceProvider provider, CancellationToken ct);

        public Task OnFailedFetchAndLock(IServiceProvider provider, CancellationToken ct);
    }

    private class ExternalTaskComparer : IEqualityComparer<ExternalTask>
    {
        public bool Equals(ExternalTask? x, ExternalTask? y)
        {
            if (ReferenceEquals(x, y)) return true;
            if (ReferenceEquals(x, null)) return false;
            if (ReferenceEquals(y, null)) return false;
            if (x.GetType() != y.GetType()) return false;
            return x.Id == y.Id;
        }

        public int GetHashCode(ExternalTask obj)
        {
            return obj.Id.GetHashCode();
        }
    }

    public abstract class FakeExternalTaskProcessingService : IExternalTaskProcessingService
    {
        public abstract Task ProcessAsync(
            ExternalTask externalTask,
            IExternalTaskClient externalTaskClient,
            CancellationToken cancellationToken
        );
    }
}
