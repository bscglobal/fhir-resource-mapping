using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping.Logging;

public static class FhirMappingLogging
{
    public static ILoggerFactory LoggerFactory { private get; set; } = new DefaultLoggerFactory();
    private static readonly Dictionary<Type, ILogger> _loggers = new();

    public static ILogger<T> GetLogger<T>()
    {
        return LoggerFactory.CreateLogger<T>();
    }
}
