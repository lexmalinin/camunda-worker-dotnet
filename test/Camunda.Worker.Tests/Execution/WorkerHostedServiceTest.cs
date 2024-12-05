using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Camunda.Worker.Execution;

public class WorkerHostedServiceTest
{
    private readonly Mock<ICamundaWorker> _workerMock = new();

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public async Task TestRunWorkerOnStart(int numberOfWorkers)
    {
        var workerId = new WorkerIdString("test-worker");

        await using var serivceProvider = new ServiceCollection()
            .AddKeyedTransient(workerId.Value, (_, _) => _workerMock.Object)
            .BuildServiceProvider();

        _workerMock.Setup(w => w.RunAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        using (var workerHostedService = new WorkerHostedService(serivceProvider, workerId, numberOfWorkers))
        {
            await workerHostedService.StartAsync(CancellationToken.None);
        }

        _workerMock.Verify(w => w.RunAsync(It.IsAny<CancellationToken>()), Times.Exactly(numberOfWorkers));
    }
}
