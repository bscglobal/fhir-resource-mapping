using BSC.Fhir.Mapping.Core;
using BSC.Fhir.Mapping.Core.Expressions;
using BSC.Fhir.Mapping.Expressions;
using Microsoft.Extensions.DependencyInjection;

namespace BSC.Fhir.Mapping;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddSdcMapping<T1, T2>(this IServiceCollection services)
        where T1 : class, IProfileLoader
        where T2 : class, IResourceLoader =>
        services
            .AddScoped<IProfileLoader, T1>()
            .AddScoped<IResourceLoader, T2>()
            .AddScoped<IExtractor, Extractor>()
            .AddScoped<IPopulator, Populator>()
            .AddScoped<IDependencyGraphGenerator, DependencyGraphGenerator>()
            .AddScoped<IScopeTreeCreator, ScopeTreeCreator>()
            .AddScoped<FhirPathMapping>()
            .AddScoped<INumericIdProvider, NumericIdProvider>();
}
