using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// Stores a persisted entity as a string (JSON object)
    /// and also stores the values of the top-level attributes
    /// used for the persistence filename.
    /// </summary>
    public class PersistEntity
    {
        public string Json { get; set; }
        public Dictionary<string, string> PersistValues { get; set; } = new Dictionary<string, string>();

        /// <summary>
        /// Takes a fileDef containing variable names and
        /// returns the actual filename by replacing all
        /// variables with their actual values, e.g.
        /// myFile_{id} becomes myFile_1234.
        /// 
        /// Throws an exception if the fileDef contains
        /// any variables with no matching value.
        /// </summary>
        public string GetFilename(string fileDef)
        {
            foreach (var entry in PersistValues)
            {
                fileDef = fileDef.Replace("{" + entry.Key + "}", entry.Value);
            }

            int startVar = fileDef.IndexOf('{');
            if (startVar != -1)
            {
                startVar++;
                int endVar = fileDef.IndexOf('}', startVar);
                if (endVar != -1)
                {
                    throw new Exception($"Cannot write output file '{fileDef}' as attribute " +
                            $"'{fileDef.Substring(startVar, endVar - startVar)}' was not found in the object.");
                }
            }

            return fileDef;
        }
    }
}
