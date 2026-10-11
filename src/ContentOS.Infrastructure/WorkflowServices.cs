using ContentOS.Application;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public static class WorkflowServices
{
    public static IServiceCollection AddOwnedWorkflows(this IServiceCollection services)
    {
        services.AddScoped<WorkflowRuntime>();
        services.AddScoped<IWorkflowRuntime>(s => s.GetRequiredService<WorkflowRuntime>());
        services.AddScoped<IWorkflowWorker>(s => s.GetRequiredService<WorkflowRuntime>());
        services.AddScoped<IToolInvoker, WorkflowTools>();
        return services;
    }
}
