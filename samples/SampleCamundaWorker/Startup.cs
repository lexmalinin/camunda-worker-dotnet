using System;
using Camunda.Worker;
using Camunda.Worker.Client;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SampleCamundaWorker.Handlers;

namespace SampleCamundaWorker;

public class Startup
{
    public Startup(IConfiguration configuration)
    {
        Configuration = configuration;
    }

    public IConfiguration Configuration { get; }

    public void ConfigureServices(IServiceCollection services)
    {
        services.AddExternalTaskClient(client =>
        {
            client.BaseAddress = new Uri("http://localhost:8080/engine-rest");
        });

        services.AddCamundaWorker("sampleWorker")
            .AddHandler<SayHelloHandler>()
            .ConfigurePipeline(pipeline =>
            {
                pipeline.Use(next => async context =>
                {
                    var logger = context.ServiceProvider.GetRequiredService<ILogger<Startup>>();
                    logger.LogInformation("Started processing of task {Id} by worker {WorkerId}", context.Task.Id, context.Task.WorkerId);
                    await next(context);
                    logger.LogInformation("Finished processing of task {Id}", context.Task.Id);
                });
            });

        services.AddCamundaWorker("sampleWorker2")
            .AddHandler<SayHelloGuestHandler>()
            .ConfigurePipeline(pipeline =>
            {
                pipeline.Use(next => async context =>
                {
                    var logger = context.ServiceProvider.GetRequiredService<ILogger<Startup>>();
                    logger.LogInformation("Started processing of task {Id} by worker {WorkerId}", context.Task.Id, context.Task.WorkerId);
                    await next(context);
                    logger.LogInformation("Finished processing of task {Id}", context.Task.Id);
                });
            });

        services.AddHealthChecks();
    }

    public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
    {
        if (env.IsDevelopment())
        {
            app.UseDeveloperExceptionPage();
        }

        app.UseHealthChecks("/health");
    }
}
