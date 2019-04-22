using Microsoft.AspNetCore.Http;
using reqit.CmdLine;
using reqit.Engine;
using reqit.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace reqit.Services
{
    public class DefaultService : IDefaultService
    {
        private readonly ICommand command;
        private readonly ISimulator simulator;

        public DefaultService(ICommand command, ISimulator simulator)
        {
            this.command = command;
            this.simulator = simulator;
        }

        public string DoCall(Api.Methods method, string path, IQueryCollection queryStr, Stream body)
        {
            string request = null;
            if (body != null)
            {
                using (StreamReader reader = new StreamReader(body, Encoding.UTF8))
                {
                    request = reader.ReadToEnd();
                }
            }

            if (path.Equals("/"))
            {
                if (!command.IsAdminMode)
                {
                    throw new ArgumentException("Not running in Admin mode");
                }
                string args = queryStr["command"];
                if (args == null)
                {
                    args = queryStr["cmd"];
                }
                return this.command.AdminMode(args, request);
            }

            Dictionary<string, string> query = queryStr.Keys.Cast<string>()
                .ToDictionary(k => k, v => (string)queryStr[v]);

            return this.simulator.Call(method, path, query, request);
        }
    }
}
