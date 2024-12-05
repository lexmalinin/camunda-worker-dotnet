using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Camunda.Worker.Client;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Camunda.Worker.Execution;

internal sealed class DefaultCamundaWorker : ICamundaWorker
{
    private readonly IExternalTaskClient _externalTaskClient;
    private readonly IFetchAndLockRequestProvider _fetchAndLockRequestProvider;
    private readonly WorkerEvents _workerEvents;
    private readonly IServiceProvider _serviceProvider;
    private readonly IExternalTaskProcessingService _processingService;
    private readonly ILogger<DefaultCamundaWorker> _logger;

    public DefaultCamundaWorker(
        WorkerIdString workerId,
        IExternalTaskClient externalTaskClient,
        IFetchAndLockRequestProvider fetchAndLockRequestProvider,
        IOptionsMonitor<WorkerEvents> workerEvents,
        IServiceProvider serviceProvider,
        IExternalTaskProcessingService processingService,
        ILogger<DefaultCamundaWorker>? logger
    )
    {
        _externalTaskClient = externalTaskClient;
        _fetchAndLockRequestProvider = fetchAndLockRequestProvider;
        _workerEvents = workerEvents.Get(workerId.Value);
        _serviceProvider = serviceProvider;
        _processingService = processingService;
        _logger = logger ?? NullLogger<DefaultCamundaWorker>.Instance;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            var externalTasks = await SelectAsync(cancellationToken);

            if (externalTasks is { Count: > 0 })
            {
                var tasks = new Task[externalTasks.Count];
                var i = 0;
                foreach (var externalTask in externalTasks)
                {
                    tasks[i++] = Task.Run(
                        () => ProcessExternalTaskAsync(externalTask, cancellationToken),
                        cancellationToken
                    );
                }

                await Task.WhenAll(tasks);
            }

            await _workerEvents.OnAfterProcessingAllTasks(_serviceProvider, cancellationToken);
        }
    }

    private async Task<List<ExternalTask>?> SelectAsync(CancellationToken cancellationToken)
    {
        await _workerEvents.OnBeforeFetchAndLock(_serviceProvider, cancellationToken);

        try
        {
            _logger.LogWorker_Waiting();
            var fetchAndLockRequest = _fetchAndLockRequestProvider.GetRequest();
            var externalTasks = await _externalTaskClient.FetchAndLockAsync(fetchAndLockRequest, cancellationToken);
            _logger.LogWorker_Locked(externalTasks.Count);
            return externalTasks;
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWorker_FailedLocking(e.Message, e);
            await _workerEvents.OnFailedFetchAndLock(_serviceProvider, cancellationToken);
            return null;
        }
    }

    private async Task ProcessExternalTaskAsync(ExternalTask externalTask, CancellationToken cancellationToken)
    {
        try
        {
            await _processingService.ProcessAsync(externalTask, _externalTaskClient, cancellationToken);
        }
        catch (Exception e)
        {
            _logger.LogWorker_FailedExecution(externalTask.Id, e);
        }
    }
}
