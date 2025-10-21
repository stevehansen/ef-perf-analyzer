# Entity Framework Performance Analyzer
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer.svg?type=shield)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer?ref=badge_shield)

A Roslyn diagnostic analyzer that detects common performance issues in Entity Framework 6 and Entity Framework Core applications.

## Features

This analyzer helps you write more efficient Entity Framework queries by detecting common performance anti-patterns and suggesting fixes.

### Supported Diagnostics

#### EFPERF001: Prefer Projection
Detects when only a subset of properties are used from a queried entity and suggests using projection to reduce data transfer.

**Example:**
```csharp
// Warning: Variable 'user' is only used for properties: Name, Email
var user = dbContext.Users.FirstOrDefault(u => u.Id == 1);
var name = user.Name;
var email = user.Email;

// Fix: Use projection
var user = dbContext.Users
    .Select(u => new { u.Name, u.Email })
    .FirstOrDefault(u => u.Id == 1);
```

#### EFPERF002: Use AsNoTracking for Read-Only Queries (EF Core)
Detects read-only queries that should use `AsNoTracking()` for better performance.

**Example:**
```csharp
// Warning: Query should use AsNoTracking() for better performance
var users = dbContext.Users.ToList();

// Fix: Add AsNoTracking
var users = dbContext.Users.AsNoTracking().ToList();
```

#### EFPERF003: Potential N+1 Query (Future Enhancement)
Detects navigation property access in loops that may cause N+1 query problems.

#### EFPERF004: Client-Side Evaluation
Detects LINQ operations that occur after materialization (ToList/ToArray), causing client-side evaluation.

**Example:**
```csharp
// Warning: Filter operation 'Where' occurs after ToList - move filtering before materialization
var users = dbContext.Users.ToList().Where(u => u.IsActive);

// Fix: Filter before materialization
var users = dbContext.Users.Where(u => u.IsActive).ToList();
```

#### EFPERF005: Use Async Query Methods
Detects synchronous query methods in async contexts and suggests using async variants for better scalability.

**Example:**
```csharp
// Warning in async method: Method 'ToList' should use async variant 'ToListAsync'
public async Task<List<User>> GetUsersAsync()
{
    return dbContext.Users.ToList(); // Synchronous call
}

// Fix: Use async variant
public async Task<List<User>> GetUsersAsync()
{
    return await dbContext.Users.ToListAsync();
}
```

## Compatibility

- **Entity Framework 6**: Full support
- **Entity Framework Core**: Full support (tested with EF Core 8.0)
- **Target Framework**: .NET Framework 4.8

## Install

Install via NuGet Package Manager Console:

```
PM> Install-Package EntityFrameworkPerformanceAnalyzer
```

Or via .NET CLI:

```
dotnet add package EntityFrameworkPerformanceAnalyzer
```

## How It Works

The analyzer uses Roslyn to analyze your C# code at compile-time and detects common Entity Framework performance anti-patterns. When issues are detected, you'll see warnings in Visual Studio with code fixes that can be applied automatically.

## Contributing

Contributions are welcome! Please feel free to submit pull requests or open issues for bugs and feature requests.

## License
[![FOSSA Status](https://app.fossa.io/api/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer.svg?type=large)](https://app.fossa.io/projects/git%2Bgithub.com%2Fstevehansen%2Fef-perf-analyzer?ref=badge_large)