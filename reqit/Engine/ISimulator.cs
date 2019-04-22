using System.Collections.Generic;
using reqit.Models;

namespace reqit.Engine
{
    public interface ISimulator
    {
        string GetRequest(Api.Methods method, string path, Dictionary<string, string> query);
        string Call(Api.Methods method, string path, Dictionary<string, string> query, string request);
    }
}