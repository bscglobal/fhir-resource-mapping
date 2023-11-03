using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace BSC.Fhir.Mapping.Tests.Mocks;

public class TestLoggerFactory : ILoggerFactory
{
    private readonly ITestOutputHelper _output;

    public TestLoggerFactory(ITestOutputHelper output)
    {
        _output = output;
    }

    public void AddProvider(ILoggerProvider provider) { }

    public ILogger CreateLogger(string categoryName)
    {
        return new TestLogger(_output);
    }

    public void Dispose() { }
}
