using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This cache is used by the resolver to provide a consistent
    /// view with consistent genders across an entity.
    /// 
    /// The resolver may use multiple caches, e.g. if it resolving
    /// an array of entities (where the entities are independent
    /// of each other).
    /// 
    /// The cache also ensures there are no circular dependencies
    /// by storing the set of values currently being resolved. If
    /// a value is already in the unresolved set and the resolver
    /// tries to resolve it again there is a circular dependency.
    /// </summary>
    public class Cache
    {
        private HashSet<string> unresolved;
        public Dictionary<string, ResolvedValue> Resolved { get; private set; }

        public Cache()
        {
            unresolved = new HashSet<string>();
            Resolved = new Dictionary<string, ResolvedValue>();
        }

        /// <summary>
        /// FullName may start with "request.", "response." or "path.".
        /// 
        /// Returns the value if it has been resolved. If it hasn't been resolved
        /// returns null and adds it to the unresolved set.
        /// 
        /// Throws an exception if there is a circular dependency (already exists
        /// in the unresolved set).
        /// </summary>
        public ResolvedValue GetResolved(string fullName)
        {
            // Special case: fullName is null (or ends with '.NOCACHE') for values that shouldn't be cached
            if (fullName == null || fullName.EndsWith(".NOCACHE"))
            {
                return null;
            }

            var value = Resolved.GetValueOrDefault(fullName);
            if (value == null)
            {
                if (!unresolved.Add(fullName))
                {
                    throw new Exception($"Cannot resolve '{fullName}' as it has a circular dependency");
                }
            }

            return value;
        }

        /// <summary>
        /// Adds the value to the resolved set and removes it from the unresolved set.
        /// </summary>
        public void SetResolved(ResolvedValue value)
        {
            // Special case: name is null (or ends with '.NOCACHE') for values that shouldn't be cached
            if (value.Name == null || value.Name.EndsWith(".NOCACHE"))
            {
                return;
            }

            Resolved.Add(value.Name, value);
            unresolved.Remove(value.Name);
        }

        /// <summary>
        /// Adds an already resolved value to the cache
        /// </summary>
        public void AddResolved(string name, string strValue)
        {
            var value = new ResolvedValue(name, Entity.Types.STR, strValue);
            value.SetValue(strValue);
            Resolved.Add(name, value);
        }

        public ResolvedValue GetValue(string name)
        {
            if (Resolved.TryGetValue(name, out var value))
            {
                return value;
            }
            else
            {
                throw new Exception($"Attribute '{name}' not found");
            }
        }

        public ResolvedValue GetValueOrNull(string name)
        {
            return Resolved.GetValueOrDefault(name);
        }
        
        public bool HasValue(string name)
        {
            return Resolved.ContainsKey(name);
        }
    }
}
