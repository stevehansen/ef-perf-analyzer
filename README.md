# Entity Framework Performance Analyzer
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer?ref=badge_shield)
[![CI](https://github.com/stevehansen/ef-perf-analyzer/actions/workflows/ci.yml/badge.svg)](https://github.com/stevehansen/ef-perf-analyzer/actions/workflows/ci.yml)

A Roslyn diagnostic analyzer that detects common performance issues in Entity Framework 6 and Entity Framework Core applications.

## Supported Diagnostics

### EFPERF001: Prefer Projection (warning)
Detects when only a subset of properties are used from a queried entity and suggests using projection to reduce data transfer.

```csharp
// Variable 'user' is only used for properties: Name, Email
var user = dbContext.Users.FirstOrDefault(u => u.Id == 1);
var name = user.Name;
var email = user.Email;

// Fix: project only the used properties
var user = dbContext.Users.Where(u => u.Id == 1).Select(it => new { it.Name, it.Email }).FirstOrDefault();
```

### EFPERF002: Use AsNoTracking for Read-Only Queries (suggestion)
Change tracking only pays off when the loaded entities are changed and saved. Reported when the member never saves, attaches, removes or writes to anything, and does not return the loaded entities.

```csharp
foreach (var user in dbContext.Users.ToList())
    Console.WriteLine(user.Name);

// Fix: skip change tracking
foreach (var user in dbContext.Users.AsNoTracking().ToList())
    Console.WriteLine(user.Name);
```

### EFPERF004: Client-Side Evaluation (warning)
Detects `Where`, `Skip` and `Take` right after `ToList`/`ToArray` on a query, which load every row and filter in memory.

```csharp
// 'Where' runs in memory over every row loaded by 'ToList'
var users = dbContext.Users.ToList().Where(u => u.IsActive);

// Fix: let the database filter
var users = dbContext.Users.Where(u => u.IsActive).ToList();
```

### EFPERF005: Use Async Query Methods (suggestion)
Detects synchronous query methods (`ToList`, `First`, `Count`, `Any`, ...) inside async methods and lambdas.

```csharp
public async Task<List<User>> GetUsersAsync()
{
    return dbContext.Users.ToList();
}

// Fix: await the async variant
public async Task<List<User>> GetUsersAsync()
{
    return await dbContext.Users.ToListAsync();
}
```

EFPERF002 and EFPERF005 are only reported when EF Core or EF6 is referenced, since their fixes use EF extension methods.

## Compatibility

- **Entity Framework**: EF6 and EF Core (tested with EF 6.5 and EF Core 8)
- **Hosts**: the analyzer targets `netstandard2.0` and Roslyn 3.11, so it runs in Visual Studio 2019 16.11+, Visual Studio 2022+, Rider and `dotnet build` with the .NET 5 SDK or later

## Install

```
dotnet add package EntityFrameworkPerformanceAnalyzer
```

Or in the Package Manager Console:

```
PM> Install-Package EntityFrameworkPerformanceAnalyzer
```

## Build

```
dotnet build
dotnet test
dotnet pack -c Release
```

## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer?ref=badge_large)
