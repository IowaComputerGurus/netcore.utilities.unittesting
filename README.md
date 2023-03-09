# netcore.utilities.unittesting ![](https://img.shields.io/github/license/iowacomputergurus/netcore.utilities.unittesting.svg)

![Build Status](https://github.com/IowaComputerGurus/netcore.utilities.unittesting/actions/workflows/ci-build.yml/badge.svg)

![](https://img.shields.io/nuget/v/icg.netcore.utilities.unittesting.svg) ![](https://img.shields.io/nuget/dt/icg.netcore.utilities.unittesting.svg)

This library provides helpful items to speed the development of unit tests across all .NET Core project types.  We will update this library regularly with helpful base classes/implementations.

## Using ICG.NetCore.Utilities.UnitTesting

### Installation

Install from NuGet

``` powershell
Install-Package ICG.NetCore.Utilities.UnitTesting
```
### Register Dependencies (If using Dependency Injection)

Inside of of your project's Startup.cs within the RegisterServices method add this line of code.

``` csharp
services.UseIcgUnitTestUtilities();
```

### Included Features

| Object | Purpose |
| ---- | --- |
| AbstractDataServiceTest | Provides an abstract class that will build the proper options for an EF In Memory Database Provider|
| AbstractModelTest | Provides an abstract class that contains helpful items for writing unit tests for model objects | 
| SampleDataGenerator | Provides a utility for generating sample strings, dates, and the like for building unit tests, can be used with DI or standard creation |
| XUnitLogger | Provides an implementation of `ILogger` that writes to xUnit's [`ITestOutputHelper`](https://xunit.net/docs/capturing-output) |
| DatabaseFixture | A test fixture with helper methods for setup and verification that allows tests to be run against a SQL database. Uses [Respawn](https://github.com/jbogard/Respawn) to reset the database between test runs |
Detailed information can be found in the XML Comment documentation for the objects, we are working to add to this document as well.

## Related Projects

ICG has a number of other related projects as well

* [AspNetCore.Utilities](https://www.github.com/iowacomputergurus/aspnetcore.utilities)
* [NetCore.Utilities.Spreadsheet](https://www.github.com/iowacomputergurus/netcore.utilities.spreadsheet)
* [NetCore.Utilities.Email](https://www.github.com/iowacomputergurus/netcore.utilities.email)