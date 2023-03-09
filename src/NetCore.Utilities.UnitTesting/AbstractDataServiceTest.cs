using Microsoft.EntityFrameworkCore;

namespace ICG.NetCore.Utilities.UnitTesting;

/// <summary>
///     An abstract class that can be utilized as a base for unit testing where you need to instantiate a Memory DB Context
/// </summary>
public abstract class AbstractDataServiceTest
{
    /// <summary>
    ///     Creates a set of DB Context Options for in memory usage with the specified name
    /// </summary>
    /// <typeparam name="T">The DbContext Type</typeparam>
    /// <param name="dbName">The targeted name for the in-memory database</param>
    /// <returns></returns>
    public DbContextOptions<T> BuildMemoryDbOptions<T>(string dbName) where T : DbContext
    {
        return new DbContextOptionsBuilder<T>().UseInMemoryDatabase(dbName).Options;
    }
}