using reqit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace reqit.Engine
{
    public class Formatter : IFormatter
    {
        private Random random = new Random();

        private readonly IResolver resolver;

        public Formatter(IResolver resolver)
        {
            this.resolver = resolver;
        }

        public string EntityToJson(Entity entity, Cache cache, Dictionary<string, string> mods = null)
        {
            return ToJson(null, entity, cache, mods, 0);
        }

        /// <summary>
        /// Outputs the entity in JSON format after resolving all values
        /// </summary>
        private string ToJson(string parentName, Entity entity, Cache cache,
                Dictionary<string, string> mods, int nestedLevel, string knownName = "")
        {
            if (knownName.Length == 0)
            {
                knownName = entity.Name;
            }

            string fullName;
            if (parentName != null)
            {
                int parentLast = parentName.LastIndexOf('.');
                if (parentLast != -1 && parentName[parentLast + 1] == '~' && entity.Type == Entity.Types.PARENT)
                {
                    fullName = parentName;
                }
                else
                {
                    fullName = parentName + "." + entity.Name;
                }
            }
            else
            {
                fullName = entity.Name;
            }

            // Get mod
            string mod = null;
            bool modAll = false;
            if (mods != null)
            {
                if (mods.TryGetValue(fullName, out mod))
                {
                    if (mod == null)
                    {
                        // Attribute should be ignored
                        return "";
                    }
                }
                else if (mods.TryGetValue("*", out mod))
                {
                    modAll = true;
                }
            }

            if (nestedLevel > 500 && parentName != null)
            {
                throw new Exception($"Cannot resolve entity '{parentName}' as it contains a circular reference");
            }

            StringBuilder sb = new StringBuilder();
            bool isArrayChild = knownName.StartsWith("~") || knownName.StartsWith('#');

            if (entity.Type == Entity.Types.PARENT | entity.Type == Entity.Types.ARRAY)
            {
                if (parentName != null && !isArrayChild)
                {
                    sb.Append($"{entity.Name}: ");
                }

                if (entity.Type == Entity.Types.PARENT)
                {
                    sb.Append("{");
                }
                else
                {
                    sb.Append("[");
                }

                bool isFirst = true;
                foreach (var entityName in entity.ChildOrder)
                {
                    var toJsonStr = ToJson(fullName, entity.ChildEntities[entityName], cache, mods, nestedLevel + 1);
                    if (toJsonStr.Length > 0)
                    {
                        if (!isFirst)
                        {
                            sb.Append(", ");
                        }
                        else
                        {
                            isFirst = false;
                        }

                        sb.Append(toJsonStr);
                    }
                }

                if (entity.Type == Entity.Types.PARENT)
                {
                    sb.Append("}");
                }
                else
                {
                    sb.Append("]");
                }
            }
            else if (entity.Type == Entity.Types.REF)
            {
                Entity refEntity;

                try
                {
                    refEntity = this.resolver.FindEntity(entity.Value);
                }
                catch (Exception e)
                {
                    throw new Exception($"Attribute '{knownName}' has bad reference '{entity.Value}': {e.Message}");
                }

                string refParent;
                if (modAll)
                {
                    refParent = fullName;
                }
                else
                {
                    refParent = null;
                }

                if (refEntity.Type == Entity.Types.REPEAT)
                {
                    sb.Append($"{ToJson(refParent, refEntity, cache, mods, nestedLevel + 1, "#" + knownName)}");
                }
                else
                {
                    sb.Append($"{ToJson(refParent, refEntity, cache, mods, nestedLevel + 1, knownName)}");
                }
            }
            else if (entity.Type == Entity.Types.REPEAT)
            {
                if (knownName.StartsWith('#'))
                {
                    sb.Append($"{knownName.Substring(1)}: ");
                }

                Entity refEntity;
                try
                {
                    refEntity = this.resolver.FindEntity(entity.Value);
                }
                catch (Exception)
                {
                    throw new Exception($"Attribute '{knownName}' references unknown entity '{entity.Value}'");
                }

                string modParent;
                int repeatCount;
                if (modAll && mod[0] == '~')
                {
                    modParent = parentName + ".~";
                    string childGroup = mod.Substring(1) + "." + modParent;
                    repeatCount = 0;
                    while (true)
                    {
                        if (!cache.HasValue(childGroup + (repeatCount + 1)))
                        {
                            break;
                        }
                        repeatCount++;
                    }
                }
                else
                {
                    // Choose a random repeat count in the range specified
                    modParent = null;
                    repeatCount = random.Next(entity.Min, entity.Max + 1);
                }

                sb.Append("[");
                for (int i = 0; i < repeatCount; i++)
                {
                    if (i > 0)
                    {
                        sb.Append(", ");
                    }

                    if (modParent == null)
                    {
                        // Use a clean cache for each instance (so they are not related)
                        sb.Append($"{ToJson(null, refEntity, new Cache(), mods, nestedLevel + 1, knownName)}");
                    }
                    else
                    {
                        // Getting data from request which is in cache
                        sb.Append($"{ToJson(modParent + (i + 1), refEntity, cache, mods, nestedLevel + 1, knownName)}");
                    }
                }
                sb.Append("]");
            }
            else
            {
                if (!isArrayChild)
                {
                    sb.Append($"{knownName}: ");
                }

                ResolvedValue resolving = null;

                if (modAll)
                {
                    // Apply mod prefix to attribute name.
                    // e.g. if *=~request, employee.id becomes ~request.employee.id
                    mod += "." + fullName;
                    if (mod[0] == '~')
                    {
                        mod = mod.Substring(1);
                    }

                    // Retrieve from cache
                    try
                    {
                        resolving = cache.GetValue(mod);
                    }
                    catch (Exception e)
                    {
                        // Not an error if request or query value not found
                        if (!mod.StartsWith("request.") && !mod.StartsWith("query."))
                        {
                            throw e;
                        }
                        resolving = null;
                    }
                }

                if (resolving == null)
                {
                    // No mod applied
                    resolving = new ResolvedValue(fullName, entity.Type, entity.Value);
                    this.resolver.Resolve(resolving, cache, this);
                }

                sb.Append(resolving.GetValue(false));
            }

            return sb.ToString();
        }

        public string EntityToSql(Entity entity)
        {
            return ToSqlOrCsv(entity, true);
        }

        public string EntityToCsv(Entity entity)
        {
            return ToSqlOrCsv(entity, false);
        }

        private string ToSqlOrCsv(Entity entity, bool isSql)
        {
            string entityName = entity.Name;
            var attributes = new List<ResolvedValue>();
            bool isRepeating = AddAttributes(attributes, null, entity);

            int repeatCount = 1;
            if (isRepeating)
            {
                Entity repeatEntity;
                try
                {
                    repeatEntity = this.resolver.FindEntity(entity.Value);
                }
                catch (Exception)
                {
                    throw new Exception($"Entity '{entity.Name}' references unknown entity '{entity.Value}'");
                }

                if (AddAttributes(attributes, null, repeatEntity, entity.Name))
                {
                    throw new Exception($"Cannot output entity '{entity.Name}' in SQL or CSV format as '{entity.Value}' contains nested repeats. " +
                        "Only flat structures can be output in this format.");
                }

                // Choose a random repeat count in the range specified
                repeatCount = random.Next(entity.Min, entity.Max + 1);

                int pos = entity.Value.LastIndexOf('.');
                if (pos == -1)
                {
                    entityName = entity.Value;
                }
                else
                {
                    entityName = entity.Value.Substring(0, pos);
                }
            }

            StringBuilder sb = new StringBuilder();

            if (isSql)
            {
                sb.Append($"INSERT INTO {entityName} (");
            }

            // Column names
            bool first = true;
            foreach (var attribute in attributes)
            {
                if (first)
                {
                    first = false;
                }
                else
                {
                    sb.Append(",");
                }

                string attribName;
                int pos = attribute.Name.LastIndexOf('.');
                if (pos == -1)
                {
                    if (attribute.Name[0] == '~')
                    {
                        attribName = attribute.Name.Substring(1);
                    }
                    else
                    {
                        attribName = attribute.Name;
                    }
                }
                else
                {
                    attribName = attribute.Name.Substring(pos + 1);
                }

                if (isSql)
                {
                    sb.Append($"'{attribName}'");
                }
                else
                {
                    sb.Append($"\"{attribName}\"");
                }
            }

            string preSql = "";
            if (isSql)
            {
                sb.Append(") VALUES (");

                preSql = sb.ToString();
                sb.Clear();
            }
            else
            {
                sb.AppendLine();
            }

            // Data
            for (int i = 0; i < repeatCount; i++)
            {
                var cache = new Cache();

                if (isSql)
                {
                    sb.Append(preSql);
                }

                first = true;

                foreach (var attribute in attributes)
                {
                    if (first)
                    {
                        first = false;
                    }
                    else
                    {
                        sb.Append(",");
                    }

                    this.resolver.Resolve(attribute, cache, this);
                    sb.Append(attribute.GetValue(isSql));
                }

                if (isSql)
                {
                    sb.AppendLine(");");
                }
                else
                {
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        /// <summary>
        /// Adds the (not yet resolved) attributes of the specified entity to the list.
        /// Throws an error if any attribute has children (must be a flat structure).
        /// 
        /// Returns true if the entity is a repeating entity.
        /// </summary>
        private bool AddAttributes(List<ResolvedValue> attributes, string parentName, Entity entity, string entityName = "")
        {
            string fullName;
            if (parentName != null)
            {
                fullName = parentName + "." + entity.Name;
            }
            else
            {
                fullName = entity.Name;
            }

            if (entityName.Length == 0)
            {
                entityName = entity.Name;
            }

            if (entity.Type == Entity.Types.PARENT || entity.Type == Entity.Types.ARRAY || entity.Type == Entity.Types.REPEAT)
            {
                if (attributes.Count == 0 && entity.Type == Entity.Types.PARENT)
                {
                    foreach (var childName in entity.ChildOrder)
                    {
                        Entity child = entity.ChildEntities[childName];
                        AddAttributes(attributes, fullName, child);
                    }
                }
                else if (attributes.Count == 0 && entity.Type == Entity.Types.REPEAT)
                {
                    return true;
                }
                else
                {
                    throw new Exception($"Cannot output attribute '{entity.Name}' in SQL or CSV format as it has children. " +
                        "Only flat structures can be output in these formats.");
                }
            }
            else if (entity.Type == Entity.Types.REF)
            {
                Entity refEntity;
                try
                {
                    refEntity = this.resolver.FindEntity(entity.Value);
                }
                catch (Exception)
                {
                    throw new Exception($"Attribute '{entityName}' references unknown entity '{entity.Value}'");
                }

                AddAttributes(attributes, null, refEntity, entityName);
            }
            else
            {
                var attribute = new ResolvedValue(fullName, entity.Type, entity.Value);
                attributes.Add(attribute);
            }

            return false;
        }
    }
}
