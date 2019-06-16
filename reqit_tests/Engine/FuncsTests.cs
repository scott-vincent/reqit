using Moq;
using reqit.Engine;
using reqit.Models;
using reqit.Parsers;
using System;
using Xunit;

namespace reqit_tests.Engine
{
    public class FuncsTestsBase : IDisposable
    {
        protected Funcs funcs;
        protected Resolver resolver;

        protected FuncsTestsBase()
        {
            // Called before every test
            this.funcs = new Funcs();
            this.resolver = new Resolver(null, null);
        }

        public void Dispose()
        {
            // Called after every test
        }
    }

    public class FuncsTests : FuncsTestsBase
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
        public void TestFuncStr()
        {
            string[] args = { "4" };

            string result = this.funcs.FuncStr("test", args);
            Assert.Equal(4, result.Length);
        }

        [Fact]
        public void TestFuncStrBadArgs()
        {
            string[] args = { "4", "2", "3" };

            Action funcStr = () => this.funcs.FuncStr("test", args);
            var ex = Record.Exception(funcStr);
            Assert.NotNull(ex);
            Assert.Contains("bad number of arguments", ex.Message);
        }

        [Fact]
        public void TestFuncNum()
        {
            string[] args = { "4" };

            string result = this.funcs.FuncNum("test", args, null, null, null);
            Assert.Equal(4, result.Length);
        }

        [Fact]
        public void TestFuncNumBadArgs()
        {
            string[] args = { "4", "2", "3" };

            Action funcNum = () => this.funcs.FuncNum("test", args, null, null, null);
            var ex = Record.Exception(funcNum);
            Assert.NotNull(ex);
            Assert.Contains("bad number of arguments", ex.Message);
        }

        [Theory]
        [InlineData("4.567", "2t", "4.56")]
        [InlineData("4.567", "2r", "4.57")]
        [InlineData("4.567", "1r", "4.6")]
        [InlineData("4.567", "0", "5")]
        public void TestFuncNumRounding(params string[] args)
        {
            string expected = args[args.Length - 1];
            Array.Resize(ref args, args.Length - 1);

            var apiService = new ApiService();
            apiService.EntityRoot = new Entity("test.num", Entity.Types.NUM, args[0]);
            args[0] = "~num";
            resolver.Init(apiService);

            string result = this.funcs.FuncNum("test", args, new Cache(), "test", this.resolver);
            Assert.Equal(expected, result);
        }

        [Fact]
        public void TestFuncDate()
        {
            string[] args = { "NOW" };

            string result = this.funcs.FuncDate("test", args);
            Assert.Equal("2019-04-06T12:30:15".Length, result.Length);
        }

        [Fact]
        public void TestFuncDateBadArgs()
        {
            string[] args = { "NOTNOW" };

            Action funcDate = () => this.funcs.FuncDate("test", args);
            var ex = Record.Exception(funcDate);
            Assert.NotNull(ex);
            Assert.Contains("valid DateTime", ex.Message);
        }

        [Fact]
        public void TestFuncTime()
        {
            string[] args = { "NOW" };

            string result = this.funcs.FuncTime("test", args);
            Assert.Equal("12:30:15".Length, result.Length);
        }

        [Fact]
        public void TestFuncTimeBadArgs()
        {
            string[] args = { "NOTNOW" };

            Action funcDate = () => this.funcs.FuncTime("test", args);
            var ex = Record.Exception(funcDate);
            Assert.NotNull(ex);
            Assert.Contains("valid DateTime", ex.Message);
        }

        [Fact]
        public void TestFuncGen()
        {
            string[] args = { "#-^-@-*" };

            string result = this.funcs.FuncGen("test", args);
            Assert.Equal(7, result.Length);
            Assert.True(Char.IsDigit(result[0]));
            Assert.True(Char.IsUpper(result[2]));
            Assert.True(Char.IsLower(result[4]));
            Assert.True(Char.IsLetter(result[6]));
        }

        [Fact]
        public void TestFuncGenWithComma()
        {
            string[] args = { "4", "2", ",,1" };

            string result = this.funcs.FuncGen("test", args);
            Assert.Equal("4,2,,,1", result);
        }

        [Fact]
        public void TestFuncRand()
        {
            string[] args = { "100-999" };

            string result = this.funcs.FuncRand("test", args);
            Assert.Equal(3, result.Length);
        }

        [Fact]
        public void TestFuncRandBadArgs()
        {
            string[] args = { "4", "2", "1" };

            Action funcRand = () => this.funcs.FuncStr("test", args);
            var ex = Record.Exception(funcRand);
            Assert.NotNull(ex);
            Assert.Contains("bad number of arguments", ex.Message);
        }

        [Fact]
        public void TestFuncPick()
        {
            string[] args = { "one", "two", "three" };

            string result = this.funcs.FuncPick("test", args);
            Assert.Contains(result, args);
        }

        [Fact]
        public void TestFuncSampleBadArgs()
        {
            string[] args = { "4", "2", "3" };

            Action funcSample = () => this.funcs.FuncSample("test", args, new Cache(), "parent", null, out var gender);
            var ex = Record.Exception(funcSample);
            Assert.NotNull(ex);
            Assert.Contains("bad number of arguments", ex.Message);
        }

        [Theory]
        [InlineData("7", ">4", "yes", "no")]
        [InlineData("7", "<4", "no", "yes")]
        [InlineData("7", "=4", "no", "yes")]
        [InlineData("7", "=7", "yes", "no")]
        [InlineData("6", ">6.5", "no", "yes")]
        [InlineData("6", "<6.5", "yes", "no")]
        [InlineData("6.54", "=6.54", "yes", "no")]
        [InlineData("first", "<second", "yes", "no")]
        [InlineData("first", ">second", "no", "yes")]
        [InlineData("first", "=first", "yes", "no")]
        public void TestFuncIf(params string[] args)
        {
            string result = this.funcs.FuncIf("test", args, null, null, null, null);
            Assert.Equal("yes", result);
        }

        [Theory]
        [InlineData("4.6", ">4.5", "yes", "no")]
        [InlineData("4.5", ">4.5", "no", "yes")]
        [InlineData("4.6", "<4.5", "no", "yes")]
        [InlineData("4.6", "=4.5", "no", "yes")]
        [InlineData("4.6", "=4.6", "yes", "no")]
        public void TestFuncIfRef(params string[] args)
        {
            var apiService = new ApiService();
            apiService.EntityRoot = new Entity("test.num", Entity.Types.NUM, args[0]);
            args[0] = "~num";
            resolver.Init(apiService);
 
            string result = this.funcs.FuncIf("test", args, new Cache(), "test", this.resolver, null);
            Assert.Equal("yes", result);
        }

        [Theory]
        [InlineData("7", "+4", "11")]
        [InlineData("7", "-4", "3")]
        [InlineData("7", "*2", "14")]
        [InlineData("7", "/2", "3.5")]
        [InlineData("7", "+4", "+5", "16")]
        [InlineData("7", "+4", "-2", "9")]
        [InlineData("7", "+0.5", "-2.75", "4.75")]
        public void TestFuncMath(params string[] args)
        {
            string expected = args[args.Length - 1];
            Array.Resize(ref args, args.Length - 1);

            string result = this.funcs.FuncMath("test", args, null, null, null);
            Assert.Equal(expected, result);
        }

        [Theory]
        [InlineData("72", "-30")]
        [InlineData("17", "+13", "+12")]
        [InlineData("42", "*2", "/2")]
        [InlineData("41", "/2", "+21.5")]
        public void TestFuncMathRef(params string[] args)
        {
            var apiService = new ApiService();
            apiService.EntityRoot = new Entity("test.num", Entity.Types.NUM, args[0]);
            args[0] = "~num";
            resolver.Init(apiService);

            string result = this.funcs.FuncMath("test", args, new Cache(), "test", this.resolver);
            Assert.Equal("42", result);
        }
    }
}
