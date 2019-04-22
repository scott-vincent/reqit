using Moq;
using reqit.Engine;
using reqit.Models;
using reqit.Parsers;
using System;
using Xunit;

namespace reqit_tests.Parsers
{
    public class YamlParserTestsBase : IDisposable
    {
        protected YamlParser yamlParser;

        protected YamlParserTestsBase()
        {
            // Called before every test
            this.yamlParser = new YamlParser();
        }

        public void Dispose()
        {
            // Called after every test
        }
    }

    public class YamlParserTests : YamlParserTestsBase
    {
        public static string COMPLEX_YAML = @"
entity:
  other:
    other1: STR, val1
    other2: STR, val 2
    other3: STR, val3

  employee:
    id:        STR,  func.num(4)-func.num(4)      # e.g. 1234-5678
    name:      STR,  func.str(6-20,cap)           # cap = first letter upper case
    startDate: DATE, func.date(NOW,-5Y)           # Date between now and 5 years ago
    salary:    NUM,  func.num(5,2)                # e.g. 12345.67
    nothing1:  STR,  <null>
    nothing2:  NUM,  <null>
    stuff:
      field1: BOOL,true
      field2: STR,   Blah de blah
      array1:
        - STR,hi
        - STR,there
        - STR,string
        - STR,array
      array2:
        - orderNum: NUM,123
          quantity: NUM, 5000
        - orderNum: NUM,124
          quantity: NUM, 8999
      array3:
        - a1:
            a1_first: STR, 1st
            a1_second: STR,2nd
        - a2:
            a2_group:
              a2_first: STR,first
              a2_second: STR, second
        - a3:
          - a3_first: STR, F i r s t
            a3_second: STR, S e c o n d
          - a4:
              a4_first: STR, First
      field3: DATE, 2019-03-21
    more: REF, other

alias:
  empid: employee.id
  array2_quantity2: employee.stuff.array2.#2.quantity

api:
  - method: GET
    path: ""/api/employees/{id}""
    response: ""employee,id={path.id}""

  - method: POST
    path: ""/api/employees""
    request: ""employee,!id""
    response: ""employee""
        ";

        //private readonly ITestOutputHelper output;

        //public YamlParserTests(ITestOutputHelper output)
        //{
        //    this.output = output;
        //    this.output.WriteLine("Running Test");
        //}

        [Fact]
        public void TestValidYaml()
        {
            this.yamlParser.Load(COMPLEX_YAML);
            Assert.NotNull(this.yamlParser.ApiService);
            Assert.NotEmpty(this.yamlParser.ApiService.EntityRoot.ChildEntities);
            Assert.NotEmpty(this.yamlParser.ApiService.Apis);
        }

        [Fact]
        public void TestDefaultYaml()
        {
            this.yamlParser.LoadFile(YamlParser.DEFAULT);
            Assert.NotNull(this.yamlParser.ApiService);
            Assert.NotEmpty(this.yamlParser.ApiService.EntityRoot.ChildEntities);
        }

        [Fact]
        public void TestSampleYaml()
        {
            this.yamlParser.LoadFile(YamlParser.SAMPLE);
            Assert.NotNull(this.yamlParser.ApiService);
            Assert.NotEmpty(this.yamlParser.ApiService.EntityRoot.ChildEntities);
            Assert.NotNull(this.yamlParser.ApiService.EntityRoot.ChildEntities["sample"]);
        }

        [Fact]
        public void TestEmptyYaml()
        {
            string yaml = "";

            this.yamlParser.Load(yaml);
            Assert.NotNull(this.yamlParser.ApiService);
            Assert.NotNull(this.yamlParser.ApiService.EntityRoot);
            Assert.Empty(this.yamlParser.ApiService.EntityRoot.ChildEntities);
            Assert.Empty(this.yamlParser.ApiService.Apis);
        }

        [Fact]
        public void TestEntityNoType()
        {
            string yaml = @"
entity:
  test1: "",stuff""
            ";

            Action yamlLoad = () => this.yamlParser.Load(yaml);
            var ex = Record.Exception(yamlLoad);
            Assert.NotNull(ex);
            Assert.Contains("must be in format TYPE,VALUE", ex.Message);
        }

        [Fact]
        public void TestEntityNoValue()
        {
            string yaml = @"
entity:
  test1: STR
            ";

            Action yamlLoad = () => this.yamlParser.Load(yaml);
            var ex = Record.Exception(yamlLoad);
            Assert.NotNull(ex);
            Assert.Contains("must be in format TYPE,VALUE", ex.Message);
        }

        [Fact]
        public void TestEntityEmptyValue()
        {
            string yaml = @"
entity:
  test1: STR,
            ";

            this.yamlParser.Load(yaml);
            Assert.NotNull(this.yamlParser.ApiService.EntityRoot);
            var entity = this.yamlParser.ApiService.EntityRoot.ChildEntities["test1"];
            Assert.Equal("", entity.Value);
        }

        [Fact]
        public void TestEntityBadType()
        {
            string yaml = @"
entity:
  test1: BAD, stuff
            ";

            Action yamlLoad = () => this.yamlParser.Load(yaml);
            var ex = Record.Exception(yamlLoad);
            Assert.NotNull(ex);
            Assert.Contains("unknown type: BAD", ex.Message);
        }

        [Fact]
        public void TestApiNoMethod()
        {
            string yaml = @"
api:
  - path: /api
            ";

            Action yamlLoad = () => this.yamlParser.Load(yaml);
            var ex = Record.Exception(yamlLoad);
            Assert.NotNull(ex);
            Assert.Contains("must have method and path", ex.Message);
        }

        [Fact]
        public void TestApiNoPath()
        {
            string yaml = @"
api:
  - method: GET
            ";

            Action yamlLoad = () => this.yamlParser.Load(yaml);
            var ex = Record.Exception(yamlLoad);
            Assert.NotNull(ex);
            Assert.Contains("must have method and path", ex.Message);
        }

        [Fact]
        public void TestApiBadMethod()
        {
            string yaml = @"
api:
  - method: BAD
    path: /api
            ";

            Action yamlLoad = () => this.yamlParser.Load(yaml);
            var ex = Record.Exception(yamlLoad);
            Assert.NotNull(ex);
            Assert.Contains("unknown method: BAD", ex.Message);
        }

        [Fact]
        public void TestDuplicateApi()
        {
            string yaml = @"
api:
  - method: GET
    path: /api
  - method: GET
    path: /api
            ";

            Action yamlLoad = () => this.yamlParser.Load(yaml);
            var ex = Record.Exception(yamlLoad);
            Assert.NotNull(ex);
            Assert.Contains("duplicate", ex.Message);
        }
    }
}
