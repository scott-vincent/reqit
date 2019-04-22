using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace reqit.Models
{
    /// <summary>
    /// This model contains the entire set of data from a single samples file.
    /// 
    /// If the sample data contains gender it is split into two lists,
    /// male and female. The neutral samples are added to both lists.
    /// </summary>
    public class Samples
    {
        private Random random = new Random();

        public string Name { get; }
        public List<Sample> SampleList { get; }
        public bool HasGender { get; }
        public List<Sample> FemaleSampleList { get; }

        public Samples(string name, bool hasGender)
        {
            Name = name;
            SampleList = new List<Sample>();
            HasGender = hasGender;

            if (hasGender)
            {
                FemaleSampleList = new List<Sample>();
            }
        }

        public void AddSample(Sample sample)
        {
            if (!HasGender && sample.Gender != Sample.Genders.NEUTRAL)
            {
                throw new Exception("Cannot add sample with gender to non-gendered samples");
            }

            if (HasGender)
            {
                if (sample.Gender == Sample.Genders.MALE || sample.Gender == Sample.Genders.NEUTRAL)
                {
                    SampleList.Add(sample);
                }

                if (sample.Gender == Sample.Genders.FEMALE || sample.Gender == Sample.Genders.NEUTRAL)
                {
                    FemaleSampleList.Add(sample);
                }
            }
            else
            {
                SampleList.Add(sample);
            }
        }

        /// <summary>
        /// Pick a random sample from the set, optionally with the specified gender.
        /// If a sample is rare it will be picked less often (using the rarity value).
        /// </summary>
        public Sample Pick(Sample.Genders gender = Sample.Genders.NEUTRAL)
        {
            if (!HasGender && gender != Sample.Genders.NEUTRAL)
            {
                throw new Exception("Cannot pick a gendered sample from a set of un-gendered samples");
            }

            // If gender is not specified choose male or female at random
            if (HasGender && gender == Sample.Genders.NEUTRAL)
            {
                if (random.Next(2) == 0)
                {
                    gender = Sample.Genders.MALE;
                }
                else
                {
                    gender = Sample.Genders.FEMALE;
                }
            }

            // Copy the sample list so we can pick at random and
            // remove so we don't pick the same sample again.
            List<Sample> samples;
            if (!HasGender || gender == Sample.Genders.MALE)
            {
                samples = new List<Sample>(SampleList);
            }
            else
            {
                samples = new List<Sample>(FemaleSampleList);
            }

            // Pick a sample and, if it's rare, apply rarity percent and
            // if it isn't chosen, remove it from the list and pick again.
            // There is always at least one non-rare sample so the list
            // will never be exhausted.
            while (true)
            {
                int pickPos = random.Next(samples.Count);
                Sample sample = samples[pickPos];

                if (sample.Rarity > 0)
                {
                    // Roll the dice to see if we pick this rare sample
                    if (random.Next(100) >= sample.Rarity)
                    {
                        // Remove from list and pick again
                        samples.RemoveAt(pickPos);
                        continue;
                    }
                }

                // Got our sample
                return sample;
            }
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            if (HasGender)
            {
                sb.AppendLine($"{Name} (male):");
                foreach (var sample in SampleList)
                {
                    sb.AppendLine($"  {sample}");
                }

                sb.AppendLine($"\n{Name} (female):");
                foreach (var sample in FemaleSampleList)
                {
                    sb.AppendLine($"  {sample}");
                }
            }
            else
            {
                sb.AppendLine($"{Name}:");
                foreach (var sample in SampleList)
                {
                    sb.AppendLine($"  {sample.Value}");
                }
            }

            return sb.ToString();
        }
    }
}
