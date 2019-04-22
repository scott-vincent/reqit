using reqit.Models;
using reqit.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace reqit.Engine
{
    public class Resolver : IResolver
    {
        private Dictionary<string, Samples> loadedSamples;
        private Random random = new Random();

        public ApiService ApiService { get; private set; }
        private EntityIndex loadedEntities;

        private readonly ISamplesParser samplesParser;
        private readonly IFuncs funcs;

        public Resolver(ISamplesParser samplesParser, IFuncs funcs)
        {
            this.samplesParser = samplesParser;
            this.funcs = funcs;
        }

        /// <summary>
        /// The resolver should be initialised after loading the YAML file
        /// </summary>
        public void Init(ApiService apiService)
        {
            this.ApiService = apiService;
            this.loadedSamples = new Dictionary<string, Samples>();

            // Index all the loaded entities
            this.loadedEntities = new EntityIndex();
            this.loadedEntities.Add(null, this.ApiService.EntityRoot);
        }

        public Api MatchRoute(Api.Methods method, string path, out Cache cache)
        {
            if (this.ApiService == null)
            {
                throw new Exception("Resolver has not been initialised");
            }

            Route route = new Route(path);

            foreach (var api in this.ApiService.Apis)
            {
                if (method == api.Method && route.Equals(api.Path, out cache))
                {
                    return api;
                }
            }

            cache = null;
            return null;
        }

        public Entity FindEntity(string name)
        {
            return this.loadedEntities.Find(name);
        }

        /// <summary>
        /// Returns the list of valid sample file names
        /// </summary>
        public List<string> GetSampleNames()
        {
            return this.samplesParser.GetSampleNames();
        }

        /// <summary>
        /// Load required samples if not already loaded.
        /// Throws an exception if samples file not found.
        /// </summary>
        public Samples GetSamples(string samplesName)
        {
            if (this.loadedSamples == null)
            {
                throw new Exception("Resolver has not been initialised");
            }

            Samples samples = this.loadedSamples.GetValueOrDefault(samplesName);
            if (samples == null)
            {
                samples = this.samplesParser.LoadSamplesFromFile(samplesName);
                this.loadedSamples.Add(samplesName, samples);
            }

            return samples;
        }

        /// <summary>
        /// Resolves an unresolved value and adds it to the cache.
        /// If any other values need to be resolved before this one
        /// (dependencies) then they are also resolved and added to
        /// the cache.
        /// 
        /// The value and any dependencies are added to the cache
        /// once resolved. The cache provides a consistent view with
        /// consistent genders across a complete entity. When resolving
        /// a new entity pass in a new cache.
        /// </summary>
        public void Resolve(ResolvedValue resolving, Cache cache)
        {
            try
            {
                ResolveValue(resolving, cache);
            }
            catch (Exception e)
            {
                throw new Exception($"Cannot resolve {resolving.Name}: {e.Message}");
            }
        }

        /// <summary>
        /// Takes a literal string and looks for any embedded
        /// func.xxx(...) calls. Each embedded function is resolved
        /// and the result is included in the returned string.
        /// 
        /// Throws an exception if the value cannot be resolved
        /// </summary>
        private void ResolveValue(ResolvedValue resolving, Cache cache)
        {
            // See if it's already been resolved
            var resolved = cache.GetResolved(resolving.Name);
            if (resolved != null)
            {
                resolving.SetValue(resolved.Value, resolved.Gender);
                return;
            }

            if (resolving.UnresolvedValue == null)
            {
                throw new Exception("Unresolved value cannot be null");
            }

            // If value is already concrete it's resolved
            string value = resolving.UnresolvedValue;
            if (!value.Contains("func."))
            {
                resolving.SetValue(value);
                cache.SetResolved(resolving);
                return;
            }

            StringBuilder resolvedStr = new StringBuilder();
            Sample.Genders gender = Sample.Genders.NEUTRAL;

            int idx = 0;
            while (idx < value.Length)
            {
                int funcPos = value.IndexOf("func.", idx, StringComparison.CurrentCultureIgnoreCase);
                if (funcPos == -1)
                {
                    resolvedStr.Append(value.Substring(idx));
                    break;
                }

                resolvedStr.Append(value.Substring(idx, funcPos-idx));

                // Find function name
                idx = funcPos + 5;
                int argStart = value.IndexOf('(', idx);
                if (argStart == -1)
                {
                    throw new Exception($"Function 'func.{value.Substring(idx)}' has missing opening bracket");
                }
                string funcName = value.Substring(idx, argStart-idx).Trim();

                // Find matching close bracket
                argStart++;
                int argEnd = argStart;
                int openBrackets = 0;
                while (true)
                {
                    if (argEnd == value.Length)
                    {
                        throw new Exception($"Function 'func.{value.Substring(idx)}' has missing closing bracket");
                    }

                    // Ignore escaped brackets
                    if (value[argEnd] == '\\' && argEnd < value.Length-1 && (value[argEnd+1] == '(' || value[argEnd+1] == ')'))
                    {
                        argEnd += 2;
                        continue;
                    }

                    if (value[argEnd] == '(')
                    {
                        openBrackets++;
                    }
                    else if (value[argEnd] == ')')
                    {
                        if (openBrackets == 0)
                        {
                            break;
                        }
                        else
                        {
                            openBrackets--;
                        }
                    }

                    argEnd++;
                }

                string argList = value.Substring(argStart, argEnd-argStart);
                string parentName;
                int pos = resolving.Name == null? -1 : resolving.Name.LastIndexOf('.');
                if (pos == -1)
                {
                    parentName = resolving.Name;
                }
                else
                {
                    parentName = resolving.Name.Substring(0, pos);
                }

                resolvedStr.Append(eval(funcName, argList, cache, parentName, out var newGender));
                if (newGender != Sample.Genders.NEUTRAL)
                {
                    gender = newGender;
                }

                idx = argEnd + 1;
            }

            resolving.SetValue(resolvedStr.ToString(), gender);
            cache.SetResolved(resolving);
        }

        /// <summary>
        /// Evaluates a single function. Function name must be a known
        /// function or an exception will be thrown.
        /// 
        /// The arg list is resolved (as a whole) before being split into
        /// separate arguments so it may contain embedded func.xxx() calls.
        /// </summary>
        private string eval(string funcName, string argList, Cache cache, string parentName, out Sample.Genders gender)
        {
            string called = $"func.{funcName}({argList})";

            // Validate function name
            Funcs.FuncNames func;
            try
            {
                func = (Funcs.FuncNames)Enum.Parse(typeof(Funcs.FuncNames), funcName.Trim(), true);
            }
            catch (Exception)
            {
                throw new Exception($"Unknown function 'func.{funcName}(...)'. Must be one of: {String.Join(", ", Enum.GetNames(typeof(Funcs.FuncNames)))}");
            }

            // Resolve arg list (in case funcs are being passed as args to other funcs) but don't add
            // to cache as we want them to evaluate differently each time (if they're random).
            var resolvingArgs = new ResolvedValue(null, Entity.Types.STR, argList);
            ResolveValue(resolvingArgs, cache);
            gender = resolvingArgs.Gender;

            // Replace escaped brackets and split up arguments
            var args = SplitArgs(resolvingArgs.Value.Replace("\\(", "(").Replace("\\)", ")").Trim());

            // Call the relevant function
            string result;
            Sample.Genders newGender;
            switch (func)
            {
                case Funcs.FuncNames.STR:
                    return this.funcs.FuncStr(called, args);
                case Funcs.FuncNames.NUM:
                    return this.funcs.FuncNum(called, args, cache, parentName, this);
                case Funcs.FuncNames.DATE:
                    return this.funcs.FuncDate(called, args);
                case Funcs.FuncNames.TIME:
                    return this.funcs.FuncTime(called, args);
                case Funcs.FuncNames.GEN:
                    return this.funcs.FuncGen(called, args);
                case Funcs.FuncNames.RAND:
                    return this.funcs.FuncRand(called, args);
                case Funcs.FuncNames.PICK:
                    return this.funcs.FuncPick(called, args);
                case Funcs.FuncNames.SAMPLE:
                    result = this.funcs.FuncSample(called, args, cache, parentName, this, out newGender);
                    if (newGender != Sample.Genders.NEUTRAL)
                    {
                        gender = newGender;
                    }
                    return result;
                case Funcs.FuncNames.REF:
                    result = this.funcs.FuncRef(called, args, cache, parentName, this, out newGender);
                    if (newGender != Sample.Genders.NEUTRAL)
                    {
                        gender = newGender;
                    }
                    return result;
                case Funcs.FuncNames.IF:
                    return this.funcs.FuncIf(called, args, cache, parentName, this);
                case Funcs.FuncNames.MATH:
                    return this.funcs.FuncMath(called, args, cache, parentName, this);
                default:
                    throw new Exception($"Missing function name: {func}");
            }
        }

        /// <summary>
        /// Split up argument list (comma separated) but ignore
        /// commas within quoted arguments.
        /// </summary>
        private string[] SplitArgs(string argStr)
        {
            var args = new List<string>();

            if (argStr.Length == 0)
            {
                return args.ToArray();
            }

            bool inQuoted = false;
            var arg = new StringBuilder();

            foreach (var ch in argStr)
            {
                if (ch == '"')
                {
                    inQuoted = !inQuoted;
                }
                else if (!inQuoted && ch == ',')
                {
                    args.Add(arg.ToString().Trim());
                    arg = arg.Clear();
                }
                else
                {
                    arg.Append(ch);
                }
            }

            args.Add(arg.ToString().Trim());
            return args.ToArray();
        }
    }
}
