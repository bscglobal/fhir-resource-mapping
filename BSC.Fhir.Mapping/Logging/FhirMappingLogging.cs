using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping.Logging;

public static class FhirMappingLogging
{
    public static ILoggerFactory LoggerFactory { private get; set; } = new DefaultLoggerFactory();

    public static ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }

    public static ILogger GetLogger(string? categoryName = null)
    {
        return LoggerFactory.CreateLogger(categoryName ?? "FhirMapping");
    }
}
