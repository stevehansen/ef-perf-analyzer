using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace EntityFrameworkAnalyzer.Test
{
    [TestClass]
    public class UnitTest
    {
        [TestMethod]
        public async Task DontSuggestForEmpty()
        {
            var test = @"";

            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForConst()
        {
            var test = @"
    namespace ConsoleApplication1
    {
        class TypeName
        {
            public void Do()
            {
                const int i = 0;
            }
        }
    }";

            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForAssignment()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                var name = person.Name;
                person = null;
            }
        }
    }";

            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task FixSingleProperty()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                var name = person.Name;
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(it => new { it.Name }).First();
                var name = person.Name;
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixSingleStringPropertySubstring()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                var name = person.Name.Substring(0, 2);
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(it => new { it.Name }).First();
                var name = person.Name.Substring(0, 2);
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixSinglePropertyToField()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;
            private string name;

            public void Do()
            {
                var person = people.First();
                name = person.Name;
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(24, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;
            private string name;

            public void Do()
            {
                var person = people.Select(it => new { it.Name }).First();
                name = person.Name;
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixSinglePropertyWithAccessor()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                var nameLength = person.Name.Length;
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(it => new { it.Name }).First();
                var nameLength = person.Name.Length;
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixMultipleProperties()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                var id = person.Id;
                var name = person.Name;
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Id, Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(it => new { it.Id, it.Name }).First();
                var id = person.Id;
                var name = person.Name;
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixPropertyAndNullCheck()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.FirstOrDefault();
                if (person != null)
                {
                    var name = person.Name;
                }
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(it => new { it.Name }).FirstOrDefault();
                if (person != null)
                {
                    var name = person.Name;
                }
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixNullConditionalProperty()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.FirstOrDefault();
                var name = person?.Name;
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(it => new { it.Name }).FirstOrDefault();
                var name = person?.Name;
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixPropertyAsArgument()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.FirstOrDefault();
                Console.WriteLine(person.Name);
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(it => new { it.Name }).FirstOrDefault();
                Console.WriteLine(person.Name);
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task FixAndRewriteCondition()
        {
            var test = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First(it => it.Id > 0);
                var name = person.Name;
            }
        }
    }";
            var expected = Verify.Diagnostic(Diagnostics.EFPERF001)
                .WithMessage("Variable 'person' is only used for properties: Name")
                .WithLocation(23, 21);

            var fixtest = @"
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;
    using System.Diagnostics;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Where(it => it.Id > 0).Select(it => new { it.Name }).First();
                var name = person.Name;
            }
        }
    }";
            await Verify.CodeFixAsync<EFPERF001CodeFixProvider>(References.NoEF, test, fixtest, expected);
        }

        [TestMethod]
        public async Task DontSuggestForEnumerable()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IEnumerable<Person> people;

            public void Do()
            {
                var person = people.First();
                var name = person.Name;
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForMethodAccess()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                var name = person.ToString();
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForMemberAssignment()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                person.Name = string.Empty;
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForMemberIncrement()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                person.Id++;
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForMemberNameof()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                var nameProp = nameof(person.Name);
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForTypedAssignment()
        {
            // TODO: Might suggest info with change from Person to var
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                Person person = people.First();
                var name = person.Name;
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForSeparateAssignment()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                Person person;
                person = people.First();
                var name = person.Name;
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForCollectionMemberMethod()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public ICollection<string> Members { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                person.Members.Add(person.Name);
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForExtensionMethod()
        {
            // TODO: Might suggest info when argument is of type object/generic
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.First();
                person.Write();
            }
        }

        static class Extensions
        {
            public static void Write<T>(this T obj)
            {
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }

        [TestMethod]
        public async Task DontSuggestForProjectedQueryable()
        {
            var test = @"
    using System.Collections.Generic;
    using System.Linq;

    namespace ConsoleApplication1
    {
        class Person
        {
            public int Id { get; set; }
            public string Name { get; set; }
        }

        class TypeName
        {
            public IQueryable<Person> people;

            public void Do()
            {
                var person = people.Select(p => new { p.Name }).First();
                var name = person.Name;
            }
        }
    }";
            await Verify.AnalyzerAsync(References.NoEF, test);
        }
    }
}