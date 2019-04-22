using McMaster.Extensions.CommandLineUtils;
using reqit.Models;
using reqit.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace reqit.CmdLine
{
    /// <summary>
    /// Parses the command line and validates all args.
    /// Calls Command to run the requested command.
    ///
    /// When the command completes, the program will either
    /// exit or will continue and start the web server.
    /// </summary>
    public class MyMain
    {
        private static string VersionNum = "0.9.0";
        private static string CopyrightYear = "2019";

        private readonly ICommand command;

        public MyMain(ICommand command)
        {
            this.command = command;
        }

        public void Main(string[] args, string request = null)
        {
            string yamlFile = YamlParser.DEFAULT;

            var app = new CommandLineApplication
            {
                Name = "reqit",
                Description = "\nRequestIt - A Data Generator and Service Virtualiser",
                UsePagerForHelpText = false
            };

            app.HelpOption(inherited: true);

            var appVersion = app.Option("-v|--version", "Show version information", CommandOptionType.NoValue);
            var appFile = app.Option("-f|--file", $"Specify YAML file to use (default: {yamlFile})", CommandOptionType.SingleValue);
            var appFuncs = app.Option("--funchelp", "Show help for all functions", CommandOptionType.NoValue);

            ///
            /// COMMAND: Run
            ///
            app.Command("run", runCmd =>
            {
                var runAdmin = runCmd.Option("--admin", "Run in admin mode. Allows Command Line as a service. " +
                    "Leave endpoint blank and pass ?command=<your args>", CommandOptionType.NoValue);

                runCmd.OnExecute(() =>
                {
                    if (appFile.HasValue())
                    {
                        // Must use default yaml file in admin mode
                        if (runAdmin.HasValue())
                        {
                            Console.WriteLine("Cannot specify YAML file if --admin is specified");
                            return 1;
                        }

                        yamlFile = appFile.Value();
                    }

                    if (!this.command.Run(yamlFile, runAdmin.HasValue()))
                    {
                        return 1;
                    }

                    // Want to start the web server
                    return -1;
                });
            });

            ///
            /// COMMAND: Call
            ///
            app.Command("call", callCmd =>
            {
                var callPath = callCmd.Option("-p|--path", "API endpoint to call (simulate)", CommandOptionType.SingleValue)
                    .IsRequired();
                var callMethod = callCmd.Option("-m|--method", "Method associated with API endpoint", CommandOptionType.SingleValue)
                    .IsRequired();
                var callRequest = callCmd.Option("-r|--request", "Output the request instead of the response", CommandOptionType.NoValue);
                var callInput = callCmd.Option("-i|--input", "JSON file to read the request from", CommandOptionType.SingleValue)
                    .Accepts(v => v.ExistingFile());
                var callOutput = callCmd.Option("-o|--output", "Write the output to a file", CommandOptionType.SingleValue);

                callCmd.OnExecute(() =>
                {
                    if (appFile.HasValue())
                    {
                        if (this.command.IsAdminMode)
                        {
                            Console.WriteLine("Must use default YAML file in admin mode");
                            return 1;
                        }

                        yamlFile = appFile.Value();
                    }

                    if (!IsValidMethod(callMethod, out var method))
                    {
                        return 1;
                    }

                    if (callRequest.HasValue() && callInput.HasValue())
                    {
                        Console.WriteLine("Specify either --request or --input");
                        return 1;
                    }

                    if (callInput.HasValue() && this.command.IsAdminMode)
                    {
                        Console.WriteLine("Cannot specify --input in admin mode. Use POST and pass a body instead.");
                        return 1;
                    }

                    if (callOutput.HasValue() && this.command.IsAdminMode)
                    {
                        Console.WriteLine("Cannot specify --output in admin mode");
                        return 1;
                    }

                    return this.command.Call(yamlFile, method, callPath.Value(), callRequest.HasValue(), callInput.Value(), callOutput.Value());
                });
            });

            ///
            /// COMMAND: Read
            ///
            app.Command("read", readCmd =>
            {
                var readApis = readCmd.Option("--alist", "Output the list of API endpoints", CommandOptionType.NoValue);
                var readEntities = readCmd.Option("--elist", "Output the list of entities", CommandOptionType.NoValue);
                var readSamples = readCmd.Option("--slist", "Output the list of samples", CommandOptionType.NoValue);
                var readEntity = readCmd.Option("-e|--entity", "Output the specified entity in JSON format", CommandOptionType.SingleValue);
                var readSql = readCmd.Option("--sql", "Output entity in SQL instead of JSON format (flat structures only)", CommandOptionType.NoValue);
                var readCsv = readCmd.Option("--csv", "Output entity in CSV instead of JSON format (flat structures only)", CommandOptionType.NoValue);
                var readSample = readCmd.Option("-s|--sample", "Output the specified samples file", CommandOptionType.SingleValue);
                var readOutput = readCmd.Option("-o|--output", "Write the output to a file. " +
                        "You can also output JSON arrays to multiple files by including one or more attribute names, " +
                        "e.g. myfile_{id}", CommandOptionType.SingleValue);
                var readYaml = readCmd.Option("--yaml", "Output the entire YAML file", CommandOptionType.NoValue);

                readCmd.OnExecute(() =>
                {
                    if (appFile.HasValue())
                    {
                        if (this.command.IsAdminMode)
                        {
                            Console.WriteLine("Must use default YAML file in admin mode");
                            return 1;
                        }

                        yamlFile = appFile.Value();
                    }

                    if (!readApis.HasValue() && !readEntities.HasValue() && !readSamples.HasValue() &&
                            !readEntity.HasValue() && !readSample.HasValue() && !readYaml.HasValue())
                    {
                        Console.WriteLine("Specify at least one of --alist, --elist, --slist, --entity, --sample or --yaml");
                        return 1;
                    }

                    if (readSql.HasValue() && !readEntity.HasValue())
                    {
                        Console.WriteLine("Must specify --entity if --sql is specified");
                        return 1;
                    }

                    if (readCsv.HasValue() && !readEntity.HasValue())
                    {
                        Console.WriteLine("Must specify --entity if --csv is specified");
                        return 1;
                    }

                    if (readSql.HasValue() && readCsv.HasValue())
                    {
                        Console.WriteLine("Cannot specify both --sql and --csv");
                        return 1;
                    }

                    if (readYaml.HasValue() && (readApis.HasValue() || readEntities.HasValue() || readSamples.HasValue() ||
                            readEntity.HasValue() || readSample.HasValue()))
                    {
                        Console.WriteLine("Cannot specify --alist, --elist, --slist, --entity or --sample if --yaml is specified");
                        return 1;
                    }

                    return this.command.Read(yamlFile, readApis.HasValue(), readEntities.HasValue(), readSamples.HasValue(),
                            readEntity.Value(), readSql.HasValue(), readCsv.HasValue(), readSample.Value(), readOutput.Value(),
                            readYaml.HasValue());
                });
            });

            ///
            /// COMMAND: Write
            ///
            app.Command("write", writeCmd =>
            {
                var writePath = writeCmd.Option("-p|--path", "API endpoint to add to YAML file", CommandOptionType.SingleValue);
                var writeMethod = writeCmd.Option("-m|--method", $"Method associated with API endpoint or " +
                        "'{Api.CRUD_SAMPLE}' to add a complete CRUD set with persistence.", CommandOptionType.SingleValue);
                var writeEntity = writeCmd.Option("-e|--entity", $"Name of entity to add to YAML file or " + 
                        "'{Entity.SAMPLE}' to add a sample entity", CommandOptionType.SingleValue);
                var writeInput = writeCmd.Option("-i|--input", "JSON file to read the entity from", CommandOptionType.SingleValue)
                    .Accepts(v => v.ExistingFile());
                var writeYaml = writeCmd.Option("--yaml", "Overwrite the entire YAML file (admin mode only)", CommandOptionType.NoValue);

                writeCmd.OnExecute(() =>
                {
                    if (appFile.HasValue())
                    {
                        if (this.command.IsAdminMode)
                        {
                            Console.WriteLine("Must use default YAML file in admin mode");
                            return 1;
                        }

                        yamlFile = appFile.Value();
                    }

                    if (request != null)
                    {
                        request = request.Trim();

                        // Treat empty request same as no request (except for yaml option)
                        if (request.Length == 0 && !writeYaml.HasValue())
                        {
                            request = null;
                        }
                    }

                    string path = null;
                    if (writePath.HasValue())
                    {
                        path = writePath.Value();
                    }

                    Api.Methods? method = null;
                    if (writeMethod.HasValue())
                    {
                        if (writeMethod.Value().Equals("~crud"))
                        {
                            if (path != null)
                            {
                                Console.WriteLine("--path must not be specified to write a complete CRUD set");
                            }

                            if (!writeEntity.HasValue())
                            {
                                Console.WriteLine("--entity must be specified to write a complete CRUD set");
                                return 1;
                            }

                            method = null;
                            path = "generated";
                        }
                        else if (!IsValidMethod(writeMethod, out var validMethod))
                        {
                            return 1;
                        }
                        else
                        {
                            method = validMethod;
                        }
                    }

                    if ((path == null && writeMethod.HasValue()) || path != null && !writeMethod.HasValue())
                    {
                        Console.WriteLine("Both --path and --method must be specified together");
                        return 1;
                    }

                    if (writeEntity.HasValue() && !writeEntity.Value().Equals(Entity.SAMPLE))
                    {
                        if (this.command.IsAdminMode && request == null)
                        {
                            Console.WriteLine("Must POST a body if --entity is specified");
                            return 1;
                        }

                        if (!this.command.IsAdminMode && !writeInput.HasValue())
                        {
                            Console.WriteLine("Must specify --input if --entity is specified");
                            return 1;
                        }
                    }

                    if (writeEntity.HasValue() && writeEntity.Value().Equals(Entity.SAMPLE))
                    {
                        if (this.command.IsAdminMode && request != null)
                        {
                            Console.WriteLine("Must not POST a body for sample entity");
                            return 1;
                        }

                        if (!this.command.IsAdminMode && writeInput.HasValue())
                        {
                            Console.WriteLine("Must not specify --input for sample entity");
                            return 1;
                        }
                    }

                    if (!writePath.HasValue() && !writeEntity.HasValue() && !writeYaml.HasValue())
                    {
                        Console.WriteLine("Specify at least one of --path, --entity or --yaml");
                        return 1;
                    }

                    if (this.command.IsAdminMode && writeYaml.HasValue() && request == null)
                    {
                        Console.WriteLine("Must POST a body if --yaml is specified");
                        return 1;
                    }

                    if (writeInput.HasValue() && this.command.IsAdminMode)
                    {
                        Console.WriteLine("Cannot specify --input in admin mode. Use POST and pass a body instead.");
                        return 1;
                    }

                    if (writeYaml.HasValue() && !this.command.IsAdminMode)
                    {
                        Console.WriteLine("Can only specify --yaml in admin mode");
                        return 1;
                    }

                    if (writeYaml.HasValue() && (writePath.HasValue() || writeEntity.HasValue()))
                    {
                        Console.WriteLine("Cannot specify --path or --entity if --yaml is specified");
                        return 1;
                    }

                    return this.command.Write(yamlFile, method, path, writeEntity.Value(),
                            writeInput.Value(), writeYaml.HasValue(), request);
                });
            });

            ///
            /// COMMAND: Persist
            ///
            app.Command("persist", persistCmd =>
            {
                var persistDef = persistCmd.Option("--def", "API endpoint to call (simulate)", CommandOptionType.SingleValue)
                    .IsRequired();
                var persistShow = persistCmd.Option("--show", "Overwrite the entire YAML file (admin mode only)", CommandOptionType.NoValue);
                var persistDelete = persistCmd.Option("--delete", "Overwrite the entire YAML file (admin mode only)", CommandOptionType.NoValue);

                persistCmd.OnExecute(() =>
                {
                    if (!persistDef.HasValue())
                    {
                        Console.WriteLine("Must specify --def");
                        return 1;
                    }

                    if ((!persistShow.HasValue() && !persistDelete.HasValue()) ||
                            (persistShow.HasValue() && persistDelete.HasValue()))
                    {
                        Console.WriteLine("Specify one of --show or --delete");
                        return 1;
                    }

                    return this.command.Persist(persistDef.Value(), persistShow.HasValue(), persistDelete.HasValue());
                });
            });

            app.OnExecute(() =>
            {
                if (appVersion.HasValue())
                {
                    Console.WriteLine($"reqit version {VersionNum}");
                    Console.WriteLine($"Copyright © {CopyrightYear} Scott Vincent");
                }
                else if (appFuncs.HasValue())
                {
                    Console.WriteLine("FUNCTIONS HELP");
                    this.command.ShowFuncHelp();
                }
                else
                {
                    Console.WriteLine("Specify a command");
                    app.ShowHelp();
                }

                return 1;
            });

            // Exit codes:
            //
            // -1 = Success and web server can be started
            //  0 = Success and program can exit
            //  1 = Failure
            //
            int exitCode;
            try
            {
                exitCode = app.Execute(args);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                exitCode = 1;
            }

            if (exitCode >= 0)
            {
                // Don't run web server
                if (!this.command.IsAdminMode)
                {
#if DEBUG
                    Console.WriteLine("\nPress any key to exit");
                    Console.ReadKey();
#endif
                    Environment.Exit(exitCode);
                }
            }

            // Web server will run on return
        }

        private bool IsValidMethod(CommandOption option, out Api.Methods method)
        {
            method = Api.Methods.GET;

            if (option.HasValue())
            {
                try
                {
                    method = (Api.Methods)Enum.Parse(typeof(Api.Methods), option.Value(), true);
                }
                catch (ArgumentException)
                {
                    Console.WriteLine($"--method must be one of: {String.Join(", ", Enum.GetNames(typeof(Api.Methods)))}");
                    return false;
                }
            }

            return true;
        }
    }
}
