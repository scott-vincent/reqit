using Moq;
using reqit.Engine;
using reqit.Models;
using reqit.Parsers;
using System;
using Xunit;

namespace reqit_tests.Engine
{
    public class FormatterTestsBase : IDisposable
    {
        protected IYamlParser yamlParser;
        protected Funcs funcs;
        protected Resolver resolver;
        protected Formatter formatter;

        protected FormatterTestsBase()
        {
            // Called before every test
            this.yamlParser = new YamlParser();
            this.funcs = new Funcs();
            this.resolver = new Resolver(null, null);
            this.formatter = new Formatter(this.resolver);
        }

        public void Dispose()
        {
            // Called after every test
        }
    }

    public class FormatterTests : FormatterTestsBase
    {
        /*
        private readonly ITestOutputHelper output;

        public EmployeesControllerTests(ITestOutputHelper output)
        {
            this.output = output;
            this.output.WriteLine("Running Test");
        }
        */

        [Fact]
        public void TestAttributeToJson()
        {
            string yaml = @"
entity:
  test: STR, This is a test
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("test");
            Assert.NotNull(entity);
            string value = this.formatter.EntityToJson(entity, new Cache());
            Assert.Equal("test: \"This is a test\"", value);
        }

        [Fact]
        public void TestObjectToJson()
        {
            string yaml = @"
entity:
  test:
    first: STR, This is first
    second: STR, This is second
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("test");
            Assert.NotNull(entity);
            string value = this.formatter.EntityToJson(entity, new Cache());
            Assert.Equal("{first: \"This is first\", second: \"This is second\"}", value);
        }

        [Fact]
        public void TestArrayToJson()
        {
            string yaml = @"
entity:
  test: STR, This is a test
  empty: REF, [test, 0]

alias:
  array: ""[test, 2]""
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("array");
            Assert.NotNull(entity);
            string value = this.formatter.EntityToJson(entity, new Cache());
            Assert.Equal("[\"This is a test\", \"This is a test\"]", value);

            var entity2 = this.resolver.FindEntity("empty");
            Assert.NotNull(entity2);
            string value2 = this.formatter.EntityToJson(entity2, new Cache());
            Assert.Equal("[]", value2);
        }

        [Fact]
        public void TestObjectToSql()
        {
            string yaml = @"
entity:
  test:
    first: STR, This is first
    second: STR, This is second
            ";

            string expected = @"INSERT INTO test ('first','second') VALUES ('This is first','This is second');
";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("test");
            Assert.NotNull(entity);
            string value = this.formatter.EntityToSql(entity);
            Assert.Equal(expected, value);
        }

        [Fact]
        public void TestArrayToSql()
        {
            string yaml = @"
entity:
  test:
    first: STR, This is first
    second: STR, This is second

alias:
  array: ""[test, 2]""
            ";

            string expected = @"INSERT INTO test ('first','second') VALUES ('This is first','This is second');
INSERT INTO test ('first','second') VALUES ('This is first','This is second');
";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("array");
            Assert.NotNull(entity);
            string value = this.formatter.EntityToSql(entity);
            Assert.Equal(expected, value);
        }

        [Fact]
        public void TestObjectToCsv()
        {
            string yaml = @"
entity:
  test:
    first: STR, This is first
    second: STR, This is second
            ";

            string expected = @"""first"",""second""
""This is first"",""This is second""
";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("test");
            Assert.NotNull(entity);
            string value = this.formatter.EntityToCsv(entity);
            Assert.Equal(expected, value);
        }

        [Fact]
        public void TestArrayToCsv()
        {
            string yaml = @"
entity:
  test:
    first: STR, This is first
    second: STR, This is second

alias:
  array: ""[test, 2]""
            ";

            string expected = @"""first"",""second""
""This is first"",""This is second""
""This is first"",""This is second""
";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("array");
            Assert.NotNull(entity);
            string value = this.formatter.EntityToCsv(entity);
            Assert.Equal(expected, value);
        }
    }
}
