using reqit.Models;

namespace reqit.Parsers
{
    public interface IYamlParser
    {
        ApiService ApiService { get; }

        ApiService LoadFile(string yamlFile);
        ApiService Load(string yamlString);
    }
}