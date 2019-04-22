using Moq;
using reqit.Engine;
using reqit.Models;
using reqit.Parsers;
using System;
using System.Linq;
using Xunit;

namespace reqit_tests.Parsers
{
    public class JsonParserTestsBase : IDisposable
    {
        protected JsonParser jsonParser;

        protected JsonParserTestsBase()
        {
            // Called before every test
            this.jsonParser = new JsonParser();
        }

        public void Dispose()
        {
            // Called after every test
        }
    }

    public class JsonParserTests : JsonParserTestsBase
    {
        public static string COMPLEX_JSON = @"{id: ""1234-5678"", name: ""MyStr"", startDate: ""2017-11-04"", salary: 12345.67, " +
            @"nothing: null, stuff: {field1: true, field2: ""Blah de blah"", array1: [""hi"", ""there"", ""string"", ""array""], " +
            @"array2: [{orderNum: 123, quantity: 5000}, {orderNum: 124, quantity: 8999}], " +
            @"array3: [{a1: {a1_first: ""1st"", a1_second: ""2nd""}}, {a2: {a2_group: {a2_first: ""first"", a2_second: ""second""}}}, " +
            @"{a3: [{a3_first: ""F i r s t"", a3_second: ""S e c o n d""}, {a4: {a4_first: ""First""}}]}], field3: ""2019-03-21""}}";

        //private readonly ITestOutputHelper output;

        //public JsonParserTests(ITestOutputHelper output)
        //{
        //    this.output = output;
        //    this.output.WriteLine("Running Test");
        //}

        [Fact]
        public void TestValidJson()
        {
            Entity entity = this.jsonParser.LoadEntity("complex", COMPLEX_JSON);
            Assert.NotNull(entity);
            Assert.Equal("complex", entity.Name);
            Assert.Equal(6, entity.ChildOrder.Count);
        }

        [Fact]
        public void TestBadJson()
        {
            // No point testing the external json parser so just need one bad case
            string json = @"{blah}";

            Action jsonLoad = () => this.jsonParser.LoadEntity("bad", json);
            var ex = Record.Exception(jsonLoad);
            Assert.NotNull(ex);
            Assert.Contains("Invalid", ex.Message);
        }
    }
}
