using reqit.Models;
using System.Collections.Generic;

namespace reqit.Engine
{
    public interface IResolver
    {
        ApiService ApiService { get; }
        void Init(ApiService apiService);
        Api MatchRoute(Api.Methods method, string path, out Cache cache);
        Entity FindEntity(string name);
        List<string> GetSampleNames();
        Samples GetSamples(string samplesName);
        void Resolve(ResolvedValue resolving, Cache cache, IFormatter formatter = null);
    }
}
