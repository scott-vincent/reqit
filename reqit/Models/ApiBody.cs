using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This model defines an API request or response.
    /// It includes the entity definition, single or repeating,
    /// and the modifications that should be applied to the
    /// entity, e.g.
    /// 
    ///   [employee, 0-7], id=~path.id
    ///   
    /// </summary>
    public class ApiBody
    {
        public string EntityDef { get; }
        public string EntityName { get; }
        public Dictionary<string, string> Mods { get; }

        /// <summary>
        /// Takes a body definition, e.g.:
        ///   myentity, attrib1=path.id, !attrib2, ...
        /// and stores its constituent parts.
        /// </summary>
        public ApiBody(string bodyDef)
        {
            bodyDef = bodyDef.Trim();

            int modStart;
            if (bodyDef.Length > 0 && bodyDef[0] == '[')
            {
                modStart = bodyDef.IndexOf(']');
                if (modStart == -1)
                {
                    throw new Exception("Missing closing bracket ']'");
                }

                modStart++;
                EntityDef = bodyDef.Substring(0, modStart).Trim();

                string[] parts = EntityDef.Split(',');
                if (parts.Length > 0)
                {
                    EntityName = parts[0].Trim();

                    // Repeat count is optional (might be using persist)
                    if (parts.Length == 1)
                    {
                        parts = new string[2] { parts[0], "0" };
                    }
                }

                if (parts.Length != 2 || EntityName.Length == 0 || parts[1].Trim().Length == 0)
                {
                    throw new Exception("Repeating entity format must be [name, count] or [name, min-max]");
                }
            }
            else
            {
                modStart = bodyDef.IndexOf(',');
                if (modStart == -1)
                {
                    EntityName = bodyDef.Trim();
                }
                else
                {
                    EntityName = bodyDef.Substring(0, modStart).Trim();
                }

                if (EntityName == null || EntityName.Length == 0)
                {
                    throw new Exception("Missing entity name");
                }

                EntityDef = EntityName;
                modStart++;
            }

            string[] mods = bodyDef.Substring(modStart).Split(',');
            Mods = new Dictionary<string, string>();

            for (int i = 0; i < mods.Length; i++)
            {
                string mod = mods[i].Trim();

                if (mod.Length > 0)
                {
                    // Mod format is either !attrib or attrib=val
                    if (mod[0] == '!')
                    {
                        Mods.Add(EntityName + "." + mod.Substring(1), null);
                    }
                    else
                    {
                        var modParts = mod.Split('=');
                        string modAttrib = modParts[0].Trim();
                        string modValue = "";
                        if (modParts.Length == 2)
                        {
                            modValue = modParts[1].Trim();
                        }

                        // Ignore badly formed mods
                        if (modAttrib.Equals("*"))
                        {
                            Mods.Add(modAttrib, modValue);
                        }
                        else if (modAttrib.Length > 0 && modValue.Length > 0)
                        {
                            Mods.Add(EntityName + "." + modAttrib, modValue);
                        }
                    }
                }
            }
        }
    }
}
