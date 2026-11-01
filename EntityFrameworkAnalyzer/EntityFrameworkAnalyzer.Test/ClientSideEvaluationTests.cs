using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFrameworkAnalyzer.Test
{
    [TestClass]
    public class ClientSideEvaluationTests
    {
        [TestMethod]
        public async Task FixWhereAfterToList()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;

class Person
{
    public int Age { get; set; }
}

class People
{
    private readonly IQueryable<Person> people = null;

    public IEnumerable<Person> Adults()
    {
        return people.ToList().{|EFPERF004:Where|}(p => p.Age >= 18);
    }
}";
            var fixtest = @"
using System.Collections.Generic;
using System.Linq;

class Person
{
    public int Age { get; set; }
}

class People
{
    private readonly IQueryable<Person> people = null;

    public IEnumerable<Person> Adults()
    {
        return people.Where(p => p.Age >= 18).ToList();
    }
}";
            await Verify.CodeFixAsync<EFPERF004CodeFixProvider>(References.NoEF, test, fixtest);
        }

        [TestMethod]
        public async Task FixKeepsMultiLineLayout()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;

class Person
{
    public int Age { get; set; }
}

class People
{
    private readonly IQueryable<Person> people = null;

    public IEnumerable<Person> Oldest()
    {
        return people
            .OrderByDescending(p => p.Age)
            .ToArray()
            .{|EFPERF004:Take|}(10);
    }
}";
            var fixtest = @"
using System.Collections.Generic;
using System.Linq;

class Person
{
    public int Age { get; set; }
}

class People
{
    private readonly IQueryable<Person> people = null;

    public IEnumerable<Person> Oldest()
    {
        return people
            .OrderByDescending(p => p.Age)
            .Take(10)
            .ToArray();
    }
}";
            await Verify.CodeFixAsync<EFPERF004CodeFixProvider>(References.NoEF, test, fixtest);
        }

        [TestMethod]
        public async Task NoFixForStatementLambda()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;

class Person
{
    public int Age { get; set; }
}

class People
{
    private readonly IQueryable<Person> people = null;

    public IEnumerable<Person> Adults()
    {
        return people.ToList().{|EFPERF004:Where|}(p => { return p.Age >= 18; });
    }
}";
            await Verify.CodeFixAsync<EFPERF004CodeFixProvider>(References.NoEF, test, test);
        }

        [TestMethod]
        public async Task DontSuggestForInMemoryList()
        {
            var test = @"
using System.Collections.Generic;
using System.Linq;

class Person
{
    public int Age { get; set; }
}

class People
{
    private readonly List<Person> people = new List<Person>();

    public IEnumerable<Person> Adults()
    {
        return people.ToList().Where(p => p.Age >= 18);
    }
}";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }
    }
}
