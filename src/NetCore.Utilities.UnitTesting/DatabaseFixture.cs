using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Logging;
using Respawn;
using Respawn.Graph;
using Xunit;
using Xunit.Abstractions;

namespace ICG.NetCore.Utilities.UnitTesting;

#nullable enable

/// <summary>
///     Contains logging settings for the <see cref="DatabaseFixture{TContext}"/>
/// </summary>
public class DatabaseFixtureLoggingSettings
{
    /// <summary>
    ///     Controls if the contexts created for inserts and queries
    ///     from test classes write to logs
    /// </summary>
    public bool HelperContexts { get; init; }

    /// <summary>
    /// Controls if contexts created for methods under test
    ///     write to logs
    /// </summary>
    public bool TestContexts { get; init; }

    /// <summary>
    ///     The minimum level to log.
    /// </summary>
    public LogLevel MinimumLevel { get; init; }

    /// <summary>
    ///     Default logging settings. Writes logs only from contexts used for tests
    /// </summary>
    public static readonly DatabaseFixtureLoggingSettings Default = new() { HelperContexts = false, TestContexts = true, MinimumLevel = LogLevel.Warning};

    /// <summary>
    ///     Disables logging
    /// </summary>
    public static readonly DatabaseFixtureLoggingSettings None = new() { HelperContexts = false, TestContexts = false, MinimumLevel = LogLevel.Warning };

    /// <summary>
    ///     Enables all logs
    /// </summary>
    public static readonly DatabaseFixtureLoggingSettings All = new() { HelperContexts = true, TestContexts = true, MinimumLevel = LogLevel.Warning };

    /// <summary>
    ///     Determines if two instances of <see cref="DatabaseFixtureLoggingSettings"/> are equal
    /// </summary>
    /// <param name="other">The other <see cref="DatabaseFixtureLoggingSettings"/></param>
    /// <returns>True if they are equal, false otherwise.</returns>
    protected bool Equals(DatabaseFixtureLoggingSettings other)
    {
        return HelperContexts == other.HelperContexts && TestContexts == other.TestContexts;
    }

    /// <summary>
    ///     Determines if another object is equal to this instance
    /// </summary>
    /// <param name="obj">The other object</param>
    /// <returns>True if they are equal, false otherwise</returns>
    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((DatabaseFixtureLoggingSettings)obj);
    }

    /// <inheritdoc />
    public override int GetHashCode()
    {
        return HashCode.Combine(HelperContexts, TestContexts);
    }

    internal bool LogEnabled(ContextType ct) => ct switch
    {
        ContextType.Unknown => true,
        ContextType.Helper => HelperContexts,
        ContextType.Test => TestContexts,
        _ => throw new ArgumentOutOfRangeException(nameof(ct), ct, null)
    };
}

/// <summary>
///     Possible types of a database context
/// </summary>
public enum ContextType
{
    /// <summary>
    ///     Not used, serves as a guard value in the enum
    /// </summary>
    Unknown,
    /// <summary>
    ///     The associated context is used as part of test setup or verification. 
    /// </summary>
    Helper,
    /// <summary>
    ///     The associated context is used by the method under test
    /// </summary>
    Test
}

/// <summary>
///     Provides a database fixture for xUnit tests that maintains and cleans a testing instance
///     of a MsSql database. It is important that this testing instance is not a real in-use database,
///     as all tables will be regularly truncated between test runs.
///
///     To use, inherit this class, specifying your own DbContext to use, and also a Collection Fixture
///     definition: https://xunit.net/docs/shared-context#collection-fixture
/// </summary>
/// <remarks>
///     Using the in-memory provider is strongly discouraged: 
///     https://docs.microsoft.com/en-us/ef/core/testing/choosing-a-testing-strategy#in-memory-as-a-database-fake
///     Sqlite brings its own problems with different case sensitivity and different migrations needed.
/// </remarks>
#pragma warning disable EF1001 // Internal EF Core API usage.
public abstract class DatabaseFixture<TContext>
#pragma warning restore EF1001 // Internal EF Core API usage.
    : IAsyncLifetime, IDbContextPool<TContext>, IDbContextFactory<TContext>
    where TContext : DbContext
{
    /// <summary>
    ///     The Checkpoint information used by Respawn to clear the database between test runs
    /// </summary>
    private readonly Checkpoint? _checkpoint;

    /// <summary>
    ///     Options used for creating all database contexts handed out by this fixture.
    /// </summary>
    private readonly DbContextOptions<TContext> _options;

    private WeakReference<ITestOutputHelper> _outputHelper = new(null!);
    private readonly DatabaseFixtureLoggingSettings _logSettings;

    /// <summary>
    ///     Creates an instance of the database fixture. Not normally called by testing code, the lifetime of this class is expected to be managed by xUnit.
    /// </summary>
    /// <param name="connectionString">The connection string to use for the DbContext</param>
    /// <param name="contextOptionsAction">
    ///     An action to set up the DbContextOptions used to create the context. If null, it will default to using Sql Server with the connection
    ///     string provided by the <paramref name="connectionString"/> parameter.
    /// </param>
    /// <param name="checkpointFunc">
    ///     A function returning a <see cref="Checkpoint"/> object. If null, a default configuration
    ///     that ignores the EF migrations table will be used.
    /// </param>
    /// <param name="logging">
    ///     A <see cref="DatabaseFixtureLoggingSettings"/> to control what type of contexts get logged.
    ///     If not provided, will default to <see cref="DatabaseFixtureLoggingSettings.Default"/>
    ///     To customize which events get logged, override <see cref="LogFilter"/>
    /// </param>
    protected DatabaseFixture(
        string connectionString, 
        Action<DbContextOptionsBuilder<TContext>>? contextOptionsAction = null, 
        Func<Checkpoint>? checkpointFunc = null, 
        DatabaseFixtureLoggingSettings? logging = null)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TContext>();
        
        _logSettings = logging ?? DatabaseFixtureLoggingSettings.Default;

        if (!_logSettings.Equals(DatabaseFixtureLoggingSettings.None))
        {
            optionsBuilder.LogTo(LogFilter, Logger);
        }

        if (contextOptionsAction == null)
        {
            optionsBuilder.UseSqlServer(connectionString);
        }
        else
        {
            contextOptionsAction(optionsBuilder);
        }
        
        _options = optionsBuilder.Options;

        var constructor = typeof(TContext).GetConstructor(new[] { typeof(DbContextOptions<TContext>) });
        if (constructor == null)
        {
            throw new InvalidOperationException($"The DbContext type used by the database fixture must have a public constructor that takes a DbContextOptions.");
        }

        if (checkpointFunc == null)
        {
            _checkpoint = new Checkpoint()
            {
                TablesToIgnore = new Table[] { "__EFMigrationsHistory" },
                DbAdapter = DbAdapter.SqlServer
            };
        }
        else
        {
            _checkpoint = checkpointFunc();
        }
    }

    /// <summary>
    ///     Filters logs from EF Core 
    /// </summary>
    /// <param name="eventId">The EF Core Event</param>
    /// <param name="logLevel">The log level</param>
    /// <returns>True if the event is to be logged, otherwise false.</returns>
    /// <remarks>
    ///     This default implementation will log any messages with a log level greater
    ///     than debug, excluding EF's various initialization messages. It will also
    ///     include logs for CommandExecuted that captures SQL issued by EF.
    /// </remarks>
    protected virtual bool LogFilter(EventId eventId, LogLevel logLevel)
    {
        if (eventId == CoreEventId.ContextInitialized) return false;
        if (logLevel >= _logSettings.MinimumLevel) return true;
        if (eventId == RelationalEventId.CommandExecuted) return true;
        return false;
    }

    /// <summary>
    ///     Writes EF event logs to the <see cref="ITestOutputHelper"/> provided by <see cref="SetOutputHelper"/>
    /// </summary>
    /// <param name="eventData">The event data from EF</param>
    /// <remarks>
    ///     If <see cref="SetOutputHelper"/> was not called, or if the instance it was provided has been
    ///     garbage collected, the events will not be logged.
    /// </remarks>
    private void Logger(EventData eventData)
    {
        if (!_outputHelper.TryGetTarget(out var target)) return;

        var contextTypeDisplay = "";
        var contextType = ContextType.Unknown;
        if (eventData is DbContextEventData {Context: { } ctx})
        {
            if (_contextTypes.TryGetValue(ctx.ContextId, out contextType))
            {
                contextTypeDisplay = $"[{contextType}]";
            }
        }

        if (_logSettings.LogEnabled(contextType))
        {
            var eventString = eventData.ToString();
            eventString = eventString.ReplaceLineEndings("\n    ");
            target.WriteLine($"[{XUnitLogger.LogLevelAsString(eventData.LogLevel)}]{contextTypeDisplay}[EntityFramework]\n    {eventString}\n");
        }
    }

    /// <summary>
    ///     Sets the <see cref="ITestOutputHelper"/> used for logging EF events.
    /// </summary>
    /// <param name="helper">The <see cref="ITestOutputHelper"/> to use</param>
    /// <remarks>
    ///     This class holds a weak reference to <paramref name="helper"/> as to not
    ///     interfere with xUnit's test lifecycle.
    /// </remarks>
    public void SetOutputHelper(ITestOutputHelper helper)
    {
        _outputHelper = new WeakReference<ITestOutputHelper>(helper);
    }

    /// <summary>
    ///     Clears the <see cref="ITestOutputHelper"/> used for logging.
    /// </summary>
    public void ClearOutputHelper()
    {
        _outputHelper.SetTarget(null!);
    }

    /// <summary>
    ///     Uses Respawn to clear the database
    /// </summary>
    /// <returns>
    ///     A task to await 
    /// </returns>
    public virtual async Task Reset()
    {
        var db = CreateDbContext(ContextType.Helper);
        var conn = db.Database.GetDbConnection();

        if (conn.State == ConnectionState.Closed)
            await conn.OpenAsync();

        if (_checkpoint != null)
            await _checkpoint.Reset(conn);

        await PostReset();
    }

    /// <summary>
    ///     Ensures that the database is migrated to the latest version,
    ///     and also resets it with Respawn.
    /// </summary>
    /// <remarks>Called by xUnit as part of the testing lifecycle</remarks>
    /// <returns>A Task to await</returns>
    public async Task InitializeAsync()
    {
        await CreateDatabase();
        await Reset();
    }

    public virtual async Task CreateDatabase()
    {
        var db = CreateDbContext(ContextType.Helper);
        await db.Database.MigrateAsync();
    }

    public virtual Task PostReset()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Part of xUnit's IAsyncLifetime contract
    /// </summary>
    /// <returns>A completed task</returns>
    public virtual Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    /// <summary>
    ///     Helper method to add and commit an entity into the testing database
    /// </summary>
    /// <remarks>
    ///     This method calls SaveChangesAsync on its own.
    /// </remarks>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entity">The entity to insert</param>
    /// <returns>A task to await</returns>
    public async Task InsertAsync<TEntity>(TEntity entity) where TEntity : class
    {
        await using var db = CreateDbContext(ContextType.Helper);
        db.Add(entity);
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Helper method to add and commit a list of entities into the testing database
    /// </summary>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entities">An enumerable of entities</param>
    /// <returns>A task to await</returns>
    public async Task InsertAsync<TEntity>(IEnumerable<TEntity> entities) where TEntity : class
    {
        await using var db = CreateDbContext(ContextType.Helper);
        await db.Set<TEntity>().AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Helper method to add and commit multiple entities into the testing database
    /// </summary>
    /// <remarks>
    ///     This method calls SaveChangesAsync on its own.
    /// </remarks>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="entities">The entities to insert</param>
    /// <returns>A task to await</returns>
    public async Task InsertAsync<TEntity>(params TEntity[] entities) where TEntity : class
    {
        await using var db = CreateDbContext(ContextType.Helper);
        await db.Set<TEntity>().AddRangeAsync(entities);
        await db.SaveChangesAsync();
    }

    /// <summary>
    ///     Helper method to fetch an entity from the database by its key field.
    /// </summary>
    /// <remarks>Will only work on entities with int ids. This was all that was needed for now.</remarks>
    /// <typeparam name="TEntity">The entity type</typeparam>
    /// <param name="keyValues">The key of the entity to fetch. For entities with composite keys, pass each as a parameter.</param>
    /// <returns>A task that when completed will contain the entity if it was found, or null if it wasn't</returns>
    public async Task<TEntity?> FindAsync<TEntity>(params object?[] keyValues) where TEntity : class
    {
        await using var db = CreateDbContext(ContextType.Helper);
        return await db.Set<TEntity>().FindAsync(keyValues);
    }

    /// <summary>
    ///     Helper method to execute an arbitrary function taking a <typeparamref name="TContext"/> as a parameter, returning some value.
    /// </summary>
    /// <typeparam name="TResult">The data type to return from <paramref name="action"/></typeparam>
    /// <param name="action">A function that takes a <typeparamref name="TContext"/> as a parameter, and returns <typeparamref name="TResult"/></param>
    /// <returns>A task that returns the <typeparamref name="TResult"/> from <paramref name="action"/></returns>
    public async Task<TResult> ExecuteAsync<TResult>(Func<TContext, Task<TResult>> action)
    {
        await using var context = CreateDbContext(ContextType.Helper);
        return await action(context);
    }

    /// <summary>
    ///     Helper method to execute an arbitrary function taking a <typeparamref name="TContext"/> returning nothing
    /// </summary>
    /// <param name="action">A function that takes a <typeparamref name="TContext"/> as a parameter, and returns a <see cref="Task"/></param>
    /// <returns>An awaitable task</returns>
    public async Task ExecuteAsync(Func<TContext, Task> action)
    {
        await using var context = CreateDbContext(ContextType.Helper);
        await action(context);
    }

    // Here be dragons 🐲🐉
#pragma warning disable EF1001 // Internal EF Core API usage.

    /*
     * This section hijacks the IDbContextPoolable interface on the DbContext and
     * the IDbContextPool interface on this test fixture to get something resembling
     * lifecycle hooks on an arbitrary DbContext. Both of these interfaces are internal
     * to EF Core, hence the disabled warnings.
     *
     * This is used to maintain a lookup table between DbContexts generated by this
     * fixture and how they are meant to be used in order to provide additional log
     * annotations and filtering. This could have more uses in the future (associating
     * a context with an ITestOutputHelper, to cut back on potential race conditions if
     * tests are run in parallel).
     *
     * When a DbContext has SetLease called on it, when the context is being disposed
     * it will call ContextDisposed on the lease, which in turn calls ReturnAsync on
     * the IDbContextPool which created the lease (which is this fixture).
     *
     * So, when creating a DbContext to hand out, we assign it a lease and then put
     * the ContextId of the context into a dictionary. When the lease is returned
     * when the context is disposing, the ContextId is removed from the dictionary.
     */

    private readonly ConcurrentDictionary<DbContextId, ContextType> _contextTypes = new();

    /// <summary>
    ///     Creates a new DbContext
    /// </summary>
    /// <remarks>
    ///     This method is used internally by EF to attempt to return an existing DbContext
    ///     instance from a pool of created ones. Since this is just abusing the pool interface,
    ///     this always just creates a new one.
    /// </remarks>
    /// <returns>A new DbContext of type <typeparamref name="TContext"/></returns>
    public IDbContextPoolable Rent() => (Activator.CreateInstance(typeof(TContext), _options) as IDbContextPoolable)!;

    /// <summary>
    ///     Called by DbContexts when their lifetime expires.
    /// </summary>
    /// <param name="context">The context that is expiring</param>
    public void Return(IDbContextPoolable context)
    {
        if (context is not TContext ctx) return;

        // Don't really care if it can't be removed from the dictionary, or what the value was
        _contextTypes.TryRemove(ctx.ContextId, out var _);
    }

    /// <summary>
    ///     Called by DbContexts when their lifetime expires.
    /// </summary>
    /// <param name="context">The context that is expiring</param>
    /// <param name="cancellationToken">A cancellation token</param>
    /// <returns>A completed task, since this method does no async work</returns>
    public ValueTask ReturnAsync(IDbContextPoolable context, CancellationToken cancellationToken = default)
    {
        Return(context);
        return ValueTask.CompletedTask;
    }

    /// <summary>
    ///     Creates a new <typeparamref name="TContext"/> with the specified context type
    /// </summary>
    /// <param name="contextType">The context type to assign to the new <typeparamref name="TContext"/></param>
    /// <returns>A new <typeparamref name="TContext"/></returns>
    public TContext CreateDbContext(ContextType contextType)
    {
        return CreateDbContext(contextType, null);
    }

    /// <summary>
    ///     Creates a new <typeparamref name="TContext"/> with the specified context type
    /// </summary>
    /// <param name="contextType">The context type to assign to the new <typeparamref name="TContext"/></param>
    /// <param name="interceptors">A list of interceptors, or null for no interceptors</param>
    /// <returns>A new <typeparamref name="TContext"/></returns>
    public TContext CreateDbContext(ContextType contextType, IEnumerable<IDbCommandInterceptor>? interceptors)
    {
        DbContextLease lease;
        if (interceptors == null)
        {
            lease = new DbContextLease(this, true);
        }
        else
        {
            var builder = new DbContextOptionsBuilder(_options);
            builder.AddInterceptors(interceptors);
            lease = new DbContextLease(new CustomContextPool<TContext>(Return, builder.Options), true);
        }
        var dbcp = lease.Context;
        dbcp.SnapshotConfiguration();
        dbcp.SetLease(lease);

        var ctx = (TContext)dbcp;

        _contextTypes.TryAdd(ctx.ContextId, contextType);

        return ctx;
    }
#pragma warning restore EF1001 // Internal EF Core API usage.

    /// <summary>
    ///     Creates a <typeparamref name="TContext"/> that is marked for use for the class under test.
    /// </summary>
    /// <returns>A new <typeparamref name="TContext"/></returns>
    public TContext CreateDbContext() => CreateDbContext(ContextType.Test);

    /// <summary>
    ///     Creates a <typeparamref name="TContext"/> that has interceptors attached to it to modify queries and their results
    /// </summary>
    /// <param name="interceptors">A list of interceptors to attach</param>
    /// <returns>A new <typeparamref name="TContext"/> with the specified interceptors</returns>
    public TContext CreateInterceptedDbContext(params IDbCommandInterceptor[] interceptors) => CreateDbContext(ContextType.Test, interceptors);
}

/// <summary>
///     Will cause commands matching the specified predicate to fail
/// </summary>
public class FailCommandInterceptor : DbCommandInterceptor
{
    private readonly Func<DbCommand, bool> _predicate;
    private readonly Exception _ex;

    /// <summary>
    ///     Creates a <see cref="FailCommandInterceptor"/>
    /// </summary>
    /// <param name="predicate">A predicate taking a DbCommand that returns true if the command should be failed</param>
    /// <param name="ex">An exception to throw when the predicate is matched. If null, it will throw a plain Exception with a message</param>
    public FailCommandInterceptor(Func<DbCommand, bool> predicate, Exception? ex = null)
    {
        _predicate = predicate;
        _ex = ex ?? new Exception("Made to fail by FailCommandInterceptor");
    }

    /// <inheritdoc />
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result)
    {
        if (_predicate(command))
            throw _ex;

        return base.ReaderExecuting(command, eventData, result);
    }

    /// <inheritdoc />
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = new CancellationToken())
    {
        if (_predicate(command))
            throw _ex;

        return base.ReaderExecutingAsync(command, eventData, result, cancellationToken);
    }

    /// <summary>
    /// Creates a <see cref="FailCommandInterceptor"/> that causes INSERT statements to fail
    /// </summary>
    /// <param name="customException">A custom exception to throw, if null, it will throw a plain Exception with a message</param>
    /// <returns> A new <see cref="FailCommandInterceptor"/></returns>
    public static FailCommandInterceptor FailInserts(Exception? customException = null)
        => new(c => c.CommandText.StartsWith("INSERT", StringComparison.OrdinalIgnoreCase), customException);

    /// <summary>
    /// Creates a <see cref="FailCommandInterceptor"/> that causes UPDATE statements to fail
    /// </summary>
    /// <param name="customException">A custom exception to throw, if null, it will throw a plain Exception with a message</param>
    /// <returns> A new <see cref="FailCommandInterceptor"/></returns>
    public static FailCommandInterceptor FailUpdates(Exception? customException = null)
        => new(c => c.CommandText.StartsWith("UPDATE", StringComparison.OrdinalIgnoreCase), customException);

    /// <summary>
    /// Creates a <see cref="FailCommandInterceptor"/> that causes MERGE statements to fail
    /// </summary>
    /// <param name="customException">A custom exception to throw, if null, it will throw a plain Exception with a message</param>
    /// <returns> A new <see cref="FailCommandInterceptor"/></returns>
    public static FailCommandInterceptor FailMerges(Exception? customException = null)
        => new(c => c.CommandText.StartsWith("MERGE", StringComparison.OrdinalIgnoreCase), customException);

    /// <summary>
    /// Creates a <see cref="FailCommandInterceptor"/> that causes DELETE statements to fail
    /// </summary>
    /// <param name="customException">A custom exception to throw, if null, it will throw a plain Exception with a message</param>
    /// <returns> A new <see cref="FailCommandInterceptor"/></returns>
    public static FailCommandInterceptor FailDeletes(Exception? customException = null)
        => new(c => c.CommandText.StartsWith("DELETE", StringComparison.OrdinalIgnoreCase), customException);
}

// Here be dragons 🐲🐉
#pragma warning disable EF1001 // Internal EF Core API usage.
internal class CustomContextPool<TContext> : IDbContextPool<TContext>
where TContext: DbContext
{
    private readonly Action<IDbContextPoolable> _returnCallback;
    private readonly DbContextOptions _options;

    public CustomContextPool(Action<IDbContextPoolable> returnCallback, DbContextOptions options)
    {
        _returnCallback = returnCallback;
        _options = options;
    }

    public IDbContextPoolable Rent()
        => (Activator.CreateInstance(typeof(TContext), _options) as IDbContextPoolable)!;

    public void Return(IDbContextPoolable context)
        => _returnCallback(context);

    public ValueTask ReturnAsync(IDbContextPoolable context, CancellationToken cancellationToken = new CancellationToken())
    {
        _returnCallback(context);
        return ValueTask.CompletedTask;
    }
}
#pragma warning restore EF1001