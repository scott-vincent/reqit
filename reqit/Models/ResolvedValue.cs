using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This model stores the value of a bottom-level (no children) entity
    /// that either needs resolving or has already been resolved (concrete value).
    /// 
    /// When a value is resolved it may have a gender associated
    /// with it (if the resolution included a gendered sample).
    /// 
    /// Note: If a gendered sample is resolved as neutral, a resolved
    /// sample is still stored as male or female (chosen at random).
    /// This is so that if multiple other attributes rely on the gender
    /// of this value they will have a consistent gender. A resolved
    /// value will only ever be stored as neutral if it is resolved
    /// with no gender-specific references.
    /// </summary>
    public class ResolvedValue
    {
        public string Name { get; }
        public Entity.Types Type { get; }
        public string UnresolvedValue { get; private set; }
        public Sample.Genders Gender { get; private set; }
        public string Value { get; private set; }

        public ResolvedValue(string name, Entity.Types type, string unresolvedValue)
        {
            Name = name;
            Type = type;
            UnresolvedValue = unresolvedValue;
            Gender = Sample.Genders.NEUTRAL;
            Value = null;
        }

        public void SetValue(string value, Sample.Genders gender = Sample.Genders.NEUTRAL)
        {
            Value = value;
            Gender = gender;
        }

        /// <summary>
        /// Returns the formatted value, i.e. string and date types are
        /// enclosed in quotes. Chars are also escaped where needed.
        /// </summary>
        public string GetValue(bool isSql)
        {
            if (Value == null)
            {
                throw new Exception($"Attribute {Name} has not been resolved");
            }

            // Only STR and DATE should have quotes around their values
            if (Value.Equals(Entity.NULL))
            {
                return "null";
            }
            else if (Type == Entity.Types.STR || Type == Entity.Types.DATE)
            {
                if (isSql)
                {
                    return "'" + Value.Replace("'", "''") + "'";
                }
                else
                {
                    if (Value.StartsWith("#obj!#:"))
                    {
                        return Value.Substring(7);
                    }
                    else
                    {
                        return "\"" + Value.Replace("\"", "\\\"") + "\"";
                    }
                }
            }
            else
            {
                return Value;
            }
        }

        public override string ToString()
        {
            if (Value == null)
            {
                return $"Unresolved: {Name}";
            }
            else if (Gender == Sample.Genders.NEUTRAL)
            {
                return $"Resolved: {Name}, Value: {Value}";
            }
            else
            {
                return $"Resolved: {Name}, Value: {Value}, Gender: {Gender}";
            }
        }
    }
}
