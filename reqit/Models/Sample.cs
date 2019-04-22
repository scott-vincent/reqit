using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This model contains a single sample, i.e. one line
    /// of data from a samples file.
    /// 
    /// Samples can contain gender and rarity% information.
    /// Rarity=0 means no rarity, i.e. sample can appear any time.
    /// Rarity=5 means sample appears 5% of the time.
    /// </summary>
    public class Sample
    {
        public enum Genders { MALE, FEMALE, NEUTRAL };

        public string Value { get; }
        public Genders Gender { get; }
        public int Rarity { get; }

        public Sample(string value, Genders gender, int rarity)
        {
            Value = value;
            Gender = gender;
            Rarity = rarity;
        }

        public override string ToString()
        {
            if (Rarity == 0)
            {
                return $"{Value}";
            }
            else
            {
                return $"{Value} ({Rarity}%)";
            }
        }
    }
}
