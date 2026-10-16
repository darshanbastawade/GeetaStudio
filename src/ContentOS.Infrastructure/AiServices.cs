using ContentOS.Application;
using ContentOS.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
namespace ContentOS.Infrastructure;

public static class AiServices
{
    public static IServiceCollection AddAiControlPlane(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<AiService>(s =>
        {
            var service = new AiService(s.GetRequiredService<CoreDbContext>(), s.GetRequiredService<RuntimeDbContext>(),
                s.GetRequiredService<ISecretResolver>(), s.GetServices<ITextGenerationProvider>(), s.GetServices<IAiEmbeddingAdapter>(),
                configuration, s.GetRequiredService<Microsoft.Extensions.Logging.ILogger<AiService>>(), s);
            service.Retrieval = async (scope, universe, query, ct) =>
            {
                using var child = s.GetRequiredService<IServiceScopeFactory>().CreateScope();
                return await child.ServiceProvider.GetRequiredService<IKnowledgeRetriever>().RetrieveAsync(scope, universe, query, ct);
            };
            return service;
        });
        services.AddScoped<IAiAdministration>(s => s.GetRequiredService<AiService>());
        services.AddScoped<IAiTaskRouter>(s => s.GetRequiredService<AiService>());
        services.AddSingleton<RoutedEmbeddingProvider>();
        services.AddSingleton<IEmbeddingProvider>(s => s.GetRequiredService<RoutedEmbeddingProvider>());
        return services;
    }
}
