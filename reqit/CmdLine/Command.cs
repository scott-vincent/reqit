using McMaster.Extensions.CommandLineUtils;
using reqit.Engine;
using reqit.Models;
using reqit.Parsers;
using reqit.Services;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace reqit.CmdLine
{
    /// <summary>
    /// Runs the command specified on the command line. All parameters
    /// have already been validated and are passed in.
    ///
    /// Any optional parameters will be passed in as null if they were
    /// not specified on the command line.
    /// </summary>
    public class Command : ICommand
    {
        private FileStream redirectStream;
        private StreamWriter redirectWriter;
        private TextWriter consoleWriter = Console.Out;

        public bool IsAdminMode { get; set; }

        private readonly IYamlParser yamlParser;
        private readonly IJsonParser jsonParser;
        private readonly IResolver resolver;
        private readonly IFormatter formatter;
        private readonly IFuncs funcs;
        private readonly ISimulator simulator;

        public Command(IYamlParser yamlParser, IJsonParser jsonParser, IResolver resolver,
                IFormatter formatter, IFuncs funcs, ISimulator simulator)
        {
            this.yamlParser = yamlParser;
            this.jsonParser = jsonParser;
            this.resolver = resolver;
            this.formatter = formatter;
            this.funcs = funcs;
            this.simulator = simulator;

            IsAdminMode = false;
        }

        /// <summary>
        /// This method is called from the web server when running
        /// in admin mode (command-line as a service).
        /// 
        /// If called from POST the request will be populated.
        /// It returns the response in text format.
        /// </summary>
        public string AdminMode(string argStr, string request = null)
        {
            if (argStr == null)
            {
                return "Try: http://<host:port>?command=--help";
            }

            var args = Regex.Split(argStr, @"\s+");

            // Don't allow Run (unless param 2 is --help) as we're already running!
            if (args[0].Equals("run", StringComparison.CurrentCultureIgnoreCase) &&
                    (args.Length < 2 || !args[1].Equals("--help")))
            {
                string msg = "Welcome to ReqIt Command-Line as a Service.\n\n" +
                        "YAML file has been re-loaded.";
                InitResolver(YamlParser.DEFAULT, false);
                return msg;
            }

            // Need to capture console output
            string tempFile = Path.GetTempFileName();
            if (!redirectOutput(tempFile, true))
            {
                return "Command failed: Could not redirect output";
            }

            var main = new MyMain(this);
            main.Main(args, request);

            redirectOutput(tempFile, false);
            string output = "";
            for (int retry = 0; retry < 8; retry++)
            {
                try
                {
                    output = File.ReadAllText(tempFile);
                    break;
                }
                catch (Exception e)
                {
                    // Try again - Gets a file in use error sometimes
                    output = e.Message;
                    Thread.Sleep(100);
                }
            }

            File.Delete(tempFile);
            return output;
        }

        /// <summary>
        /// Load the YAML file and initialise the resolver.
        /// Returns false if YAML file cannot be parsed or
        /// needApis is true and file contains no APIs.
        /// </summary>
        private bool InitResolver(string yamlFile, bool needApis)
        {
            ApiService apiService;
            try
            {
                apiService = this.yamlParser.LoadFile(yamlFile);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to load YAML file {yamlFile}: {e.Message}");
                return false;
            }

            // Once YAML file is loaded the resolver can be initialised
            this.resolver.Init(apiService);

            if (needApis && this.resolver.ApiService.Apis.Count == 0) {
                Console.WriteLine($"At least one api must be defined in YAML file {yamlFile}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// COMMAND: Run
        ///
        /// Starts the web server to process all API endpoints specified in the YAML file.
        /// Returns true to start the web server or false if error.
        /// 
        /// In admin mode you can call the root endpoint to run a command. Add ?args="your args",
        /// e.g. GET http://host:port?args="--help"
        /// </summary>
        public bool Run(string yamlFile, bool isAdminMode)
        {
            IsAdminMode = isAdminMode;

            // Not an error if yaml file is empty when in admin mode (can be uploaded later)
            if (!InitResolver(yamlFile, !isAdminMode))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// COMMAND: Call
        ///
        /// Simulates calling the specified endpoint and writes the response to stdout or a file.
        /// Can also output the request rather than the response if required.
        ///
        /// The response will have any modifiers applied that are specified in the YAML file's
        /// api response definition which is what makes the output different from the Read
        /// command's entity output.
        /// </summary>
        public int Call(string yamlFile, Api.Methods method, string path, bool wantRequest, string requestFile, string outFile)
        {
            if (!InitResolver(yamlFile, true))
            {
                return 1;
            }

            // Redirect output if outFile specified
            if (outFile != null && !IsAdminMode)
            {
                if (!redirectOutput(outFile, true))
                {
                    return 1;
                }
            }

            // Extract query string from path and process
            var query = new Dictionary<string, string>();
            int sep = path.IndexOf('?');
            if (sep != -1)
            {
                string queryStr = path.Substring(sep + 1);
                path = path.Substring(0, sep);
                var queryPairs = queryStr.Split('&');
                foreach (var queryPair in queryPairs)
                {
                    var pair = queryPair.Split('=');
                    var key = pair[0].Trim();
                    if (key.Length > 0)
                    {
                        if (pair.Length == 1)
                        {
                            query.Add(key, "true");
                        }
                        else
                        {
                            query.Add(key, pair[1].Trim());
                        }
                    }
                }
            }

            string body = null;
            if (requestFile != null)
            {
                body = File.ReadAllText(requestFile);
            }

            string response;
            try
            {
                if (wantRequest)
                {
                    response = this.simulator.GetRequest(method, path, query);
                }
                else
                {
                    response = this.simulator.Call(method, path, query, body);
                }
            }
            catch (KeyNotFoundException)
            {
                response = "404 Not Found";
            }
            catch (ArgumentException e)
            {
                response = $"400 Bad Request - {e.Message}";
            }

            Console.WriteLine(response);

            if (outFile != null && !IsAdminMode)
            {
                redirectOutput(outFile, false);
                Console.WriteLine($"Output written to file: {outFile}");
            }

            return 0;
        }

        /// <summary>
        /// COMMAND: Read
        ///
        /// Reads the requested entity or api from the YAML file and writes it to stdout or a file.
        /// The resolver is used to evaluate all functions so concrete values are output.
        /// Entities are written in JSON format unless useSql or useCsv is specified.
        /// </summary>
        public int Read(string yamlFile, bool listApis, bool listEntities, bool listSamples, string entityName,
                bool useSql, bool useCsv, string sampleName, string outFile, bool readYaml)
        {
            if (!InitResolver(yamlFile, false))
            {
                return 1;
            }

            // Redirect output if outFile specified
            Persistence multiOut = null;
            if (outFile != null)
            {
                try
                {
                    multiOut = new Persistence(outFile);
                    if (multiOut.OutputVars == null)
                    {
                        multiOut = null;
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"--output '{outFile}' is invalid - {e.Message}");
                    return 1;
                }

                // Defer redirect if we are outputting multiple files
                if (multiOut == null)
                {
                    if (!redirectOutput(outFile, true))
                    {
                        return 1;
                    }
                }
            }

            // Output the list of apis
            if (listApis)
            {
                if (this.resolver.ApiService.Apis.Count == 0)
                {
                    Console.WriteLine("No APIs found");
                }
                else
                {
                    foreach (Api api in this.resolver.ApiService.Apis)
                    {
                        Console.WriteLine(api.ToString());
                    }
                }

                if (listEntities || entityName != null)
                {
                    Console.WriteLine();
                }
            }

            // Output the list of entities
            if (listEntities)
            {
                var names = this.resolver.ApiService.EntityRoot.ChildOrder;
                if (names.Count == 0)
                {
                    Console.WriteLine("No entities found");
                }
                else
                {
                    names.OrderBy(x => x).ToList().ForEach(i => Console.WriteLine($"{i}"));
                }
            }

            // Output the specified entity
            string output = null;
            if (entityName != null)
            {
                Entity entity;
                try
                {
                    entity = this.resolver.FindEntity(entityName);

                    if (useSql)
                    {
                        if (multiOut != null)
                        {
                            throw new Exception("Cannot write SQL to multiple files");
                        }

                        Console.WriteLine(this.formatter.EntityToSql(entity));
                    }
                    else if (useCsv)
                    {
                        if (multiOut != null)
                        {
                            throw new Exception("Cannot write CSV to multiple files");
                        }

                        Console.WriteLine(this.formatter.EntityToCsv(entity));
                    }
                    else
                    {
                        if (multiOut == null)
                        {
                            Console.WriteLine(this.formatter.EntityToJson(entity, new Cache()));
                        }
                        else
                        {
                            output = this.formatter.EntityToJson(entity, new Cache());
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                    if (multiOut != null)
                    {
                        // Cancel multi output
                        outFile = null;
                        multiOut = null;
                    }
                }
            }

            // Output the list of samples
            if (listSamples)
            {
                List<string> sampleNames;
                try
                {
                    sampleNames = this.resolver.GetSampleNames();

                    if (sampleNames.Count == 0)
                    {
                        Console.WriteLine("No sample files found");
                    }
                    else
                    {
                        foreach (var name in sampleNames)
                        {
                            Console.WriteLine(name);
                        }
                    }
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Cannot read samples folder: {e.Message}");
                }
            }

            // Output the specified sample file
            if (sampleName != null)
            {
                try
                {
                    var samples = this.resolver.GetSamples(sampleName);
                    Console.WriteLine(samples);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e.Message);
                }
            }

            // Output the yaml file
            if (readYaml)
            {
                Console.Write(File.ReadAllText(yamlFile));
            }

            if (outFile != null)
            {
                if (multiOut == null)
                {
                    redirectOutput(outFile, false);
                    Console.WriteLine($"Output written to file: {outFile}");
                }
                else
                {
                    if (output == null || output.Length == 0)
                    {
                        Console.WriteLine($"Output cannot be written to multiple output files");
                    }
                    else
                    {
                        // Convert output to multiple output files
                        try
                        {
                            int count = WriteMulti(output, multiOut);
                            Console.WriteLine($"{count} files written");
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to write to multiple files: {e.Message}");
                        }
                    }
                }
            }

            return 0;
        }

        /// <summary>
        /// COMMAND: Write
        ///
        /// Reads the supplied info (or JSON file) and appends the data to the YAML file
        /// or creates the YAML file it if it doesn't already exist.
        /// </summary>
        public int Write(string yamlFile, Api.Methods? method, string path, string entityName,
                string jsonFile, bool writeYaml, string request)
        {
            // Path must be valid
            if (path != null)
            {
                path = cleanPath(path);
                if (path.Contains(':') || path.Contains('\\'))
                {
                    Console.WriteLine("--path must not contain a colon or backslash");
                    return 1;
                }
            }

            if (!writeYaml && File.Exists(yamlFile))
            {
                if (!InitResolver(yamlFile, false))
                {
                    return 1;
                }

                if (path != null)
                {
                    // Matching route must not already exist
                    Api.Methods checkMethod = method ?? Api.Methods.GET;
                    var matchedApi = this.resolver.MatchRoute(checkMethod, path, out var cache);
                    if (matchedApi != null)
                    {
                        Console.WriteLine($"Matching route '{checkMethod.ToString()} {matchedApi.Path}' already exists in YAML file {yamlFile}");
                        return 1;
                    }
                }
            }

            bool isSample = false;
            string entityFile = jsonFile;
            if (entityName != null && entityName.Equals(Entity.SAMPLE))
            {
                isSample = true;
                entityName = Entity.SAMPLE.Substring(1);
                entityFile = YamlParser.SAMPLE;
            }

            if (request != null)
            {
                if (writeYaml)
                {
                    // Replace entire yaml file after validating new version
                    if (ReplaceYaml(yamlFile, request))
                    {
                        Console.WriteLine($"YAML file has been replaced and reloaded");
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }

                // Need to treat body same as if an input file was specified
                entityFile = "from request";
            }

            Entity entity = null;
            if (entityName != null)
            {
                if (File.Exists(yamlFile))
                {
                    try
                    {
                        entity = this.resolver.FindEntity(entityName);
                    }
                    catch (Exception)
                    {
                        entity = null;
                    }
                }

                if (entityFile == null)
                {
                    // Entity must exist if it is not being added. This is used to add an existing
                    // entity as the request/response of an API that is being written.
                    if (entity == null)
                    {
                        Console.WriteLine($"Entity '{entityName}' not found in YAML file {yamlFile}");
                        return 1;
                    }
                }
                else
                {
                    // Entity must not exist if it is being added
                    if (entity != null)
                    {
                        Console.WriteLine($"Entity '{entityName}' already exists in YAML file {yamlFile}");
                        return 1;
                    }

                    if (isSample)
                    {
                        // Load entity from sample YAML file instead
                        try
                        {
                            ApiService sampleService = this.yamlParser.LoadFile(entityFile);
                            entity = sampleService.EntityRoot.ChildEntities["sample"];
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to load sample YAML file {entityFile}: {e.Message}");
                            return 1;
                        }
                    }
                    else if (request != null)
                    {
                        // JSON body must be valid
                        try
                        {
                            entity = this.jsonParser.LoadEntity(entityName, request);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to parse request JSON: {e.Message}");
                            return 1;
                        }
                    }
                    else
                    {
                        // JSON file must be valid
                        try
                        {
                            entity = this.jsonParser.LoadEntityFromFile(entityName, entityFile);
                        }
                        catch (Exception e)
                        {
                            Console.WriteLine($"Failed to load JSON file {entityFile}: {e.Message}");
                            return 1;
                        }
                    }
                }
            }

            // If YAML file already exists, back it up before updating it
            string[] lines = new string[0];
            int entityLastLine = -1;
            int aliasLastLine = -1;
            int apiLastLine = -1;

            bool backedUp = false;
            if (File.Exists(yamlFile))
            {
                BackupFile(yamlFile);
                backedUp = true;

                // Read the YAML file as an array of lines
                lines = File.ReadAllLines(yamlFile);

                // Find the end of the entity and api sections (so we can append to them)
                bool inEntity = false;
                bool inAlias = false;
                bool inApi = false;
                int lastLine = -1;
                for (int i = 0; i <= lines.Length; i++)
                {
                    string line;
                    if (i == lines.Length)
                    {
                        // Force start of new section
                        line = "<END>";
                    }
                    else
                    {
                        line = lines[i].TrimEnd();
                    }

                    if (line.Length > 0 && line[0] != ' ' && line[0] != '#')
                    {
                        // New section
                        if (inEntity)
                        {
                            entityLastLine = lastLine;
                        }
                        else if (inAlias)
                        {
                            aliasLastLine = lastLine;
                        }
                        else if (inApi)
                        {
                            apiLastLine = lastLine;
                        }

                        inEntity = line.Equals("entity:");
                        inAlias = line.Equals("alias:");
                        inApi = line.Equals("api:");
                    }

                    if (line.Length > 0)
                    {
                        lastLine = i;
                    }
                }
            }

            // Write the YAML file and insert the new data
            try
            {
                using (StreamWriter file = new StreamWriter(yamlFile))
                {
                    for (int i = -1; i < lines.Length; i++)
                    {
                        if (i >= 0)
                        {
                            file.WriteLine(lines[i]);
                        }

                        // Add entity to YAML file
                        if (entityFile != null && i == entityLastLine)
                        {
                            if (entityLastLine == -1)
                            {
                                file.WriteLine($"entity:");
                                file.Write(Entity.PrettyPrint(entity, "  "));
                                if (aliasLastLine != -1 || apiLastLine != -1)
                                {
                                    file.WriteLine();
                                }
                            }
                            else
                            {
                                file.WriteLine();
                                file.Write(Entity.PrettyPrint(entity, "  "));
                            }
                        }

                        // Add alias to YAML file (complete CRUD set only)
                        if (method == null && path != null && entity != null && i == aliasLastLine)
                        {
                            string aliasDef = $"  {entity.Name}_list: \"[{entity.Name}, 5]\"";
                            if (aliasLastLine == -1)
                            {
                                file.WriteLine($"\nalias:\n{aliasDef}");
                                if (apiLastLine != -1)
                                {
                                    file.WriteLine();
                                }
                            }
                            else
                            {
                                file.WriteLine(aliasDef);
                            }
                        }

                        // Add API to YAML file
                        if (path != null && ((i != -1 && i == apiLastLine) || (apiLastLine == -1 && i == lines.Length - 1)))
                        {
                            if (apiLastLine == -1)
                            {
                                if (lines.Length > 0 || entityFile != null)
                                {
                                    file.WriteLine();
                                }
                                file.WriteLine($"api:");
                            }
                            else
                            {
                                file.WriteLine();
                            }

                            WriteApis(file, method, path, entity);
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to write YAML file '{yamlFile}': {e.Message}");
                if (backedUp)
                {
                    RestoreFile(yamlFile);
                    return 1;
                }
            }

            Console.WriteLine($"Updated file: {yamlFile}");

            if (IsAdminMode)
            {
                // Reload yaml file to validate it
                if (!InitResolver(yamlFile, false))
                {
                    return 1;
                }

                Console.WriteLine($"YAML file has been reloaded");
            }

            return 0;
        }

        /// <summary>
        /// COMMAND: Persist
        ///
        /// Shows or deletes the persisted files that match the supplied definition
        /// </summary>
        public int Persist(string def, bool showFiles, bool deleteFiles)
        {
            Persistence persist = null;
            try
            {
                persist = new Persistence(def);
            }
            catch (Exception e)
            {
                Console.WriteLine($"--def not valid: {e.Message}");
                return 1;
            }

            int deleted = 0;
            string persistFolder = Path.GetDirectoryName(persist.OutputDef);
            if (persistFolder != "")
            {
                persistFolder += "/";
            }
            foreach (var filename in Directory.GetFiles(persist.Folder, persist.WildPattern).OrderBy(f => f))
            {
                if (showFiles)
                {
                    Console.WriteLine(persistFolder + Path.GetFileName(filename));
                }
                else if (deleteFiles)
                {
                    try
                    {
                        File.Delete(filename);
                        deleted++;
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to delete file '{filename}': {e.Message}");
                    }
                }
            }

            if (deleteFiles)
            {
                Console.WriteLine($"{deleted} files deleted");
            }

            return 0;
        }

        public void ShowFuncHelp()
        {
            FuncHelp("DATE", this.funcs.FuncDate, null, null);
            FuncHelp("GEN", this.funcs.FuncGen, null, null);
            FuncHelp("IF", null, this.funcs.FuncIf, null);
            FuncHelp("MATH", null, this.funcs.FuncMath, null);
            FuncHelp("NUM", null, this.funcs.FuncNum, null);
            FuncHelp("PICK", this.funcs.FuncPick, null, null);
            FuncHelp("RAND", this.funcs.FuncRand, null, null);
            FuncHelp("REF", null, null, this.funcs.FuncRef);
            FuncHelp("SAMPLE", null, null, this.funcs.FuncSample);
            FuncHelp("STR", this.funcs.FuncStr, null, null);
            FuncHelp("TIME", this.funcs.FuncTime, null, null);
        }

        private delegate string FuncType1(string called, string[] args);
        private delegate string FuncType2(string called, string[] args, Cache cache, string parent, IResolver resolver);
        private delegate string FuncType3(string called, string[] args, Cache cache, string parent, IResolver resolver,
                out Sample.Genders gender);

        private void FuncHelp(string funcName, FuncType1 func1, FuncType2 func2, FuncType3 func3)
        {
            Console.WriteLine($"\n{funcName}");

            try
            {
                if (func1 != null)
                {
                    func1("", new string[] { "--help" });
                }
                else if (func2 != null)
                {
                    func2("", new string[] { "--help" }, null, null, null);
                }
                else
                {
                    func3("", new string[] { "--help" }, null, null, null, out var gender);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }
        }

        /// <summary>
        /// Remove host from path and make sure it starts with a slash
        /// </summary>
        private string cleanPath(string path)
        {
            if (path.StartsWith("http://", true, null) || path.StartsWith("https://", true, null))
            {
                int hostEnd = path.IndexOf("/", 8);
                if (hostEnd != -1)
                {
                    path = path.Substring(hostEnd);
                }
            }

            if (!path.StartsWith('/'))
            {
                path = "/" + path;
            }

            return path;
        }

        /// <summary>
        /// Extracts variables from the path as a comma-separated string
        /// e.g. path=/first/second/{var1}/third/{var2}
        /// returns "var1=~path.var1, var2=~path.var2"
        /// </summary>
        private string ExtractPathVars(string path)
        {
            StringBuilder pathVars = new StringBuilder();

            Route route = new Route(path);
            var vars = route.GetVars();

            foreach (var v in vars)
            {
                if (pathVars.Length > 0)
                {
                    pathVars.Append(", ");
                }
                pathVars.Append($"{v}=~path.{v}");
            }

            return pathVars.ToString();
        }

        private bool redirectOutput(string filename, bool turnOn)
        {
            if (!turnOn)
            {
                // Stop output redirection
                Console.SetOut(consoleWriter);
                redirectWriter.Close();
                return true;
            }

            // Create folder if it doesn't exist
            string folder;
            try
            {
                folder = Path.GetDirectoryName(filename);
                if (!string.IsNullOrEmpty(folder) && !Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to create folder for '{filename}': {e.Message}");
            }

            // if file already exists, back it up
            BackupFile(filename);
            File.Delete(filename);

            try
            {
                redirectStream = new FileStream(filename, FileMode.Create, FileAccess.Write);
                redirectWriter = new StreamWriter(redirectStream);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Cannot write to file '{filename}': {e.Message}");
                return false;
            }

            Console.SetOut(redirectWriter);
            return true;
        }

        private void BackupFile(string filename)
        {
            if (File.Exists(filename))
            {
                string bakFile = BackupName(filename);
                File.Delete(bakFile);
                File.Copy(filename, bakFile);
            }
        }

        private void RestoreFile(string filename)
        {
            string bakFile = BackupName(filename);

            if (File.Exists(bakFile))
            {
                File.Delete(filename);
                File.Copy(bakFile, filename);
            }
        }

        private string BackupName(string filename)
        {
            string bakName;
            int suffixPos = filename.LastIndexOf('.');
            if (suffixPos == -1)
            {
                bakName = filename + ".bak";
            }
            else
            {
                bakName = filename.Substring(0, suffixPos) + ".bak";
            }

            return bakName;
        }

        /// <summary>
        /// Replaces the entire YAML file after backing it up.
        /// Returns false (and does not replace file) if the
        /// new YAML is not valid or an error occurs.
        /// </summary>
        private bool ReplaceYaml(string yamlFile, string yaml)
        {
            ApiService apiService;
            try
            {
                apiService = this.yamlParser.Load(yaml);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Supplied YAML is not valid: {e.Message}");

                // Reload original yaml file
                InitResolver(yamlFile, false);
                return false;
            }

            bool backedUp = false;
            if (File.Exists(yamlFile))
            {
                BackupFile(yamlFile);
                backedUp = true;
            }

            // Write new yaml file
            try
            {
                File.WriteAllText(yamlFile, yaml);
            }
            catch (Exception e)
            {
                Console.WriteLine($"Failed to write YAML file '{yamlFile}': {e.Message}");
                if (backedUp)
                {
                    RestoreFile(yamlFile);
                }
                return false;
            }

            // Initialise with new yaml file
            if (!InitResolver(yamlFile, false))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// Add a new API to the YAML file (or full set of CRUD
        /// APIs if method is null). Guesses the request/response
        /// to add based on the supplied entity name. For CRUD,
        /// persistence info is also added.
        /// </summary>
        private void WriteApis(StreamWriter file, Api.Methods? method, string path, Entity entity)
        {
            if (method == null)
            {
                string firstAttr = entity.ChildOrder.FirstOrDefault();
                if (firstAttr == null)
                {
                    firstAttr = "id";
                }

                // Make path plural entity name
                path = $"/{entity.Name}s";
                string pathWithVar = $"{path}/{{{firstAttr}}}";
                string persist = $"{entity.Name}s/{entity.Name}_{{{firstAttr}}}";
                string delPersist = $"{entity.Name}s/{entity.Name}_{{path.{firstAttr}}}";

                WriteApi(file, Api.Methods.GET, path, entity, persist);
                file.WriteLine();
                WriteApi(file, Api.Methods.GET, pathWithVar, entity, persist);
                file.WriteLine();
                WriteApi(file, Api.Methods.PUT, pathWithVar, entity, persist);
                file.WriteLine();
                WriteApi(file, Api.Methods.PATCH, pathWithVar, entity, persist);
                file.WriteLine();
                WriteApi(file, Api.Methods.POST, path, entity, persist);
                file.WriteLine();
                WriteApi(file, Api.Methods.DELETE, pathWithVar, entity, delPersist);
            }
            else
            {
                WriteApi(file, method ?? Api.Methods.GET, path, entity);
            }
        }

        private void WriteApi(StreamWriter file, Api.Methods method, string path, Entity entity, string persist = null)
        {
            file.WriteLine($"  - method: {method}");
            file.WriteLine($"    path: \"{path}\"");

            if (entity != null)
            {
                // Guess Request/Response based on standard methods
                if (method == Api.Methods.GET)
                {
                    // Response only with modifiers to replace attributes with path variables
                    string pathVars = ExtractPathVars(path);
                    if (pathVars.Length > 0)
                    {
                        file.WriteLine($"    response: {entity.Name}, {pathVars}");
                    }
                    else
                    {
                        file.WriteLine($"    response: \"[{entity.Name}]\"");
                    }
                }
                else if (method == Api.Methods.PUT || method == Api.Methods.PATCH)
                {
                    // Request and response
                    string pathVars = ExtractPathVars(path);
                    file.WriteLine($"    request: {entity.Name}, {pathVars}");
                    file.WriteLine($"    response: {entity.Name}, *=~request");
                }
                else if (method == Api.Methods.POST)
                {
                    // Request with modifier to remove first attribute (id) and response
                    string firstAttr = entity.ChildOrder.FirstOrDefault();
                    if (firstAttr != null)
                    {
                        file.WriteLine($"    request: {entity.Name}, !{firstAttr}");
                    }
                    else
                    {
                        file.WriteLine($"    request: {entity.Name}");
                    }

                    file.WriteLine($"    response: {entity.Name}, *=~request");
                }

                if (persist != null)
                {
                    file.WriteLine($"    persist: \"{persist}\"");
                }
            }
        }

        /// <summary>
        /// Take output which is a JSON array and output to multiple files where
        /// output filename is based on one or more attributes within the entity.
        /// </summary>
        private int WriteMulti(string output, Persistence multiOut)
        {
            int count = 0;

            string fullName = multiOut.OutputDef;
            if (!Path.IsPathRooted(fullName))
            {
                fullName = Path.Combine(Persistence.PERSIST_DIR, fullName);
            }

            string folder = Path.GetDirectoryName(fullName);
            string fileDef = Path.GetFileName(fullName);

            // Make sure folder exists
            try
            {
                if (!Directory.Exists(folder))
                {
                    Directory.CreateDirectory(folder);
                }
}
            catch (Exception e)
            {
                throw new Exception($"Failed to create folder for '{multiOut.OutputDef}': {e.Message}");
            }

            int lastPos = output.Length - 1;
            if (output[0] == '[')
            {
                // Output is an array of objects
                if (output[1] != '{' || output[lastPos - 1] != '}' || output[lastPos] != ']')
                {
                    throw new Exception("Output must be an array of objects in JSON format");
                }

                foreach (var entity in this.jsonParser.ArrayEntities(output, multiOut))
                {
                    WriteSingle(folder, fileDef, entity);
                    count++;
                }
            }
            else
            {
                // Output is a single object
                if (output[0] != '{' || output[lastPos] != '}')
                {
                    throw new Exception("Output must be an object in JSON format");
                }

                var entity = this.jsonParser.ObjectEntity(output, multiOut);
                WriteSingle(folder, fileDef, entity);
                count++;
            }

            return count;
        }

        private void WriteSingle(string folder, string fileDef, PersistEntity entity)
        {
            string filename = Path.Combine(folder, entity.GetFilename(fileDef));

            if (File.Exists(filename))
            {
                Console.WriteLine($"Overwriting file: {filename}");
            }

            try
            {
                File.WriteAllText(filename, entity.Json);
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to write file '{filename}': {e.Message}");
            }
        }
    }
}
