using System;
using System.Diagnostics;
using System.Threading;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace ICG.NetCore.Utilities.UnitTesting;
#nullable enable

/// <summary>
/// A basic implementation of ILogger that sends logs to xUnit test output
/// </summary>
public class XUnitLogger : ILogger
{
    private readonly ITestOutputHelper _outputHelper;
    private readonly IExternalScopeProvider _scopeProvider;
    private readonly string _categoryName;

    /// <summary>
    /// The minimum log level to capture. Can be changed any time.
    /// </summary>
    public LogLevel MinimumLevel { get; set; }

    /// <summary>
    /// Creates a new instance of <see cref="XUnitLogger"/>
    /// </summary>
    /// <param name="outputHelper">The <see cref="ITestOutputHelper"/></param>
    /// <param name="scopeProvider">A scope provider. If none is provided, a new one will be created</param>
    /// <param name="minimumLevel">The minimum log level to capture. Defaults to <see cref="LogLevel.Debug"/></param>
    /// <param name="categoryName">The logging category to use</param>
    public XUnitLogger(ITestOutputHelper outputHelper, IExternalScopeProvider? scopeProvider = null, LogLevel minimumLevel = LogLevel.Debug, string categoryName = "")
    {
        MinimumLevel = minimumLevel;
        _outputHelper = outputHelper;
        _scopeProvider = scopeProvider ?? new TimedExternalScopeProvider(this);
        _categoryName = categoryName;
    }

    /// <inheritdoc />
    public IDisposable BeginScope<TState>(TState state) => _scopeProvider.Push(state);

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= MinimumLevel;

    /// <inheritdoc />
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
    {
        if (logLevel < MinimumLevel) return;

        var baseString = $"[{LogLevelAsString(logLevel)}][{_categoryName}] {formatter(state, exception)}";
        if (exception != null)
        {
            baseString += exception + "\n";
        }

        _scopeProvider.ForEachScope((scope, s) => s += $"\n{scope}", baseString);

        _outputHelper.WriteLine(baseString);
    }

    /// <summary>
    /// Maps a <see cref="LogLevel"/> to a short string
    /// </summary>
    /// <param name="level">The log level to map</param>
    /// <returns>A string representation of the log level</returns>
    /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="level"/> is not a valid <see cref="LogLevel"/></exception>
    internal static string LogLevelAsString(LogLevel level) => level switch
    {
        LogLevel.Trace => "trace",
        LogLevel.Debug => "debug",
        LogLevel.Information => "info ",
        LogLevel.Warning => "warn ",
        LogLevel.Error => "error",
        LogLevel.Critical => "crit ",
        LogLevel.None => "none ",
        _ => throw new ArgumentOutOfRangeException(nameof(level), level, null)
    };
}

/// <summary>
/// A basic implementation of ILogger that sends logs to xUnit test output with the category
/// name set to <typeparamref name="TCategoryName"/>.
/// </summary>
/// <typeparam name="TCategoryName">The type whose name is used for the logger category name.</typeparam>
public class XUnitLogger<TCategoryName> : XUnitLogger, ILogger<TCategoryName>
{
    /// <inheritdoc/>
    /// <summary>
    /// Creates a new instance of <see cref="XUnitLogger{TCategoryName}"/> using the name of
    /// <typeparamref name="TCategoryName"/> for the category name
    /// </summary>
    public XUnitLogger(ITestOutputHelper outputHelper, LoggerExternalScopeProvider? scopeProvider = null, LogLevel minimumLevel = LogLevel.Debug) : base(outputHelper, scopeProvider, minimumLevel, typeof(TCategoryName).Name)
    {
    }
}

/// <summary>
/// Extension methods to help create <see cref="XUnitLogger"/> instances
/// </summary>
public static class TestOutputHelperExtensions
{
    /// <summary>
    /// Creates a new instance of <see cref="XUnitLogger"/> from an <see cref="ITestOutputHelper"/>
    /// </summary>
    /// <param name="helper">The <see cref="ITestOutputHelper"/></param>
    /// <returns>A new instance of XUnitLogger</returns>
    public static ILogger AsLogger(this ITestOutputHelper helper)
        => new XUnitLogger(helper);

    /// <summary>
    /// Creates a new instance of <see cref="XUnitLogger"/> from an <see cref="ITestOutputHelper"/> with
    /// the category set to <typeparamref name="TCategoryName"/>.
    /// </summary>
    /// <typeparam name="TCategoryName">The type whose name is used for the logger category name.</typeparam>
    /// <param name="helper">The <see cref="ITestOutputHelper"/></param>
    /// <returns>A new instance of XUnitLogger</returns>
    public static ILogger<TCategoryName> AsLogger<TCategoryName>(this ITestOutputHelper helper)
        => new XUnitLogger<TCategoryName>(helper);

    /// <summary>
    /// Creates a new instance of <see cref="XUnitLogger"/> from an <see cref="ITestOutputHelper"/> that will
    /// only output messages with a <see cref="LogLevel"/> greater than <paramref name="minimumLevel"/>
    /// </summary>
    /// <param name="helper">The <see cref="ITestOutputHelper"/></param>
    /// <param name="minimumLevel">The minimum level of log messages to capture</param>
    /// <returns>A new instance of XUnitLogger</returns>
    public static ILogger AsLogger(this ITestOutputHelper helper, LogLevel minimumLevel)
        => new XUnitLogger(helper, minimumLevel: minimumLevel);

    /// <summary>
    /// Creates a new instance of <see cref="XUnitLogger"/> from an <see cref="ITestOutputHelper"/>
    /// with the category set to <typeparamref name="TCategoryName"/> that will
    /// only output messages with a <see cref="LogLevel"/> greater than <paramref name="minimumLevel"/>
    /// </summary>
    /// <typeparam name="TCategoryName">The type whose name is used for the logger category name.</typeparam>
    /// <param name="helper">The <see cref="ITestOutputHelper"/></param>
    /// <param name="minimumLevel">The minimum level of log messages to capture</param>
    /// <returns>A new instance of XUnitLogger</returns>
    public static ILogger<TCategoryName> AsLogger<TCategoryName>(this ITestOutputHelper helper, LogLevel minimumLevel)
        => new XUnitLogger<TCategoryName>(helper, minimumLevel: minimumLevel);
}

/// <summary>
/// Lifted mostly from
/// https://github.com/dotnet/runtime/blob/main/src/libraries/Microsoft.Extensions.Logging.Abstractions/src/LoggerExternalScopeProvider.cs
/// Modified to log time taken in a scope to the logs.
/// </summary>
public class TimedExternalScopeProvider : IExternalScopeProvider
{
    private readonly AsyncLocal<Scope?> _currentScope = new();
    private readonly ILogger _logger;
    /// <summary>
    /// Creates a new <see cref="LoggerExternalScopeProvider"/>.
    /// </summary>
    public TimedExternalScopeProvider(ILogger logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public void ForEachScope<TState>(Action<object?, TState> callback, TState state)
    {
        void Report(Scope? current)
        {
            if (current == null)
            {
                return;
            }
            Report(current.Parent);
            callback(current.State, state);
        }
        Report(_currentScope.Value);
    }

    /// <inheritdoc />
    public IDisposable Push(object? state)
    {
        Scope? parent = _currentScope.Value;
        var newScope = new Scope(this, state, parent, _logger);
        _currentScope.Value = newScope;

        return newScope;
    }

    private sealed class Scope : IDisposable
    {
        private readonly TimedExternalScopeProvider _provider;
        private bool _isDisposed;
        private readonly Stopwatch _stopwatch = new();
        private readonly ILogger _logger;

        internal Scope(TimedExternalScopeProvider provider, object? state, Scope? parent, ILogger logger)
        {
            _provider = provider;
            State = state;
            Parent = parent;
            _logger = logger;
            _stopwatch.Start();
        }

        public Scope? Parent { get; }

        public object? State { get; }

        public override string? ToString()
        {
            return $"Timing '{State}': {(_stopwatch.IsRunning ? "Running" : "Completed")}, {_stopwatch.ElapsedMilliseconds}ms";
        }

        public void Dispose()
        {
            if (_isDisposed) return;
            _stopwatch.Stop();
            _provider._currentScope.Value = Parent;
            _isDisposed = true;

            _logger.LogDebug("Scope '{state}' completed in {timeMs}ms", State?.ToString(), _stopwatch.ElapsedMilliseconds);
        }
    }
}
