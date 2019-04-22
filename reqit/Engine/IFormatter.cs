using reqit.Models;
using System.Collections.Generic;

namespace reqit.Engine
{
    public interface IFormatter
    {
        string EntityToJson(Entity entity, Cache cache, Dictionary<string, string> mods = null);
        string EntityToSql(Entity entity);
        string EntityToCsv(Entity entity);
    }
}
