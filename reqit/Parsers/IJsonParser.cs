using reqit.Models;
using System.Collections.Generic;

namespace reqit.Parsers
{
    public interface IJsonParser
    {
        Entity LoadEntityFromFile(string name, string jsonFile);
        Entity LoadEntity(string name, string jsonString);
        IEnumerable<PersistEntity> ArrayEntities(string jsonArray, Persistence persist);
        PersistEntity ObjectEntity(string jsonObject, Persistence persist);
    }
}
