using Microsoft.AspNetCore.Http;
using reqit.Models;
using System.Collections.Generic;
using System.IO;

namespace reqit.Services
{
    public interface IDefaultService
    {
        string DoCall(Api.Methods method, string path, IQueryCollection queryStr, Stream body);
    }
}