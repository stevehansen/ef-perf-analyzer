using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFrameworkAnalyzer.Test
{
    [TestClass]
    public class AsNoTrackingTests
    {
        [TestMethod]
        public async Task FixReadOnlyQueryEFCore()
        {
            var test = @"
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public void Print()
    {
        var people = db.Set<Person>().Where(p => p.Id > 0).{|EFPERF002:ToList|}();
        foreach (var person in people)
            Console.WriteLine(person.Name);
    }
}";
            var fixtest = @"
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

class Person
{
    public int Id { get; set; }
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public void Print()
    {
        var people = db.Set<Person>().Where(p => p.Id > 0).AsNoTracking().ToList();
        foreach (var person in people)
            Console.WriteLine(person.Name);
    }
}";
            await Verify.CodeFixAsync<EFPERF002CodeFixProvider>(References.EFCore, test, fixtest);
        }

        [TestMethod]
        public async Task FixAsyncQueryEFCore()
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

    public async Task PrintAsync()
    {
        foreach (var person in await db.Set<Person>().{|EFPERF002:ToListAsync|}())
            Console.WriteLine(person.Name);
    }
}";
            var fixtest = @"
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

    public async Task PrintAsync()
    {
        foreach (var person in await db.Set<Person>().AsNoTracking().ToListAsync())
            Console.WriteLine(person.Name);
    }
}";
            await Verify.CodeFixAsync<EFPERF002CodeFixProvider>(References.EFCore, test, fixtest);
        }

        [TestMethod]
        public async Task FixAddsUsingEF6()
        {
            var test = @"
using System;
using System.Linq;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly System.Data.Entity.DbContext db = null;

    public void Print()
    {
        foreach (var person in db.Set<Person>().OrderBy(p => p.Name).{|EFPERF002:ToList|}())
            Console.WriteLine(person.Name);
    }
}";
            var fixtest = @"
using System;
using System.Linq;
using System.Data.Entity;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly System.Data.Entity.DbContext db = null;

    public void Print()
    {
        foreach (var person in db.Set<Person>().OrderBy(p => p.Name).AsNoTracking().ToList())
            Console.WriteLine(person.Name);
    }
}";
            await Verify.CodeFixAsync<EFPERF002CodeFixProvider>(References.EF6, test, fixtest);
        }

        [TestMethod]
        public async Task DontSuggestWhenSaved()
        {
            var test = @"
using System.Linq;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public void Rename(string name)
    {
        foreach (var person in db.Set<Person>().ToList())
            Rename(person, name);
        db.SaveChanges();
    }

    private static void Rename(Person person, string name) { }
}";
            await Verify.AnalyzerAsync(References.EFCore, test);
        }

        [TestMethod]
        public async Task DontSuggestWhenMemberWritten()
        {
            var test = @"
using System.Linq;
using Microsoft.EntityFrameworkCore;

class Person
{
    public bool Active { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public void Deactivate()
    {
        foreach (var person in db.Set<Person>().ToList())
            person.Active = false;
    }
}";
            await Verify.AnalyzerAsync(References.EFCore, test);
        }

        [TestMethod]
        public async Task DontSuggestWhenReturned()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public List<Person> All() => db.Set<Person>().ToList();

    public Person[] Named(string name)
    {
        var people = db.Set<Person>().Where(p => p.Name == name).ToArray();
        return people;
    }
}";
            await Verify.AnalyzerAsync(References.EFCore, test);
        }

        [TestMethod]
        public async Task DontSuggestWhenAlreadyNoTracking()
        {
            var test = @"
using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly DbContext db = null;

    public void Print()
    {
        var query = db.Set<Person>().AsNoTracking();
        foreach (var person in query.Where(p => p.Name != null).ToList())
            Console.WriteLine(person.Name);
        foreach (var name in db.Set<Person>().Select(p => p.Name).ToList())
            Console.WriteLine(name);
    }
}";
            await Verify.AnalyzerAsync(References.EFCore, test);
        }

        [TestMethod]
        public async Task DontSuggestWithoutEntityFramework()
        {
            var test = @"
using System;
using System.Linq;

class Person
{
    public string Name { get; set; }
}

class People
{
    private readonly IQueryable<Person> people = null;

    public void Print()
    {
        foreach (var person in people.ToList())
            Console.WriteLine(person.Name);
    }
}";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }
    }
}
