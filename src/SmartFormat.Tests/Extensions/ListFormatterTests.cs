﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using SmartFormat.Core.Extensions;
using SmartFormat.Core.Formatting;
using SmartFormat.Extensions;
using SmartFormat.Tests.TestUtils;

namespace SmartFormat.Tests.Extensions
{
    [TestFixture]
    public class ListFormatterTests
    {
        public object[] GetArgs()
        {
            var args = new object[] {
                "ABCDE".ToCharArray(),
                "One|Two|Three|Four|Five".Split('|'),
                TestFactory.GetPerson().Friends,
                "1/1/2000|10/10/2010|5/5/5555".Split('|').Select(s=>DateTime.ParseExact(s,"M/d/yyyy", new CultureInfo("en-US"))),
                new []{1,2,3,4,5},
            };
            return args;
        }

        [Test]
        public void Simple_List()
        {
            var items = new[] { "one", "two", "three" };
            var result = Smart.Default.Format("{0:list:{}|, |, and }", new object[] { items }); // important: not only "items" as the parameter
            Assert.AreEqual("one, two, and three", result);
        }

        [Test]
        public void Empty_List()
        {
            var items = Array.Empty<string>();
            var result = Smart.Default.Format("{0:list:{}|, |, and }", new object[] { items });
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void Null_List()
        {
            var result = Smart.Default.Format("{TheList?:list:{}|, |, and }", new { TheList = default(object)});
            Assert.AreEqual(string.Empty, result);
        }

        [Test]
        public void List_of_anonymous_types_and_enumerables()
        {
            var data = new[]
            {
                new { Name = "Person A", Gender = "M" },
                new { Name = "Person B", Gender = "F" },
                new { Name = "Person C", Gender = "M" }
            };

            var model = new
            {
                Persons = data.Where(p => p.Gender == "M")
            };

            Smart.Default.Settings.StringFormatCompatibility = false; // mandatory for this test case because of consecutive curly braces
            Smart.Default.Settings.Formatter.ErrorAction = SmartFormat.Core.Settings.FormatErrorAction.ThrowError;
            Smart.Default.Settings.Parser.ErrorAction = SmartFormat.Core.Settings.ParseErrorAction.ThrowError;

            // Note: it's faster to add the named formatter, than finding it implicitly by "trial and error".
            var result = Smart.Default.Format("{0:list:{Name}|, |, and }", new object[] { data }); // Person A, Person B, and Person C
            Assert.AreEqual("Person A, Person B, and Person C", result);
            result = Smart.Default.Format("{0:list:{Name}|, |, and }", model.Persons);  // Person A, and Person C
            Assert.AreEqual("Person A, and Person C", result);
            result = Smart.Default.Format("{0:list:{Name}|, |, and }", data.Where(p => p.Gender == "F"));  // Person B
            Assert.AreEqual("Person B", result);
            result = Smart.Default.Format("{0:{Persons:{Name}|, }}", model); // Person A, and Person C
            Assert.AreEqual("Person A, Person C", result);
        }

        [TestCase("{4}", "System.Int32[]")]
        [TestCase("{4:|}","12345")]
        [TestCase("{4:00|}","0102030405")]
        [TestCase("{4:|,}","1,2,3,4,5")]
        [TestCase("{4:|, |, and }","1, 2, 3, 4, and 5")]
        [TestCase("{4:N2|, |, and }","1.00, 2.00, 3.00, 4.00, and 5.00")]
        public void FormatTest(string format, string expected)
        {
            var args = GetArgs();
            Smart.Default.Test(new[] {format}, args, new[] {expected});

        }

        [TestCase("{0:{}-|}", "A-B-C-D-E-")]
        [TestCase("{0:{}|-}", "A-B-C-D-E")]
        [TestCase("{0:{}|-|+}", "A-B-C-D+E")]
        [TestCase("{0:({})|, |, and }", "(A), (B), (C), (D), and (E)")]
        public void NestedFormatTest(string format, string expected)
        {
            var args = GetArgs();
            Smart.Default.Test(new[] {format}, args, new[] {expected});
        }
        [TestCase("{2:{:{FirstName}}|, }", "Jim, Pam, Dwight")]
        [TestCase("{3:{:M/d/yyyy} |}", "1/1/2000 10/10/2010 5/5/5555 ")]
        [TestCase("{2:{:{FirstName}'s friends: {Friends:{FirstName}|, }}|; }", "Jim's friends: Dwight, Michael; Pam's friends: Dwight, Michael; Dwight's friends: Michael")]
        public void NestedArraysTest(string format, string expected)
        {
            var args = GetArgs();
            Smart.Default.Test(new[] {format}, args, new[] {expected});
        }

        [Test] /* added due to problems with [ThreadStatic] see: https://github.com/axuno/SmartFormat.NET/pull/23 */
        public void WithThreadPool_ShouldNotMixupCollectionIndex()
        {
            // Old test did not show wrong Index value - it ALWAYS passed even when using ThreadLocal<int> or [ThreadStatic] respectively:
            // const string format = "{wheres.Count::>0? where |}{wheres:{}| and }";
            const string format = "Wheres-Index={Index}.";

            var wheres = new List<string>{"test1 = test1", "test2 = test2"};
            
            var tasks = new List<Task<string>>();
            for (int i = 0; i < 10; ++i)
            {
                tasks.Add(Task.Factory.StartNew(val =>
                {
                    Thread.Sleep(5 * (int)(val ?? 100));
                    string ret = Smart.Default.Format(format, wheres);
                    Thread.Sleep(5 * (int)(val ?? 100)); /* add some delay to force ThreadPool swapping */
                    return ret;
                }, i));
            }

            foreach (var t in tasks)
            {
                // Old test did not show wrong Index value:
                // Assert.AreEqual(" where test = @test", t.Result);

                // Note: Using "[ThreadStatic] private static int CollectionIndex", the result will be as expected only with the first task
                if ("Wheres-Index=-1." == t.Result)
                    Console.WriteLine("Task {0} passed.", t.AsyncState);
                
                Assert.AreEqual("Wheres-Index=-1.", t.Result);
            }
        }

        [Test]
        public void Objects_Not_Implementing_IList_Are_Not_Processed()
        {
            var items = new[] { "one", "two", "three" };
            
            var formatter = new SmartFormatter();
            var listFormatter = new ListFormatter(formatter);
            formatter.SourceExtensions.Add(listFormatter);
            formatter.FormatterExtensions.Add(listFormatter);
            formatter.SourceExtensions.Add(new DefaultSource(formatter));
            formatter.FormatterExtensions.Add(new DefaultFormatter());

            Assert.AreEqual("one, two, and three", formatter.Format("{0:list:{}|, |, and }", new object[] { items }));
        }

        [TestCase("{0:{} = {Index}|, }", "A = 0, B = 1, C = 2, D = 3, E = 4")] // Index holds the current index of the iteration
        [TestCase("{1:{Index}: {ToCharArray:{} = {Index}|, }|; }", "0: O = 0, n = 1, e = 2; 1: T = 0, w = 1, o = 2; 2: T = 0, h = 1, r = 2, e = 3, e = 4; 3: F = 0, o = 1, u = 2, r = 3; 4: F = 0, i = 1, v = 2, e = 3")] // Index can be nested
        [TestCase("{0:{} = {1.Index}|, }", "A = One, B = Two, C = Three, D = Four, E = Five")] // Index is used to synchronize 2 lists
        [TestCase("{Index}", "-1")] // Index can be used out-of-context, but should always be -1
        public void TestIndex(string format, string expected)
        {
            var args = GetArgs();
            Smart.Default.Test(new[] {format}, args, new[] {expected});
        }

        [Test]
        public void Test_Not_An_IList_Argument()
        {
            Assert.That(() => Smart.Format("{0:list:{}|, |, and }", "not a list"),
                Throws.Exception.TypeOf<FormattingException>().And.Message
                    .Contains("No suitable Formatter"));
        }

        [TestCase("one", "one")]
        [TestCase(null, "")]
        public void Should_Return_Indexed_List_Element(string? listElement, string expected)
        {
            var smart = new SmartFormatter();
            smart.AddExtensions(new ISource[] { new ListFormatter(smart), new DefaultSource(smart), new ReflectionSource(smart) });
            smart.AddExtensions(new IFormatter[] {new ListFormatter(smart), new DefaultFormatter()});
            smart = Smart.CreateDefaultSmartFormat();

            var numbers = new List<string?> {"dummy", listElement};

            var data = new {Numbers = numbers};
            var indexResult1 = smart.Format(">{Numbers.1}<", data); // index method 1
            var indexResult2 = smart.Format(">{Numbers[1]}<", data); // index method 2
            
            Assert.That(indexResult1, Is.EqualTo($">{expected}<"));
            Assert.That(indexResult2, Is.EqualTo($">{expected}<"));
        }

        [Test]
        public void Null_IList_Nullable_Should_Return_Null()
        {
            var smart = new SmartFormatter();
            smart.AddExtensions(new ISource[] { new ListFormatter(smart), new DefaultSource(smart), new ReflectionSource(smart) });
            smart.AddExtensions(new IFormatter[] {new ListFormatter(smart), new DefaultFormatter()});
            smart = Smart.CreateDefaultSmartFormat();
            
            var data = new {Numbers = default(object)};
            var indexResult1 = smart.Format(">{Numbers?.0}<", data); // index method 1
            var indexResult2 = smart.Format(">{Numbers?[0]}<", data); // index method 2

            Assert.That(indexResult1, Is.EqualTo("><"));
            Assert.That(indexResult2, Is.EqualTo("><"));
        }
    }
}
