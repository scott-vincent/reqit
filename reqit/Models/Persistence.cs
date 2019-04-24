using reqit.CmdLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// Stores a persistence filename in its original defined form,
    /// e.g. myFolder/myEntity_{id}_{name} and also extracts and
    /// stores the variables separately, e.g. id and name.
    /// 
    /// If the original form contains no variables then OutputVars
    /// will be null.
    /// 
    /// Throws an exception if the definition is badly formed,
    /// e.g. mismatched braces or it contains invalid characters.
    /// </summary>
    public class Persistence
    {
        public static string PERSIST_DIR = Path.Combine(MyMain.GetWorkingDir(), "persist");

        public string OutputDef { get; }
        public HashSet<string> OutputVars { get; }
        public string Folder { get; }
        public string Pattern { get; }          // Last part of outputDef
        public string WildPattern { get; }      // Last part of outputDef with vars replaced by *

        public Persistence(string outputDef)
        {
            OutputDef = outputDef;
            OutputVars = ExtractVars(outputDef);

            // Make sure folder is valid and exists
            try
            {
                Folder = Path.Combine(PERSIST_DIR, Path.GetDirectoryName(OutputDef));
                if (!Directory.Exists(Folder))
                {
                    Directory.CreateDirectory(Folder);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to create folder '{Folder}': {e.Message}");
            }

            Pattern = Path.GetFileName(OutputDef);

            // Replace each var with a wildcard
            var sb = new StringBuilder();
            int pos = 0;
            int varStart = Pattern.IndexOf('{');
            while (varStart != -1)
            {
                int varEnd = Pattern.IndexOf('}', varStart);
                if (varEnd == -1)
                {
                    throw new Exception("Mismatched braces. Missing '}'.");
                }

                if (varEnd == varStart + 1)
                {
                    throw new Exception("Missing variable name (empty braces).");
                }

                // Make sure variable does not contain an open brace
                int nextVar = Pattern.IndexOf('{', varStart + 1);
                if (nextVar != -1 && nextVar < varEnd)
                {
                    throw new Exception("Mismatched braces. Missing '}'.");
                }

                sb.Append(Pattern.Substring(pos, varStart - pos));
                sb.Append("*");

                pos = varEnd + 1;
                if (pos < Pattern.Length)
                {
                    varStart = Pattern.IndexOf('{', pos);
                }
                else
                {
                    break;
                }
            }

            if (pos < Pattern.Length)
            {
                sb.Append(Pattern.Substring(pos));
            }

            WildPattern = sb.ToString();

            // Make sure there is at least one wildcard
            int wildPos = WildPattern.IndexOf('*');
            if (wildPos == -1)
            {
                throw new Exception("Persist definition must contain at least one variable '{xxx}' or wildcard '*'");
            }
        }

        public string InsertVars(Cache vars, ApiBody request, ApiBody response)
        {
            // Make sure folder is valid and exists
            try
            {
                if (!Directory.Exists(Folder))
                {
                    Directory.CreateDirectory(Folder);
                }
            }
            catch (Exception e)
            {
                throw new Exception($"Failed to create folder '{Folder}': {e.Message}");
            }

            var sb = new StringBuilder();
            int pos = 0;
            int varStart = Pattern.IndexOf('{');
            while (varStart != -1)
            {
                int varEnd = Pattern.IndexOf('}', varStart);
                if (varEnd == -1)
                {
                    throw new Exception($"Persist definition {OutputDef} has mismatched braces. Missing '}}'.");
                }

                sb.Append(Pattern.Substring(pos, varStart - pos));

                string name = Pattern.Substring(varStart + 1, varEnd - (varStart + 1));
                ResolvedValue resolved = null;
                try
                {
                    if (name.StartsWith("path.") || name.StartsWith("query.") || name.StartsWith("request."))
                    {
                        resolved = vars.GetValue(name);
                    }
                    else
                    {
                        if (response != null)
                        {
                            resolved = vars.GetValueOrNull(response.EntityName + "." + name);
                        }

                        if (resolved == null)
                        {
                            if (request == null)
                            {
                                resolved = vars.GetValue(name);
                            }
                            else
                            {
                                resolved = vars.GetValue("request." + request.EntityName + "." + name);
                            }
                        }
                    }
                }
                catch (Exception)
                {
                    if (response != null && response.Mods != null)
                    {
                        // Variable might be in response mods.
                        string newName = response.Mods.GetValueOrDefault(response.EntityName + "." + name);

                        if (newName != null)
                        {
                            if (newName[0] == '~')
                            {
                                // Mod is a reference
                                try
                                {
                                    resolved = vars.GetValue(newName.Substring(1));
                                }
                                catch (Exception)
                                {
                                    // Do nothing - will be caught by null check
                                }
                            }
                            else
                            {
                                // Mod is a literal
                                resolved = new ResolvedValue(name, Entity.Types.STR, newName);
                                resolved.SetValue(newName);
                            }
                        }
                    }
                }

                if (resolved == null)
                {
                    throw new Exception($"Persist definition '{OutputDef}' variable '{name}' does not match any known attribute.");
                }

                sb.Append(resolved.Value);

                pos = varEnd + 1;
                if (pos < Pattern.Length)
                {
                    varStart = WildPattern.IndexOf('{', pos);
                }
                else
                {
                    break;
                }
            }

            if (pos < Pattern.Length)
            {
                sb.Append(Pattern.Substring(pos));
            }

            return Path.Combine(Folder, sb.ToString());
        }

        private HashSet<string> ExtractVars(string outputDef)
        {
            var extractedVars = new HashSet<string>();
            int i = 0;
            while (i < outputDef.Length)
            {
                if (outputDef[i] == '{')
                {
                    int startPos = i + 1;
                    while (outputDef[i] != '}')
                    {
                        if (outputDef[i] == ' ' || outputDef[i] == '/' || outputDef[i] == '\\')
                        {
                            throw new Exception("Variable name must not include space, slash or backslash.");
                        }

                        i++;
                        if (i == outputDef.Length)
                        {
                            throw new Exception("Contains mismatched braces. Found '{' but no '}'.");
                        }
                    }

                    if (i - startPos == 0)
                    {
                        throw new Exception("variable name must not be empty.");
                    }

                    extractedVars.Add(outputDef.Substring(startPos, i - startPos));
                }
                else if (outputDef[i] == '}')
                {
                    throw new Exception("Contains mismatched braces. Found '}' but no '{'.");
                }

                i++;
            }

            if (extractedVars.Count == 0)
            {
                return null;
            }

            return extractedVars;
        }
    }
}
