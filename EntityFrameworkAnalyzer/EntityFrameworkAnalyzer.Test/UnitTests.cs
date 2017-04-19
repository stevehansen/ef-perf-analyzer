using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TestHelper;

namespace EntityFrameworkAnalyzer.Test
{
    [TestClass]
    public class UnitTest : CodeFixVerifier
    {
        [TestMethod]
        public void DontSuggestForEmpty()
        {
            var test = @"";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForConst()
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
        class TypeName
        {
            public void Do()
            {
                const var i = 0;
            }
        }
    }";

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForAssignment()
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

            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void FixSingleProperty()
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
            var expected = new DiagnosticResult
            {
                Id = Diagnostics.EFPERF001.Id,
                Message = "Variable 'person' is only used for properties: Name",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                            new DiagnosticResultLocation("Test0.cs", 23, 21)
                        }
            };

            VerifyCSharpDiagnostic(test, expected);

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
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void FixSinglePropertyToField()
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
            var expected = new DiagnosticResult
            {
                Id = Diagnostics.EFPERF001.Id,
                Message = "Variable 'person' is only used for properties: Name",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 24, 21)
                    }
            };

            VerifyCSharpDiagnostic(test, expected);

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
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void FixSinglePropertyWithAccessor()
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
            var expected = new DiagnosticResult
            {
                Id = Diagnostics.EFPERF001.Id,
                Message = "Variable 'person' is only used for properties: Name",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 23, 21)
                    }
            };

            VerifyCSharpDiagnostic(test, expected);

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
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void FixMultipleProperties()
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
            var expected = new DiagnosticResult
            {
                Id = Diagnostics.EFPERF001.Id,
                Message = "Variable 'person' is only used for properties: Id, Name",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 23, 21)
                    }
            };

            VerifyCSharpDiagnostic(test, expected);

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
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void FixPropertyAndNullCheck()
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
            var expected = new DiagnosticResult
            {
                Id = Diagnostics.EFPERF001.Id,
                Message = "Variable 'person' is only used for properties: Name",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 23, 21)
                    }
            };

            VerifyCSharpDiagnostic(test, expected);

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
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void FixNullConditionalProperty()
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
            var expected = new DiagnosticResult
            {
                Id = Diagnostics.EFPERF001.Id,
                Message = "Variable 'person' is only used for properties: Name",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 23, 21)
                    }
            };

            VerifyCSharpDiagnostic(test, expected);

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
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void FixAndRewriteCondition()
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
            var expected = new DiagnosticResult
            {
                Id = Diagnostics.EFPERF001.Id,
                Message = "Variable 'person' is only used for properties: Name",
                Severity = DiagnosticSeverity.Warning,
                Locations =
                    new[] {
                        new DiagnosticResultLocation("Test0.cs", 23, 21)
                    }
            };

            VerifyCSharpDiagnostic(test, expected);

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
            VerifyCSharpFix(test, fixtest);
        }

        [TestMethod]
        public void DontSuggestForEnumerable()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForMethodAccess()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForMemberAssignment()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForMemberIncrement()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForMemberNameof()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForTypedAssignment()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForSeparateAssignment()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForExtensionMethod()
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
            VerifyCSharpDiagnostic(test);
        }

        [TestMethod]
        public void DontSuggestForProjectedQueryable()
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
            VerifyCSharpDiagnostic(test);
        }

        protected override CodeFixProvider GetCSharpCodeFixProvider()
        {
            return new EFPERF001CodeFixProvider();
        }

        protected override DiagnosticAnalyzer GetCSharpDiagnosticAnalyzer()
        {
            return new EFPerfAnalyzer();
        }
    }
}