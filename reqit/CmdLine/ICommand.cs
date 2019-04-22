using reqit.Models;
using System.Collections.Generic;

namespace reqit.CmdLine
{
    public interface ICommand
    {
        bool IsAdminMode { get; set; }
        string AdminMode(string args, string request = "");
        bool Run(string yamlFile, bool isAdminMode);
        int Call(string yamlFile, Api.Methods method, string path, bool wantRequest, string requestFile, string outFile);
        int Read(string yamlFile, bool listApis, bool listEntities, bool listSamples, string entityName, bool useSql,
                bool useCsv, string sampleName, string outFile, bool readYaml);
        int Write(string yamlFile, Api.Methods? method, string path, string entityName, string jsonFile,
                bool writeYaml, string body);
        int Persist(string def, bool showFiles, bool deleteFiles);

        void ShowFuncHelp();
    }
}
