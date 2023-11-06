using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace BSC.Fhir.Mapping.Tests.Mocks;

public class TestLogger<T> : TestLogger, ILogger<T>
{
    public TestLogger(ITestOutputHelper output)
        : base(output) { }
}

public class TestLogger : ILogger
{
    public class Scope : IDisposable
    {
        public void Dispose() { }
    }

    private readonly ITestOutputHelper _output;

    public TestLogger(ITestOutputHelper output)
    {
        _output = output;
    }

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull
    {
        return new Scope();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return true;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter
    )
    {
        _output.WriteLine("{0}: {1}", logLevel.ToString(), formatter(state, exception));
    }
}
