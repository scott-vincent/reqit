using reqit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace reqit.Engine
{
    /// <summary>
    /// This model indexes all the entities in the hierarchy
    /// for fast searching. It also resolves references and
    /// converts array entities (where name is enclosed in
    /// square brackets) into Repeat entities.
    /// </summary>
    public class EntityIndex
    {
        private Dictionary<string, Entity> index;
        private Random random = new Random();

        public EntityIndex()
        {
            this.index = new Dictionary<string, Entity>();
        }

        /// <summary>
        /// Add an entity (and recursively add child entities) to the index
        /// </summary>
        public void Add(string parent, Entity entity)
        {
            string name;
            if (parent == null)
            {
                name = entity.Name;
            }
            else
            {
                name = parent + "." + entity.Name;
            }

            // Don't store root entity
            if (name != null)
            {
                this.index.Add(name, entity);
            }

            if (entity.Type == Entity.Types.PARENT || entity.Type == Entity.Types.ARRAY)
            {
                foreach (var child in entity.ChildEntities)
                {
                    Add(name, child.Value);
                }
            }
        }

        /// <summary>
        /// Finds an entity, following ref links where necessary.
        /// Throws an exception if the entity or ref link is not found
        /// or nesting is too deep (circular reference).
        /// </summary>
        public Entity Find(string fullName)
        {
            return Find(fullName, 0);
        }

        private Entity Find(string fullName, int nestedLevel)
        {
            if (nestedLevel > 50)
            {
                throw new Exception($"Cannot resolve entity '{fullName}' as it contains a circular reference");
            }

            Entity entity;

            if (fullName.StartsWith('"'))
            {
                throw new Exception($"Entity '{fullName}' must not be enclosed in quotes");
            }

            if (fullName.StartsWith('['))
            {
                if (!fullName.EndsWith(']'))
                {
                    throw new Exception($"Repeating entity '{fullName}' must be enclosed in square brackets");
                }

                var args = fullName.Substring(1, fullName.Length - 2).Split(',');

                // Repeat count is optional (might be using persist)
                if (args.Length == 1)
                {
                    args = new string[2] { args[0], "0" };
                }

                if (args.Length != 2)
                {
                    throw new Exception($"Repeating entity '{fullName}' format must be [name, count] or [name, min-max]");
                }

                return GenerateRepeating(fullName, args[0].Trim(), args[1].Trim(), nestedLevel);
            }

            if (this.index.TryGetValue(fullName, out entity))
            {
                // A reference points to another entity so we need to
                // do another search to find the real entity.
                if (entity.Type == Entity.Types.REF)
                {
                    return Find(entity.Value, ++nestedLevel);
                }

                return entity;
            }

            throw new Exception($"{fullName}: Not found");
        }

        /// <summary>
        /// Generates a repeating entity. Repeat can be a single
        /// number or a range (min-max). For a range the number of
        /// entries is chosen at random from min to max.
        /// </summary>
        private Entity GenerateRepeating(string parent, string entityName, string repeatStr, int nestedLevel)
        {
            Entity refEntity;
            try
            {
                refEntity = Find(entityName, ++nestedLevel);
            }
            catch (Exception e)
            {
                throw new Exception($"Repeating entity '{parent}' error - {e.Message}");
            }

            int min;
            int max;
            var range = repeatStr.Split('-');
            if (range.Length > 1)
            {
                try
                {
                    min = int.Parse(range[0].Trim());
                    max = int.Parse(range[1].Trim());
                }
                catch (Exception)
                {
                    return null;
                }

                if (min > max)
                {
                    int temp = min;
                    min = max;
                    max = temp;
                }
            }
            else
            {
                try
                {
                    min = int.Parse(repeatStr);
                    max = min;
                }
                catch (Exception)
                {
                    return null;
                }
            }

            return new Entity(entityName, min, max);
        }
    }
}
