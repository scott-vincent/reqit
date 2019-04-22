using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using reqit.CmdLine;

namespace reqit
{
    public class Program
    {
        public static void Main(string[] args)
        {
            // Create the web server but don't start it yet
            string[] dummyArgs = {};
            IWebHost webHost = CreateWebHostBuilder(dummyArgs).Build();

            using (var scope = webHost.Services.CreateScope())
            {
                // Get the Command dependency early so we can use it
                // before starting the web server.
                var command = scope.ServiceProvider.GetRequiredService<ICommand>();

                MyMain myMain = new MyMain(command);
                myMain.Main(args);
            }

            // Start the web server
            webHost.Run();
        }

        public static IWebHostBuilder CreateWebHostBuilder(string[] args) =>
            WebHost.CreateDefaultBuilder(args)
                //.SuppressStatusMessages(true)
                .UseStartup<Startup>();
    }
}
