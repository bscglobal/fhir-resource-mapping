using Microsoft.Extensions.Logging;

namespace BSC.Fhir.Mapping.Logging;

public class DefaultLoggerFactory : ILoggerFactory
{
    public void AddProvider(ILoggerProvider provider) { }

    public ILogger CreateLogger(string categoryName)
    {
        return new DefaultLogger();
    }

    public void Dispose() { }
}
