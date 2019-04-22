using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Http;
using System.Threading;

namespace reqit_mon
{
    /// <summary>
    /// Either copies the specified YAML file to reqit.yaml on the
    /// local filesystem or retrieves reqit.yaml from the reqit server.
    /// 
    /// The local reqit.yaml is then monitored and, whenever it
    /// changes, the latest version gets uploaded to the reqit server.
    /// </summary>
    class Program
    {
        static string yamlFile = "reqit.yaml";

        static void Main(string[] args)
        {
            var help = new HashSet<string>() { "-h", "-help", "--help" };
            string initFile = null;
            string url = "http://localhost:5000";

            if ((args.Length > 0 && help.Contains(args[0])) || args.Length > 3)
            {
                Console.WriteLine("Usage: reqit_mon [-f yaml_file] [server_url]");
                Console.WriteLine($"  where -f uploads the specified YAML file on startup");
                Console.WriteLine($"  and default server_url is {url}");
                return;
            }

            if (args.Length == 1)
            {
                url = args[0];
            }
            else if (args.Length > 1)
            {
                if (!args[0].Equals("-f"))
                {
                    Console.WriteLine("Only valid option is '-f'. Type 'reqit_mon --help' for usage.");
                }

                initFile = args[1];

                if (args.Length == 3)
                {
                    url = args[2];
                }
            }

            string yaml;
            if (initFile == null)
            {
                Console.WriteLine($"Retrieving {yamlFile} from {url}");

                try
                {
                    yaml = CallGet(url, "?cmd=read --yaml");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to retrieve YAML file: {e.Message}");
                    return;
                }
            }
            else
            {
                try
                {
                    yaml = File.ReadAllText(initFile);
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to read file '{initFile}': {e.Message}");
                    return;
                }

                try
                {
                    string message = CallPost(url, "?cmd=write --yaml", yaml);
                    Console.Write($"Uploading initial file '{initFile}': {message}");
                }
                catch (Exception e)
                {
                    Console.WriteLine($"Failed to upload initial file '{initFile}': {e.Message}");
                }
            }

            File.WriteAllText(yamlFile, yaml);
            var lastWrite = File.GetLastWriteTime(yamlFile);

            Console.WriteLine($"Monitoring {yamlFile} ...");
            int fetch = 0;
            while (true)
            {
                var writeTime = File.GetLastWriteTime(yamlFile);
                if (writeTime != lastWrite)
                {
                    try
                    {
                        string message = CallPost(url, "?cmd=write --yaml", File.ReadAllText(yamlFile));
                        Console.Write($"{writeTime.ToString("HH:mm:ss")} upload: {message}");
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to upload YAML file: {e.Message}");
                    }

                    lastWrite = writeTime;
                }

                fetch++;
                if (fetch > 3)
                {
                    // Get yaml file from server and see if it has changed
                    try
                    {
                        string serverYaml = CallGet(url, "?cmd=read --yaml");
                        if (!yaml.Equals(serverYaml))
                        {
                            yaml = serverYaml;
                            File.WriteAllText(yamlFile, yaml);
                            lastWrite = File.GetLastWriteTime(yamlFile);
                            Console.WriteLine($"{lastWrite.ToString("HH:mm:ss")} download: YAML file changed on the server");
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed to retrieve YAML file: {e.Message}");
                        return;
                    }

                    fetch = 0;
                }

                Thread.Sleep(200);
            }
        }

        private static string CallGet(string url, string cmd)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);

            HttpResponseMessage response = client.GetAsync(cmd).Result;
            client.Dispose();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"{(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            return response.Content.ReadAsStringAsync().Result;
        }

        private static string CallPost(string url, string cmd, string body)
        {
            HttpClient client = new HttpClient();
            client.BaseAddress = new Uri(url);

            HttpResponseMessage response = client.PostAsync(cmd, new StringContent(body)).Result;
            client.Dispose();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"{(int)response.StatusCode} - {response.ReasonPhrase}");
            }

            return response.Content.ReadAsStringAsync().Result;
        }
    }
}
