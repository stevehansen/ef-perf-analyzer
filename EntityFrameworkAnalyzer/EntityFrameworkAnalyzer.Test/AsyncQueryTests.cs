using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFrameworkAnalyzer.Test
{
    [TestClass]
    public class AsyncQueryTests
    {
        [TestMethod]
        public async Task FixToListEFCore()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public async Task<List<Person>> AllAsync()
    {
        await Task.Yield();
        return db.Set<Person>().{|EFPERF005:ToList|}();
    }
}";
            var fixtest = @"
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public async Task<List<Person>> AllAsync()
    {
        await Task.Yield();
        return await db.Set<Person>().ToListAsync();
    }
}";
            await Verify.CodeFixAsync<EFPERF005CodeFixProvider>(References.EFCore, test, fixtest);
        }

        [TestMethod]
        public async Task FixParenthesizesMemberAccess()
        {
            var test = @"
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public async Task<string> FirstNameAsync()
    {
        await Task.Yield();
        return db.Set<Person>().{|EFPERF002:{|EFPERF005:FirstOrDefault|}|}()?.Name;
    }
}";
            var fixtest = @"
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public async Task<string> FirstNameAsync()
    {
        await Task.Yield();
        return (await db.Set<Person>().{|EFPERF002:FirstOrDefaultAsync|}())?.Name;
    }
}";
            await Verify.CodeFixAsync<EFPERF005CodeFixProvider>(References.EFCore, test, fixtest);
        }

        [TestMethod]
        public async Task FixAddsUsingEF6()
        {
            var test = @"
using System.Linq;
using System.Threading.Tasks;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly System.Data.Entity.DbContext db = null;

    public async Task<bool> AnyAsync()
    {
        await Task.Yield();
        return db.Set<Person>().{|EFPERF005:Any|}();
    }
}";
            var fixtest = @"
using System.Linq;
using System.Threading.Tasks;
using System.Data.Entity;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly System.Data.Entity.DbContext db = null;

    public async Task<bool> AnyAsync()
    {
        await Task.Yield();
        return await db.Set<Person>().AnyAsync();
    }
}";
            await Verify.CodeFixAsync<EFPERF005CodeFixProvider>(References.EF6, test, fixtest);
        }

        [TestMethod]
        public async Task DontSuggestOutsideAsyncFunction()
        {
            var test = @"
using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public int Count() => db.Set<Person>().Count();

    public async Task<int> CountLaterAsync()
    {
        await Task.Yield();
        Func<int> count = () => db.Set<Person>().Count();
        return db.Set<Person>().{|EFPERF005:Count|}(p => db.Set<Person>().Any()) + count();
    }
}";
            await Verify.AnalyzerAsync(References.EFCore, test);
        }

        [TestMethod]
        public async Task DontSuggestWithoutEntityFramework()
        {
            var test = @"
using System.Linq;
using System.Threading.Tasks;

class People
{
    private readonly IQueryable<string> names = null;

    public async Task<int> CountAsync()
    {
        await Task.Yield();
        return names.Count();
    }
}";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }
    }
}
