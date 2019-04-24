using reqit.CmdLine;
using reqit.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.RepresentationModel;

namespace reqit.Parsers
{
    public class YamlParser : IYamlParser
    {
        public static string DEFAULT = Path.Combine(MyMain.GetWorkingDir(), "reqit.yaml");
        public static string SAMPLE = Path.Combine(MyMain.GetWorkingDir(), "sample.yaml");

        public ApiService ApiService { get; private set; }
        private YamlMappingNode rootNode;

        public ApiService LoadFile(string yamlFile)
        {
            return Load(File.ReadAllText(yamlFile));
        }

        public ApiService Load(string yamlString)
        {
            ApiService = new ApiService();

            using (var stream = new StringReader(yamlString))
            {
                var yaml = new YamlStream();
                yaml.Load(stream);

                if (yaml.Documents.Count == 0)
                {
                    // File is empty
                    return ApiService;
                }

                this.rootNode = (YamlMappingNode)yaml.Documents[0].RootNode;
            }

            ParseEntities();
            ParseAliases();
            ParseApis();

            return ApiService;
        }

        private void ParseEntities()
        {
            YamlMappingNode entitiesNode;
            try
            {
                entitiesNode = (YamlMappingNode)this.rootNode.Children[new YamlScalarNode("entity")];
            }
            catch (KeyNotFoundException)
            {
                // Not an error - entity node is optional
                return;
            }

            foreach (var entityNode in entitiesNode)
            {
                var entity = parseEntity(ApiService.EntityRoot.Name, entityNode.Key.ToString(), entityNode.Value);
                ApiService.EntityRoot.AddChild(ApiService.EntityRoot.Name, entity);
            }
        }

        private Entity parseEntity(string parentName, string name, YamlNode node)
        {
            string fullName;
            if (parentName != null)
            {
                fullName = parentName + "." + name;
            }
            else
            {
                fullName = name;
            }

            if (name.Contains(" "))
            {
                throw new Exception($"'{fullName}' must not contain spaces");
            }

            if (name.Contains("."))
            {
                throw new Exception($"{parentName} attribute '{name}' must not contain a dot");
            }

            Entity entity = null;

            if (node.NodeType == YamlNodeType.Mapping)
            {
                entity = new Entity(name, Entity.Types.PARENT);
                foreach (var childNode in (YamlMappingNode)node)
                {
                    var childEntity = parseEntity(fullName, childNode.Key.ToString(), childNode.Value);
                    entity.AddChild(fullName, childEntity);
                }
            }
            else if (node.NodeType == YamlNodeType.Sequence)
            {
                var sequenceNode = (YamlSequenceNode)node;
                entity = new Entity(name, Entity.Types.ARRAY);
                int id = 0;
                foreach (var childNode in sequenceNode.Children)
                {
                    var childEntity = parseEntity(fullName, "~" + (++id), childNode);
                    entity.AddChild(fullName, childEntity);
                }
            }
            else if (node.NodeType == YamlNodeType.Scalar)
            {
                var scalarNode = (YamlScalarNode)node;
                int sep = scalarNode.Value.IndexOf(',');
                string entityType = null;
                string entityValue = null;
                if (sep > 0 && sep < scalarNode.Value.Length)
                {
                    entityType = scalarNode.Value.Substring(0, sep).Trim();
                    entityValue = scalarNode.Value.Substring(sep+1).Trim();
                }

                // Empty value is valid
                if (String.IsNullOrEmpty(entityType) || entityValue == null)
                {
                    throw new Exception($"{fullName} must be in format TYPE,VALUE. For a null value use format 'TYPE,{Entity.NULL}' and for an empty string use format 'STR,'.");
                }

                Entity.Types type;
                try
                {
                    type = (Entity.Types)Enum.Parse(typeof(Entity.Types), entityType, true);
                }
                catch (ArgumentException)
                {
                    throw new Exception($"{fullName} has unknown type: {entityType}");
                }

                entity = new Entity(name, type, entityValue);
            }
            else
            {
                throw new Exception($"{fullName} has unknown YAML node type: {node.NodeType.ToString()}");
            }

            return entity;
        }

        /// <summary>
        /// An alias is just a shorthand way of writing a REF.
        /// The following are equivalent:
        ///   my_entity: REF, emp_id
        ///   my_alias: emp_id
        /// </summary>
        private void ParseAliases()
        {
            YamlMappingNode aliasesNode;
            try
            {
                aliasesNode = (YamlMappingNode)this.rootNode.Children[new YamlScalarNode("alias")];
            }
            catch (KeyNotFoundException)
            {
                // Not an error - alias node is optional
                return;
            }

            foreach (var aliasNode in (YamlMappingNode)aliasesNode)
            {
                string name = aliasNode.Key.ToString();
                YamlNode node = aliasNode.Value;

                if (node.NodeType == YamlNodeType.Sequence)
                {
                    throw new Exception($"Alias '{name}' should have quotes around an array, e.g. NAME: \"[VALUE, 1]\"");
                }
                else if (node.NodeType != YamlNodeType.Scalar)
                {
                    throw new Exception($"Alias '{name}' should be in format NAME: VALUE");
                }

                var scalarNode = (YamlScalarNode)node;
                string alias = scalarNode.Value.Trim();

                var entity = new Entity(name, Entity.Types.REF, alias);
                ApiService.EntityRoot.AddChild(ApiService.EntityRoot.Name, entity);
            }
        }

        private void ParseApis()
        {
            YamlSequenceNode apisNode;
            try
            {
                apisNode = (YamlSequenceNode)this.rootNode.Children[new YamlScalarNode("api")];
            }
            catch (KeyNotFoundException)
            {
                // Not an error - may be adding an api section later!
                return;
            }

            int apiNum = 0;
            foreach (YamlMappingNode apiNode in apisNode)
            {
                apiNum++;

                // Mandatory attributes
                string methodStr;
                string pathStr;
                try
                {
                    methodStr = apiNode.Children[new YamlScalarNode("method")].ToString();
                    pathStr = apiNode.Children[new YamlScalarNode("path")].ToString();
                }
                catch (KeyNotFoundException)
                {
                    throw new KeyNotFoundException($"api.~{apiNum} must have method and path attributes");
                }

                if (pathStr.Contains('?'))
                {
                    throw new Exception($"api.~{apiNum} should not have a pre-defined query string as query params are optional: {pathStr}");
                }

                var path = new Route(apiNode.Children[new YamlScalarNode("path")].ToString());

                Api.Methods method;
                try
                {
                    method = (Api.Methods)Enum.Parse(typeof(Api.Methods), methodStr, true);
                }
                catch (ArgumentException)
                {
                    throw new Exception($"api.~{apiNum} has unknown method: {methodStr}");
                }

                var api = new Api(method, path);

                try
                {
                    string requestStr = apiNode.Children[new YamlScalarNode("request")].ToString();
                    try
                    {
                        api.Request = new ApiBody(requestStr);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"api.~{apiNum} has bad request '{requestStr}': {e.Message}");
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Not an error - request node is optional
                }

                try
                {
                    string responseStr = apiNode.Children[new YamlScalarNode("response")].ToString();
                    try
                    {
                        api.Response = new ApiBody(responseStr);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"api.~{apiNum} has bad response '{responseStr}': {e.Message}");
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Not an error - response node is optional
                }

                try
                {
                    string persistStr = apiNode.Children[new YamlScalarNode("persist")].ToString();
                    try
                    {
                        api.Persist = new Persistence(persistStr);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"api.~{apiNum} has bad persist '{persistStr}': {e.Message}");
                    }
                }
                catch (KeyNotFoundException)
                {
                    // Not an error - persist node is optional
                }

                // Make sure api does not already exist
                foreach (var existingApi in ApiService.Apis)
                {
                    if (api.Method == existingApi.Method && api.Path.Equals(existingApi.Path, out var pathEntities))
                    {
                        throw new Exception($"api.~{apiNum} is a duplicate");
                    }
                }

                ApiService.Apis.Add(api);
            }
        }
    }
}
