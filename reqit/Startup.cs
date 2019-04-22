using System;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using reqit.CmdLine;
using reqit.Engine;
using reqit.Parsers;
using reqit.Services;

namespace reqit
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddMvc().SetCompatibilityVersion(CompatibilityVersion.Version_2_2);

            // Register singleton dependencies
            services.AddSingleton<ICommand, Command>();
            services.AddSingleton<IYamlParser, YamlParser>();
            services.AddSingleton<IJsonParser, JsonParser>();
            services.AddSingleton<ISamplesParser, SamplesParser>();
            services.AddSingleton<IResolver, Resolver>();
            services.AddSingleton<IFuncs, Funcs>();
            services.AddSingleton<IFormatter, Formatter>();
            services.AddSingleton<ISimulator, Simulator>();

            // Register scoped dependencies
            services.AddScoped<IDefaultService, DefaultService>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }

            app.UseMvc();
        }
    }
}
