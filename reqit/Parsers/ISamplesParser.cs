using reqit.Models;
using System.Collections.Generic;

namespace reqit.Parsers
{
    public interface ISamplesParser
    {
        List<string> GetSampleNames();
        Samples LoadSamples(string name, string[] lines);
        Samples LoadSamplesFromFile(string samplesName);
    }
}