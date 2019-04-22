using Moq;
using reqit.Engine;
using reqit.Models;
using reqit.Parsers;
using System;
using Xunit;

namespace reqit_tests.Engine
{
    public class ResolverTestsBase : IDisposable
    {
        protected IYamlParser yamlParser;
        protected ISamplesParser samplesParser;
        protected Funcs funcs;
        protected Resolver resolver;

        protected ResolverTestsBase()
        {
            // Called before every test
            this.yamlParser = new YamlParser();
            this.samplesParser = new SamplesParser();
            this.funcs = new Funcs();
            this.resolver = new Resolver(this.samplesParser, this.funcs);
        }

        public void Dispose()
        {
            // Called after every test
        }
    }

    public class ResolverTests : ResolverTestsBase
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
        public void TestResolverInit()
        {
            string yaml = @"
api:
  - method: GET
    path: /api
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);
            Assert.NotEmpty(this.resolver.ApiService.Apis);
        }

        [Fact]
        public void TestResolverInitNoApis()
        {
            string yaml = @"
entity:
  test1: STR, stuff
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);
            Assert.Empty(this.resolver.ApiService.Apis);
        }

        [Fact]
        public void TestFindEntityEmptyYaml()
        {
            string yaml = "";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Entity entity;
            try
            {
                entity = this.resolver.FindEntity("test1");
            }
            catch (Exception)
            {
                entity = null;
            }

            Assert.Null(entity);
        }

        [Fact]
        public void TestFindMissingEntity()
        {
            string yaml = @"
entity:
  test1: STR, stuff
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Entity entity;
            try
            {
                entity = this.resolver.FindEntity("test2");
            }
            catch (Exception)
            {
                entity = null;
            }
            Assert.Null(entity);
        }

        [Fact]
        public void TestFindEntity()
        {
            string yaml = @"
entity:
  test1: STR, stuff
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Entity entity = this.resolver.FindEntity("test1");
            Assert.NotNull(entity);
            Assert.Equal("test1", entity.Name);
        }

        [Fact]
        public void TestFindEntityValidTypes()
        {
            string yaml = @"
entity:
  strType: STR, strStuff
  numType: NUM, numStuff
  dateType: DATE, dateStuff
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Entity entity = this.resolver.FindEntity("strType");
            Assert.NotNull(entity);
            Assert.Equal("strType", entity.Name);
            Assert.Equal(Entity.Types.STR, entity.Type);
            Assert.Equal("strStuff", entity.Value);

            entity = this.resolver.FindEntity("numType");
            Assert.NotNull(entity);
            Assert.Equal("numType", entity.Name);
            Assert.Equal(Entity.Types.NUM, entity.Type);
            Assert.Equal("numStuff", entity.Value);

            entity = this.resolver.FindEntity("dateType");
            Assert.NotNull(entity);
            Assert.Equal("dateType", entity.Name);
            Assert.Equal(Entity.Types.DATE, entity.Type);
            Assert.Equal("dateStuff", entity.Value);
        }

        [Fact]
        public void TestFindEntityNullValue()
        {
            string yaml = @"
entity:
  test: STR, <null>
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Entity entity = this.resolver.FindEntity("test");
            Assert.NotNull(entity);
            Assert.Equal(Entity.Types.STR, entity.Type);
            Assert.Equal("<null>", entity.Value);
        }

        [Fact]
        public void TestFindEntityEmptyValue()
        {
            string yaml = @"
entity:
  test: STR,
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Entity entity = this.resolver.FindEntity("test");
            Assert.NotNull(entity);
            Assert.Equal(Entity.Types.STR, entity.Type);
            Assert.Equal("", entity.Value);
        }

        [Fact]
        public void TestMatchRoute()
        {
            string yaml = @"
api:
  - method: GET
    path: /api
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Api api = this.resolver.MatchRoute(Api.Methods.GET, "/api", out var pathEntities);
            Assert.NotNull(api);
        }

        [Fact]
        public void TestMatchRouteEmptyYaml()
        {
            string yaml = "";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Api api = this.resolver.MatchRoute(Api.Methods.GET, "/api", out var pathEntities);
            Assert.Null(api);
        }

        [Fact]
        public void TestMatchRouteWrongMethod()
        {
            string yaml = @"
api:
  - method: GET
    path: /api
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Api api = this.resolver.MatchRoute(Api.Methods.PUT, "/api", out var pathEntities);
            Assert.Null(api);
        }

        [Fact]
        public void TestMatchRouteWrongPath()
        {
            string yaml = @"
api:
  - method: GET
    path: /api
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Api api = this.resolver.MatchRoute(Api.Methods.GET, "/apis", out var pathEntities);
            Assert.Null(api);
        }

        [Fact]
        public void TestMatchRouteValidMethods()
        {
            string yaml = @"
api:
  - method: GET
    path: /api
  - method: PUT
    path: /api
  - method: PATCH
    path: /api2
  - method: POST
    path: /api
  - method: DELETE
    path: /api
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            Api api = this.resolver.MatchRoute(Api.Methods.GET, "/api", out var pathEntities);
            Assert.NotNull(api);
            Assert.Equal(Api.Methods.GET, api.Method);
            Assert.Equal("/api", api.Path.ToString());

            api = this.resolver.MatchRoute(Api.Methods.PUT, "/api", out pathEntities);
            Assert.NotNull(api);
            Assert.Equal(Api.Methods.PUT, api.Method);
            Assert.Equal("/api", api.Path.ToString());

            api = this.resolver.MatchRoute(Api.Methods.PATCH, "/api2", out pathEntities);
            Assert.NotNull(api);
            Assert.Equal(Api.Methods.PATCH, api.Method);
            Assert.Equal("/api2", api.Path.ToString());

            api = this.resolver.MatchRoute(Api.Methods.POST, "/api", out pathEntities);
            Assert.NotNull(api);
            Assert.Equal(Api.Methods.POST, api.Method);
            Assert.Equal("/api", api.Path.ToString());

            api = this.resolver.MatchRoute(Api.Methods.DELETE, "/api", out pathEntities);
            Assert.NotNull(api);
            Assert.Equal(Api.Methods.DELETE, api.Method);
            Assert.Equal("/api", api.Path.ToString());
        }

        [Fact]
        public void TestResolveRef()
        {
            string yaml = @"
entity:
  test: STR, This is a test
  myref: REF, test

alias:
  myalias: test
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("myref");
            Assert.NotNull(entity);

            var resolving = new ResolvedValue("test", entity.Type, entity.Value);
            this.resolver.Resolve(resolving, new Cache());
            Assert.Equal("This is a test", resolving.Value);

            var entity2 = this.resolver.FindEntity("myalias");
            Assert.NotNull(entity2);

            var resolving2 = new ResolvedValue("test2", entity2.Type, entity2.Value);
            this.resolver.Resolve(resolving2, new Cache());
            Assert.Equal("This is a test", resolving2.Value);
        }

        [Fact]
        public void TestResolveCombinedFuncs()
        {
            /// Note: Individual funcs are tested by FuncsTests

            string yaml = @"
entity:
  test: ""STR, func.pick(me)-func.pick(me2) : func.pick(me3)""
            ";

            var apiService = this.yamlParser.Load(yaml);
            this.resolver.Init(apiService);

            var entity = this.resolver.FindEntity("test");
            Assert.NotNull(entity);

            var resolving = new ResolvedValue("test", entity.Type, entity.Value);
            this.resolver.Resolve(resolving, new Cache());
            Assert.Equal("me-me2 : me3", resolving.Value);
        }
    }
}
