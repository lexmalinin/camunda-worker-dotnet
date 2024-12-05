using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Camunda.Worker.Execution;

internal sealed class WorkerHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly WorkerIdString _workerId;
    private readonly int _numberOfWorkers;

    public WorkerHostedService(IServiceProvider serviceProvider, WorkerIdString workerId, int numberOfWorkers)
    {
        _serviceProvider = serviceProvider;
        _workerId = workerId;
        _numberOfWorkers = numberOfWorkers;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var activeTasks = Enumerable.Range(0, _numberOfWorkers)
            .Select(_ => _serviceProvider.GetRequiredKeyedService<ICamundaWorker>(_workerId.Value))
            .Select(worker => worker.RunAsync(stoppingToken))
            .ToList();
        return Task.WhenAll(activeTasks);
    }
}
