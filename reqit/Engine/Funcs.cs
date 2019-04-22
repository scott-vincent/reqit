using reqit.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Engine
{
    /// <summary>
    /// Functions allow a variable number of arguments but an
    /// exception will be thrown if a bad number of args are passed.
    /// </summary>
    public class Funcs : IFuncs
    {
        public enum FuncNames { STR, NUM, DATE, TIME, GEN, RAND, PICK, SAMPLE, REF, IF, MATH };
        private enum StrTypes { CAP, UPPER, LOWER, MIXED };
        private enum DateTypes { Y, M, D, h, m, s, i }

        private Random random = new Random();

        public string FuncDate(string called, string[] args)
        {
            string help = "Use func.date(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.date(arg1, [arg2], [arg3]) where arg1 is either NOW or a date in format yyyy-MM-dd [HH:mm:ss] " +
                    "and arg2 is an amount to adjust the date by, e.g. -5d to subtract 5 days, +1y to add 1 year etc. " +
                    "(may be left blank if you just want to format a date) and arg3 is the format you want the date in " +
                    "(only works if attribute type is STR as DATE type is always in ISO format). For arg2 you can also " +
                    "specify a range to add or subtract (includes fractions of the specified unit), e.g. NOW, -18-102y will " +
                    "give you a random date of birth for somebody between 18 and 102 years old. " +
                    "For arg3 you can specify 'epoch' if you want the Unix epoch time.";
                throw new Exception(usage);
            }

            if (args.Length < 1 || args.Length > 3)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            DateTime date;
            if (args[0].Equals("NOW", StringComparison.CurrentCultureIgnoreCase))
            {
                date = DateTime.Now;
            }
            else
            {
                date = DateTime.Parse(args[0]);
            }

            if (args.Length > 1 && args[1].Length > 0)
            {
                char[] validUnits = { 'y', 'M', 'd', 'H', 'm', 's' };

                try
                {
                    date = AdjustDate(date, args[1], validUnits);
                }
                catch (Exception e)
                {
                    throw new Exception($"{called} second argument {e.Message}. {help}");
                }
            }

            if (args.Length == 3)
            {
                if (args[2].Equals("epoch", StringComparison.CurrentCultureIgnoreCase))
                {
                    TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
                    int secondsSinceEpoch = (int)t.TotalSeconds;
                    return secondsSinceEpoch.ToString();
                }
                else
                {
                    try
                    {
                        return date.ToString(args[2]);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{called} has bad date format: {e.Message}");
                    }
                }
            }

            return date.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
        }

        public string FuncGen(string called, string[] args)
        {
            string help = "Use func.gen(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.gen(arg) where arg is either UUID " +
                    "or a string of chars where # will be replaced by a random digit, ^ will be replaced by a random uppercase letter, " +
                    "@ will be replaced by a random lowercase letter and * will be replaced by a random mixed case letter. " +
                    "Any other chars are treated as literals and will be retained.";
                throw new Exception(usage);
            }

            if (args.Length < 1)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            // Only expecting one argument but want to allow embedded commas
            // so combine args back to single argument and re-add commas.
            var sb = new StringBuilder(args[0]);
            for (int i = 1; i < args.Length; i++)
            {
                sb.Append($",{args[i]}");
            }
            string arg = sb.ToString();

            if (arg.Equals("UUID", StringComparison.CurrentCultureIgnoreCase))
            {
                return Guid.NewGuid().ToString();
            }

            // Replace special chars with generated ones
            var generated = new StringBuilder();
            foreach (var ch in arg)
            {
                switch (ch)
                {
                    case '#':
                        generated.Append((char)('0' + random.Next(10)));
                        break;
                    case '^':
                        generated.Append((char)('A' + random.Next(26)));
                        break;
                    case '@':
                        generated.Append((char)('a' + random.Next(10)));
                        break;
                    case '*':
                        if (random.Next(2) == 0)
                        {
                            generated.Append((char)('A' + random.Next(26)));
                        }
                        else
                        {
                            generated.Append((char)('a' + random.Next(26)));
                        }
                        break;
                    default:
                        generated.Append(ch);
                        break;
                }
            }

            return generated.ToString();
        }

        public string FuncIf(string called, string[] args, Cache cache, string parent, IResolver resolver)
        {
            string help = "Use func.if(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.if(arg1, arg2, arg3, arg4) where " +
                    "arg1=value1, arg2='op'value2 (where op is <, > or =), arg3=returned value if value1'op'value2 is true " +
                    "and arg4=returned value if value1'op'value2 is false. Value1 and value2 may be positive or negative " +
                    "numbers, strings or attribute names (if preceeded by '~').";
                throw new Exception(usage);
            }

            if (args.Length != 4)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0 || args[1].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            GetOp("+" + args[0], new char[] { '+' }, cache, parent, resolver, out var op, out var val1Str, out var val1Num);

            string val2Str;
            double val2Num;
            try
            {
                GetOp(args[1], new char[] { '>', '<', '=' }, cache, parent, resolver, out op, out val2Str, out val2Num);
            }
            catch (Exception e)
            {
                throw new Exception($"{called} second argument {e.Message}. {help}");
            }

            bool passed = false;

            if (val1Str == null && val2Str == null)
            {
                // Comparing numbers
                switch (op)
                {
                    case '>':
                        passed = (val1Num > val2Num);
                        break;
                    case '<':
                        passed = (val1Num < val2Num);
                        break;
                    case '=':
                    default:
                        passed = (val1Num == val2Num);
                        break;
                }
            }
            else
            {
                // Comparing strings
                if (val1Str == null)
                {
                    val1Str = val1Num.ToString();
                }
                else if (val2Str == null)
                {
                    val2Str = val2Num.ToString();
                }

                switch (op)
                {
                    case '>':
                        passed = (val1Str.CompareTo(val2Str) == 1);
                        break;
                    case '<':
                        passed = (val1Str.CompareTo(val2Str) == -1);
                        break;
                    case '=':
                    default:
                        passed = (val1Str.Equals(val2Str));
                        break;
                }
            }

            if (passed)
            {
                return args[2];
            }
            else
            {
                return args[3];
            }
        }

        public string FuncMath(string called, string[] args, Cache cache, string parent, IResolver resolver)
        {
            string help = "Use func.math(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.math(arg1, arg2, [arg3], ...) where " +
                    "arg1=value1, arg2='op'value2 (where op is +, -, * or /), all further args in same format as arg2. " +
                    "Values may be positive or negative numbers or attribute names (if preceeded by '~').";
                throw new Exception(usage);
            }

            if (args.Length < 2)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            var validOp = new char[] { '+', '-', '*', '/' };
            char op;
            string valStr;
            double valNum;

            double total = 0;
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i].Length == 0)
                {
                    throw new Exception($"{called} has empty argument. {help}");
                }

                string arg;
                if (i == 0)
                {
                    arg = "+" + args[0];
                }
                else
                {
                    arg = args[i];
                }

                try
                {
                    GetOp(arg, validOp, cache, parent, resolver, out op, out valStr, out valNum);
                }
                catch (Exception e)
                {
                    throw new Exception($"{called} argument {i + 1} {e.Message}. {help}");
                }

                if (valStr != null)
                {
                    throw new Exception($"{called} argument {i + 1} cannot be resolved to a number. {help}");
                }

                switch (op)
                {
                    case '+':
                        total += valNum;
                        break;
                    case '-':
                        total -= valNum;
                        break;
                    case '*':
                        total *= valNum;
                        break;
                    case '/':
                    default:
                        total /= valNum;
                        break;
                }
            }

            return total.ToString();
        }

        public string FuncNum(string called, string[] args, Cache cache, string parent, IResolver resolver)
        {
            string help = "Use func.num(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.num(arg1, [arg2]) where arg1 is either the number of digits to generate, min-max number of digits " +
                    "to generate or an attribute name (if preceeded by '~') and arg2 is the required number of decimal places with optional " +
                    "t suffix to truncate or r suffix to round.";
                throw new Exception(usage);
            }

            if (args.Length < 1 || args.Length > 2)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            if (args[0][0] == '~')
            {
                string name = args[0].Substring(1);
                var refValue = GetRefValue(name, cache, parent, resolver);

                double value;
                try
                {
                    value = double.Parse(refValue.Value);
                }
                catch (Exception)
                {
                    throw new Exception($"{called} attribute {name} value {refValue.Value} is not a number. {help}");
                }

                if (!ApplyDecimalPlaces(args[1], ref value))
                {
                    throw new Exception($"{called} second argument must be the required number of decimal places with a t or r suffix. {help}");
                }

                return value.ToString();
            }
            else
            {
                if (!GetRange(args[0], out var min, out var max))
                {
                    throw new Exception($"{called} first argument must be a number or a range. {help}");
                }

                int numLen;
                if (min == -1)
                {
                    numLen = max;
                }
                else
                {
                    numLen = random.Next(min, max + 1);
                }

                var sb = new StringBuilder();
                for (int i = 0; i < numLen; i++)
                {
                    if (i == 0)
                    {
                        sb.Append((char)('1' + random.Next(9)));
                    }
                    else
                    {
                        sb.Append((char)('0' + random.Next(10)));
                    }
                }

                if (args.Length == 2)
                {
                    if (!AddDecimalPlaces(args[1], ref sb))
                    {
                        throw new Exception($"{called} second argument must be the required number of decimal places. {help}");
                    }
                }

                return sb.ToString();
            }
        }

        public string FuncPick(string called, string[] args)
        {
            string help = "Use func.pick(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.pick(arg1, arg2, [arg3], ...) where one of the arguments is chosen at random.";
                throw new Exception(usage);
            }

            if (args.Length < 1)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            return args[random.Next(args.Length)];
        }

        public string FuncRand(string called, string[] args)
        {
            string help = "Use func.rand(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.rand(arg1, [arg2]) where arg1 is a number, e.g. func.rand(4) to generate a number between 0 and 3 or " +
                    "arg1 is a range, e.g. func.rand(1-3) to generate a number between 1 and 3. " +
                    "Arg2 is the required number of decimal places.";
                throw new Exception(usage);
            }

            if (args.Length < 1 || args.Length > 2)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            if (!GetRange(args[0], out var min, out var max))
            {
                throw new Exception($"{called} first argument must be a number or a range. {help}");
            }

            int num;
            if (min == -1)
            {
                num = random.Next(max);
            }
            else
            {
                num = random.Next(min, max + 1);
            }

            if (args.Length == 2)
            {
                var sb = new StringBuilder(num.ToString());
                if (!AddDecimalPlaces(args[1], ref sb))
                {
                    throw new Exception($"{called} second argument must be the required number of decimal places. {help}");
                }
                return sb.ToString();
            }
            else
            {
                return num.ToString();
            }
        }

        public string FuncRef(string called, string[] args, Cache cache, string parent, IResolver resolver,
                out Sample.Genders gender)
        {
            string help = "Use func.ref(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.ref(arg) where arg is another attribute to take the value from.";
                throw new Exception(usage);
            }

            if (args.Length != 1)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            ResolvedValue refValue;
            try
            {
                refValue = GetRefValue(args[0], cache, parent, resolver);
            }
            catch (Exception e)
            {
                throw new Exception($"{called} {e.Message}. {help}");
            }

            gender = refValue.Gender;
            return refValue.Value;
        }

        /// <summary>
        /// The sample function has a circular dependency on the resolver (so we can
        /// have samples based on the gender of other fields which may not be resolved
        /// yet) so we have to pass the resolver in as a parameter.
        /// </summary>
        public string FuncSample(string called, string[] args, Cache cache, string parent, IResolver resolver,
                out Sample.Genders gender)
        {
            string help = "Use func.sample(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.sample(arg1, [arg2]) where arg1 is the name of the samples file to use and arg2 is " +
                    "either another attribute to take the gender from, or M or F to use a fixed gender.";
                throw new Exception(usage);
            }

            if (args.Length < 1 || args.Length > 2)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            gender = Sample.Genders.NEUTRAL;
            if (args.Length == 2)
            {
                if (args[1].Equals("M", StringComparison.CurrentCultureIgnoreCase))
                {
                    gender = Sample.Genders.MALE;
                }
                else if (args[1].Equals("F", StringComparison.CurrentCultureIgnoreCase))
                {
                    gender = Sample.Genders.FEMALE;
                }
                else
                {
                    // Inherit gender from referenced attribute.
                    string refName;
                    if (args[1].Length > 1 && args[1][0] == '~')
                    {
                        refName = parent + "." + args[1].Substring(1);
                    }
                    else
                    {
                        refName = parent + "." + args[1];
                    }

                    Entity refAttrib;
                    try
                    {
                        refAttrib = resolver.FindEntity(refName);
                    }
                    catch (Exception)
                    {
                        throw new Exception($"{called} references unknown attribute '{refName}'");
                    }

                    var refValue = new ResolvedValue(refName, refAttrib.Type, refAttrib.Value);
                    try
                    {
                        resolver.Resolve(refValue, cache);
                    }
                    catch (Exception e)
                    {
                        throw new Exception($"{called} references unresolvable attribute '{refName}': {e.Message}");
                    }

                    gender = refValue.Gender;
                }
            }

            var samples = resolver.GetSamples(args[0]);
            var sample = samples.Pick(gender);
            gender = sample.Gender;
            return sample.Value;
        }

        public string FuncStr(string called, string[] args)
        {
            string help = "Use func.str(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.str(arg1, [arg2]) where arg1 is the number of chars to generate or min-max number of chars to " +
                    "generate, e.g. func.str(4-6) and arg2 is cap, upper, lower (default) or mixed where cap = first letter only is upper case.";
                throw new Exception(usage);
            }

            StrTypes strType;
            if (args.Length == 1)
            {
                strType = StrTypes.LOWER;
            }
            else if (args.Length == 2)
            {
                try
                {
                    strType = (StrTypes)Enum.Parse(typeof(StrTypes), args[1], true);
                }
                catch (ArgumentException)
                {
                    throw new Exception($"{called} arg2 must be one of: {String.Join(", ", Enum.GetNames(typeof(StrTypes)))}. {help}");
                }
            }
            else
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            var nums = args[0].Split('-');
            bool validArg1 = (nums.Length >= 1 && nums.Length <= 2);

            int min = 0;
            if (validArg1)
            {
                try
                {
                    min = int.Parse(nums[0]);
                }
                catch (Exception)
                {
                    validArg1 = false;
                }
            }

            int max = min;
            if (validArg1 && nums.Length == 2)
            {
                try
                {
                    max = int.Parse(nums[1]);
                }
                catch (Exception)
                {
                    validArg1 = false;
                }
            }

            if (!validArg1)
            {
                throw new Exception($"{called} first argument must be a number or a range. {help}");
            }

            int strLen = random.Next(min, max + 1);
            var sb = new StringBuilder();
            for (int i = 0; i < strLen; i++)
            {
                switch (strType)
                {
                    case StrTypes.UPPER:
                        sb.Append((char)('A' + random.Next(26)));
                        break;
                    case StrTypes.CAP:
                    case StrTypes.LOWER:
                        if (i == 0 && strType == StrTypes.CAP)
                        {
                            sb.Append((char)('A' + random.Next(26)));
                        }
                        else
                        {
                            sb.Append((char)('a' + random.Next(26)));
                        }
                        break;
                    case StrTypes.MIXED:
                        if (random.Next(2) == 0)
                        {
                            sb.Append((char)('A' + random.Next(26)));
                        }
                        else
                        {
                            sb.Append((char)('a' + random.Next(26)));
                        }
                        break;
                    default:
                        break;
                }
            }

            return sb.ToString();
        }

        public string FuncTime(string called, string[] args)
        {
            string help = "Use func.time(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.time(arg1, [arg2], [arg3]) where arg1 is either NOW or a time in format HH:mm[:ss] " +
                    "(or as a full date) and arg2 is an amount to adjust the time by, e.g. -5H to subtract 5 hours, +1m to add 1 minute etc. " +
                    "(may be left blank if you just want to format a time) and arg3 is the format you want the time in " +
                    "(attribute type must be STR). You can also specify a range to add or subtract, e.g. -0-24H will " +
                    "subtract anywhere between 0 and 24 hours including fractions of hours.";
                throw new Exception(usage);
            }

            if (args.Length < 1 || args.Length > 3)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            DateTime date;
            if (args[0].Equals("NOW", StringComparison.CurrentCultureIgnoreCase))
            {
                date = DateTime.Now;
            }
            else
            {
                date = DateTime.Parse(args[0]);
            }

            if (args.Length > 1 && args[1].Length > 0)
            {
                char[] validUnits = { 'H', 'm', 's' };

                try
                {
                    date = AdjustDate(date, args[1], validUnits);
                }
                catch (Exception e)
                {
                    throw new Exception($"{called} second argument {e.Message}. {help}");
                }
            }

            if (args.Length == 3)
            {
                try
                {
                    return date.ToString(args[2]);
                }
                catch (Exception e)
                {
                    throw new Exception($"{called} has bad time format: {e.Message}");
                }
            }

            return date.ToString("HH:mm:ss");
        }

        /// <summary>
        /// This is a template to create new functions from
        /// </summary>
        public string FuncDummy(string called, string[] args)
        {
            string help = "Use func.dummy(--help) for help.";

            if (args.Length == 1 && args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase))
            {
                // Show usage
                string usage = "Usage: func.dummy(arg1, [arg2]) where " +
                    "arg1=???";
                throw new Exception(usage);
            }

            if (args.Length < 1 || args.Length > 2)
            {
                throw new Exception($"{called} has bad number of arguments. {help}");
            }

            if (args[0].Length == 0)
            {
                throw new Exception($"{called} has empty argument. {help}");
            }

            return "MyDummy";
        }

        private ResolvedValue GetRefValue(string refName, Cache cache, string parent, IResolver resolver)
        {
            if (refName.Length > 1 && refName[0] == '~')
            {
                refName = refName.Substring(1);
            }

            refName = parent + "." + refName;
            Entity refAttrib;
            try
            {
                refAttrib = resolver.FindEntity(refName);
            }
            catch (Exception)
            {
                throw new Exception($"references unknown attribute '{refName}'");
            }

            var refValue = new ResolvedValue(refName, refAttrib.Type, refAttrib.Value);
            try
            {
                resolver.Resolve(refValue, cache);
            }
            catch (Exception e)
            {
                throw new Exception($"references unresolvable attribute '{refName}': {e.Message}");
            }

            return refValue;
        }

        private bool GetRange(string arg, out int min, out int max)
        {
            min = -1;
            max = -1;

            var nums = arg.Split('-');
            if (nums.Length < 1 || nums.Length > 2)
            {
                return false;
            }

            try
            {
                max = int.Parse(nums[0]);
            }
            catch (Exception)
            {
                return false;
            }

            if (nums.Length == 2)
            {
                min = max;
                try
                {
                    max = int.Parse(nums[1]);
                }
                catch (Exception)
                {
                    return false;
                }
            }

            return true;
        }

        private bool AddDecimalPlaces(string arg, ref StringBuilder num)
        {
            int dp = 0;
            try
            {
                dp = int.Parse(arg);
            }
            catch (Exception)
            {
                return false;
            }

            if (dp < 0)
            {
                return false;
            }

            if (dp > 0)
            {
                // Add decimal places
                num.Append('.');

                for (int i = 0; i < dp; i++)
                {
                    num.Append((char)('0' + random.Next(10)));
                }
            }

            return true;
        }
        private bool ApplyDecimalPlaces(string arg, ref double num)
        {
            if (arg.Length == 0)
            {
                return false;
            }

            char suffix = arg[arg.Length - 1];
            if (suffix == 't' || suffix == 'r')
            {
                arg = arg.Substring(0, arg.Length - 1);

                if (arg.Length == 0)
                {
                    return false;
                }
            }
            else
            {
                suffix = 'r';
            }

            int dp = 0;
            try
            {
                dp = int.Parse(arg);
            }
            catch (Exception)
            {
                return false;
            }

            if (dp < 0)
            {
                return false;
            }

            if (suffix == 't')
            {
                double shift = Math.Pow(10, dp);
                num = Math.Truncate(num * shift) / shift;
            }
            else if (suffix == 'r')
            {
                num = Math.Round(num, dp, MidpointRounding.AwayFromZero);
            }

            return true;
        }

        private DateTime AdjustDate(DateTime date, string arg, char[] validUnits)
        {
            if (arg.Length < 2)
            {
                throw new Exception("must be in format [+|-]num[-num]unit");
            }

            if (arg[0] != '+' && arg[0] != '-')
            {
                arg = "+" + arg;
            }
            bool isNegative = (arg[0] == '-');
            char unit = arg[arg.Length - 1];

            if (!GetRange(arg.Substring(1, arg.Length - 2), out int min, out int max))
            {
                throw new Exception("must be in format [+|-]num[-num]unit");
            }

            if (!validUnits.Contains(unit))
            {
                throw new Exception($"must end with one of: {String.Join(", ", validUnits)}");
            }

            if (min == -1)
            {
                if (isNegative)
                {
                    max = -max;
                }

                return GetAdjusted(date, max, unit);
            }
            else
            {
                if (isNegative)
                {
                    min = -min;
                    max = -max;
                }

                var minDate = GetAdjusted(date, min, unit);
                var diffSeconds = (GetAdjusted(date, max, unit) - minDate).TotalSeconds;
                double adjustSeconds = random.NextDouble() * diffSeconds;
                return minDate.AddSeconds(adjustSeconds);
            }
        }

        private DateTime GetAdjusted(DateTime date, int amount, char unit)
        {
            switch (unit)
            {
                case 'y':
                    return date.AddYears(amount);
                case 'M':
                    return date.AddMonths(amount);
                case 'd':
                    return date.AddDays(amount);
                case 'H':
                    return date.AddHours(amount);
                case 'm':
                    return date.AddMinutes(amount);
                case 's':
                default:
                    return date.AddSeconds(amount);
            }
        }

        /// <summary>
        /// Parses '>4', '<-7.5', '+2.45', '=Blah' etc and returns the op and
        /// operand as separate values. Operand can be a number, string or
        /// attribute name (if preceeded by ~). An attribute is resolved to
        /// its number or string value. If a number is returned, valStr will
        /// be set to null.
        /// </summary>
        private void GetOp(string arg, char[] validOps, Cache cache, string parent, IResolver resolver,
                out char op, out string valStr, out double valNum)
        {
            if (arg.Length < 2)
            {
                throw new Exception("must be in format 'op'num, e.g. >-4");
            }

            op = arg[0];
            string operand = arg.Substring(1);

            if (!validOps.Contains(op))
            {
                throw new Exception($"must start with one of: {String.Join(", ", validOps)}");
            }

            if (operand[0] == '~')
            {
                var refValue = GetRefValue(operand, cache, parent, resolver);
                operand = refValue.Value;
            }

            try
            {
                valNum = double.Parse(operand);
                valStr = null;
            }
            catch (Exception)
            {
                valNum = -1;
                valStr = operand;
            }
        }
    }
}
