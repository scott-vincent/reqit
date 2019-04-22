using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using reqit.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace reqit.Parsers
{
    public class JsonParser : IJsonParser
    {
        public Entity LoadEntityFromFile(string name, string jsonFile)
        {
            return LoadEntity(name, File.ReadAllText(jsonFile));
        }

        public Entity LoadEntity(string name, string jsonString)
        {
            Entity entity = new Entity(name, Entity.Types.PARENT);

            // Read entire string into JObject hierarchy
            JObject rootObj = JObject.Parse(jsonString);

            // Recurse root object and add to entity
            addObject(entity, name, rootObj);

            return entity;
        }

        private void addObject(Entity entity, string fullName, JObject obj)
        {
            foreach (var child in obj.Children())
            {
                JProperty prop = child as JProperty;
                addToken(entity, fullName, prop.Name, prop.Value);
            }
        }

        private void addToken(Entity entity, string parentName, string name, JToken token)
        {
            string fullName = parentName + "." + name;

            if (token.Type == JTokenType.Object)
            {
                Entity newEntity = new Entity(name, Entity.Types.PARENT);
                entity.AddChild(parentName, newEntity);
                addObject(newEntity, fullName, token as JObject);
            }
            else if (token.Type == JTokenType.Array)
            {
                Entity newEntity = new Entity(name, Entity.Types.ARRAY);
                entity.AddChild(parentName, newEntity);

                JArray array = token as JArray;
                int id = 0;
                foreach (var entry in array.Children())
                {
                    addToken(newEntity, parentName, "~" + (++id), entry);
                }
            }
            else if (token.Type != JTokenType.Comment)
            {
                Entity.Types type;
                string value = token.ToString();

                if (token.Type == JTokenType.Integer || token.Type == JTokenType.Float)
                {
                    type = Entity.Types.NUM;
                }
                else if (token.Type == JTokenType.Date)
                {
                    type = Entity.Types.DATE;
                }
                else if (token.Type == JTokenType.Boolean)
                {
                    type = Entity.Types.BOOL;
                    value = value.ToLower();
                }
                else if (token.Type == JTokenType.Null)
                {
                    type = Entity.Types.STR;
                    value = Entity.NULL;
                }
                else
                {
                    type = Entity.Types.STR;
                }

                Entity newEntity = new Entity(name, type, value);
                entity.AddChild(parentName, newEntity);
            }
        }

        /// <summary>
        /// Enumerate the top-level entities in a JSON array.
        /// </summary>
        public IEnumerable<PersistEntity> ArrayEntities(string jsonArray, Persistence persist)
        {
            JArray entities = JArray.Parse(jsonArray);

            foreach (var child in entities.Children())
            {
                var entity = new PersistEntity();
                entity.Json = LessPretty(child.ToString());

                // Read top-level attributes to find variable values
                foreach (var attrib in child.Children())
                {
                    JProperty prop = attrib as JProperty;
                    if (persist.OutputVars.Contains(prop.Name))
                    {
                        string value = prop.Value.ToString(Formatting.None);
                        if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
                        {
                            entity.PersistValues.Add(prop.Name, value.Substring(1, value.Length - 2));
                        }
                        else
                        {
                            entity.PersistValues.Add(prop.Name, value);
                        }
                    }
                }

                yield return entity;
            }
        }

        /// <summary>
        /// Like the ArrayEntities method but for a single object
        /// rather than an array of objects.
        /// 
        /// </summary>
        public PersistEntity ObjectEntity(string jsonObject, Persistence persist)
        {
            JObject obj = JObject.Parse(jsonObject);

            var entity = new PersistEntity();
            entity.Json = LessPretty(obj.ToString());

            // Read top-level attributes to find variable values
            foreach (var attrib in obj.Children())
            {
                JProperty prop = attrib as JProperty;
                if (persist.OutputVars.Contains(prop.Name))
                {
                    string value = prop.Value.ToString(Formatting.None);
                    if (value.Length > 1 && value[0] == '"' && value[value.Length - 1] == '"')
                    {
                        entity.PersistValues.Add(prop.Name, value.Substring(1, value.Length - 2));
                    }
                    else
                    {
                        entity.PersistValues.Add(prop.Name, value);
                    }
                }
            }

            return entity;
        }

        /// <summary>
        /// Takes JSON formatted by the JSON library and removes
        /// newlines and extra indentation.
        /// 
        /// Cannot just take unformatted JSON from the library as
        /// it removes ALL spaces including ones between attribute
        /// name and value which makes it too hard to read.
        /// </summary>
        private string LessPretty(string prettyJson)
        {
            var lines = prettyJson.Split("\r\n");

            var json = new StringBuilder();

            foreach (var line in lines)
            {
                string entry = line.Trim();
                json.Append(entry);
                if (entry.EndsWith(','))
                {
                    json.Append(' ');
                }
            }

            return json.ToString();
        }
    }
}
