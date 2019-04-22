using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This model defines a single API resource path.
    /// It may contain variables, e.g. /api/employees/{id}.
    /// </summary>
    public class Route
    {
        private string Path { get; }
        private string[] Parts { get; }
        private bool[] IsVar { get; }

        public Route(string path)
        {
            Path = path;

            /// <summary>
            /// Store path broken down into parts for quick matching.
            /// If a part is enclosed in curly braces then it is a
            /// variable and will always match.
            ///
            /// e.g. /api/employees/{id} matches /api/employees/5
            /// </summary>
            Parts = path.Split('/');
            IsVar = new bool[Parts.Length];

            for (int i = 0; i < Parts.Length; i++)
            {
                if (Parts[i].StartsWith('{') && Parts[i].EndsWith('}'))
                {
                    IsVar[i] = true;
                    Parts[i] = Parts[i].Substring(1, Parts[i].Length - 2);
                }
                else
                {
                    IsVar[i] = false;
                }
            }
        }

        /// <summary>
        /// Returns the variable parts of the path
        /// e.g. if path = first/{var1}/second/{var2}
        /// returns ["var1", "var2"]
        /// </summary>
        public List<string> GetVars()
        {
            var vars = new List<string>();

            for (int i = 0; i < Parts.Length; i++)
            {
                if (IsVar[i])
                {
                    vars.Add(Parts[i]);
                }
            }

            return vars;
        }

        /// <summary>
        /// When comparing the paths, any variables found will be
        /// returned in the cache.
        /// </summary>
        public bool Equals(Route route, out Cache cache)
        {
            // Must have same number of parts to match
            if (Parts.Length != route.Parts.Length)
            {
                cache = null;
                return false;
            }

            // All parts must match
            cache = new Cache();
            for (int i = 0; i < Parts.Length; i++)
            {
                // If either part is variable then they match
                if (IsVar[i])
                {
                    var resolved = new ResolvedValue("path." + Parts[i], Entity.Types.STR, route.Parts[i]);
                    resolved.SetValue(route.Parts[i]);
                    cache.SetResolved(resolved);
                }
                else if (route.IsVar[i])
                {
                    cache.AddResolved("path." + route.Parts[i], Parts[i]);
                }
                else if (!Parts[i].Equals(route.Parts[i]))
                {
                    cache = null;
                    return false;
                }
            }

            return true;
        }

        public override string ToString()
        {
            return Path;
        }
    }
}
