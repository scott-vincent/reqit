using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// Recursive class to store entity hierarchy.
    /// An entity will either have child entities (if type = Parent,
    /// Array or Repeat) or a value (all other types).
    ///
    /// .NET Core does not have the OrderedDictionary class so we have to
    /// store both a Dictionary (for fast lookup) and a list (to preserve
    /// order).
    ///
    /// Note that a STR entity may reference another entity (entity name
    /// will be enclosed in curly braces).
    /// </summary>
    public class Entity
    {
        public static string NULL = "<null>";
        public static string SAMPLE = "~sample";

        public enum Types { PARENT, ARRAY, REPEAT, REF, STR, NUM, BOOL, DATE, OBJ };

        public string Name { get; }
        public Types Type { get; }
        public Dictionary<string, Entity> ChildEntities { get; }
        public List<string> ChildOrder { get; }
        public string Value { get; }

        // Only used for Repeat entity
        public int Min { get; }
        public int Max { get; }

        /// <summary>
        /// Create a new Parent or Array entity
        /// </summary>
        public Entity(string name, Types type)
        {
            Name = name;
            Type = type;

            if (type != Types.PARENT && type != Types.ARRAY)
            {
                throw new Exception("No value specified for new entity");
            }

            ChildEntities = new Dictionary<string, Entity>();
            ChildOrder = new List<string>();
        }

        /// <summary>
        /// Create a new bottom-level entity
        /// </summary>
        public Entity(string name, Types type, string value)
        {
            Name = name;
            Type = type;

            if (type == Types.PARENT || type == Types.ARRAY)
            {
                throw new Exception("Parent/Array entity should not have a value");
            }

            Value = value;
        }

        /// <summary>
        /// Create a new Repeating entity
        /// </summary>
        public Entity(string fullName, int min, int max)
        {
            int pos = fullName.LastIndexOf('.');
            if (pos == -1)
            {
                Name = "~" + fullName;
            }
            else
            {
                Name = "~" + fullName.Substring(pos+1);
            }

            Type = Types.REPEAT;
            Value = fullName;   // Don't store actual entity as it may get deleted before we evaluate it
            Min = min;
            Max = max;
        }

        public void AddChild(string parentName, Entity entity)
        {
            if (Type != Types.PARENT && Type != Types.ARRAY && Type != Types.REPEAT)
            {
                throw new Exception($"{parentName} must have type Parent, Array or Repeat to add child entities");
            }

            // Make sure name does not already exist
            if (ChildEntities.ContainsKey(entity.Name))
            {
                throw new Exception($"{parentName} defines attribute {entity.Name} multiple times");
            }

            ChildEntities.Add(entity.Name, entity);
            ChildOrder.Add(entity.Name);
        }

        public override string ToString()
        {
            return PrettyPrint(this, "");
        }

        public static string PrettyPrint(Entity entity, string indent)
        {
            StringBuilder sb = new StringBuilder();
            bool isArrayChild = entity.Name.StartsWith("~");

            if (entity.Type == Entity.Types.PARENT | entity.Type == Entity.Types.ARRAY)
            {
                // Don't show generated names of array children
                if (!isArrayChild)
                {
                    sb.AppendLine($"{indent}{entity.Name}:");
                    indent = indent.Replace("-", " ");
                }

                foreach (string entityName in entity.ChildOrder)
                {
                    Entity child = entity.ChildEntities[entityName];
                    if (isArrayChild)
                    {
                        sb.Append(PrettyPrint(child, indent + "- "));
                        isArrayChild = false;
                    }
                    else
                    {
                        sb.Append(PrettyPrint(child, indent + "  "));
                    }
                }
            }
            else
            {
                // Don't show generated names of array children
                if (isArrayChild)
                {
                    sb.AppendLine($"{indent}- {entity.Type.ToString()}, {entity.Value}");
                }
                else
                {
                    sb.AppendLine($"{indent}{entity.Name}: {entity.Type.ToString()}, {entity.Value}");
                }
            }

            return sb.ToString();
        }
    }
}
