using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using reqit.CmdLine;
using reqit.Models;
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;

namespace reqit.Parsers
{
    public class SamplesParser : ISamplesParser
    {
        private enum Columns { GENDER, RARITY };

        public static string SAMPLES_DIR = Path.Combine(MyMain.GetWorkingDir(), "Samples");

        /// <summary>
        /// Scans the samples folder and returns the list of sample file names.
        /// Throws an exception if samples folder does not exist.
        /// </summary>
        public List<string> GetSampleNames()
        {
            var sampleNames = new List<string>();

            foreach (var file in Directory.GetFiles(SAMPLES_DIR))
            {
                sampleNames.Add(Path.GetFileName(file));
            }

            sampleNames.Sort();

            return sampleNames;
        }

        /// <summary>
        /// Looks in the Samples folder for the specified file and loads the samples file.
        /// Throws an exception if samples file does not exist.
        /// </summary>
        public Samples LoadSamplesFromFile(string samplesName)
        {
            string samplesFile = Path.Combine(SAMPLES_DIR, samplesName);

            if (!File.Exists(samplesFile))
            {
                throw new Exception($"Samples file '{samplesFile}' not found");
            }

            return LoadSamples(samplesName, File.ReadAllLines(samplesFile));
        }

        public Samples LoadSamples(string name, string[] lines)
        {
            int numCols = 1;
            int genderCol = 0;
            int rarityCol = 0;
            bool hasMaleNonRare = false;
            bool hasFemaleNonRare = false;
            int i = 0;

            // Skip blank lines and comments that don't contain a comma
            while (i < lines.Length && (lines[i].Trim().Length == 0 || (lines[i][0] == '#' && !lines[i].Contains(','))))
            {
                i++;
            }

            // Is there a header?
            if (i < lines.Length && lines[i][0] == '#' && lines[i].Contains(','))
            {
                var colNames = lines[i].Split(',');
                numCols = colNames.Length;

                if (numCols > 3)
                {
                    throw new Exception($"Multi-column samples '{name}' header must have 2 or 3 columns");
                }

                // Don't validate name of first column (can be anything)
                for (int col = 1; col < numCols; col++)
                {
                    string colName = colNames[col].Trim();
                    Columns colType;
                    try
                    {
                        colType = (Columns)Enum.Parse(typeof(Columns), colName, true);
                    }
                    catch (ArgumentException)
                    {
                        throw new Exception($"Multi-column samples '{name}' header has invalid column name '{colName}' - " +
                            $"must be one of: {String.Join(", ", Enum.GetNames(typeof(Columns)))}");
                    }

                    switch (colType)
                    {
                        case Columns.GENDER:
                            genderCol = col;
                            break;
                        case Columns.RARITY:
                            rarityCol = col;
                            break;
                        default:
                            throw new Exception($"SamplesParser missing code for column type: {colType}");
                    }
                }

                i++;
            }

            var samples = new Samples(name, genderCol != 0);

            while (i < lines.Length)
            {
                string value = lines[i].Trim();

                // Skip empty lines and comments
                if (value.Length == 0 || value[0] == '#')
                {
                    i++;
                    continue;
                }

                Sample.Genders gender = Sample.Genders.NEUTRAL;
                int rarity = 0;

                if (numCols > 1)
                {
                    var columns = value.Split(',');

                    if (columns.Length > numCols)
                    {
                        throw new Exception($"Multi-column samples file '{name}' line {i + 1} " +
                            $"has too many columns (or embedded comma) - must match number of columns in header.");
                    }

                    string[] colData = new string[numCols];

                    for (int col = 0; col < numCols; col++)
                    {
                        if (col < columns.Length)
                        {
                            colData[col] = columns[col].Trim();
                        }
                        else
                        {
                            colData[col] = "";
                        }
                    }

                    value = colData[0];
                    if (value.Length == 0)
                    {
                        throw new Exception($"Multi-column samples file '{name}' line {i + 1} " +
                            $"value cannot be blank. Use <null> if you want a null value.");
                    }

                    if (genderCol != 0)
                    {
                        string genderStr = colData[genderCol].ToUpper();

                        if (genderStr.Length > 0)
                        {
                            if (genderStr.Equals("M"))
                            {
                                gender = Sample.Genders.MALE;
                            }
                            else if (genderStr.Equals("F"))
                            {
                                gender = Sample.Genders.FEMALE;
                            }
                            else
                            {
                                throw new Exception($"Multi-column samples file '{name}' line {i + 1} " +
                                    $"has gender '{genderStr}' but expected M or F");
                            }
                        }
                    }

                    if (rarityCol != 0)
                    {
                        string rarityStr = colData[rarityCol];

                        if (rarityStr.Length > 0)
                        {
                            try
                            {
                                rarity = int.Parse(rarityStr);
                            }
                            catch (Exception)
                            {
                                throw new Exception($"Multi-column samples file '{name}' line {i + 1} " +
                                    $"has non-integer rarity '{rarityStr}'");
                            }

                            if (rarity < 1 || rarity > 99)
                            {
                                throw new Exception($"Multi-column samples file '{name}' line {i + 1} " +
                                    $"has rarity {rarity} but expected either blank or value between 1 and 99");
                            }
                        }
                    }
                }

                if ((gender == Sample.Genders.MALE || gender == Sample.Genders.NEUTRAL) && rarity == 0)
                {
                    hasMaleNonRare = true;
                }

                if ((gender == Sample.Genders.FEMALE || gender == Sample.Genders.NEUTRAL) && rarity == 0)
                {
                    hasFemaleNonRare = true;
                }

                samples.AddSample(new Sample(value, gender, rarity));
                i++;
            }

            // Samples must contain at least one sample. If gendered, must be
            // true for both male and female. If rarity, there must be at least
            // one non-rare sample.
            if (!hasMaleNonRare)
            {
                StringBuilder modifier = new StringBuilder();
                if (genderCol != 0)
                {
                    modifier.Append(" Male");
                }
                if (rarityCol != 0)
                {
                    modifier.Append(modifier.Length == 0? " " : ", ");
                    modifier.Append("Non-rare (rarity left blank)");
                }

                throw new Exception($"Samples file '{name}' " +
                    $"must have at least one{modifier} sample");
            }

            if (genderCol != 0 && !hasFemaleNonRare)
            {
                StringBuilder modifier = new StringBuilder();
                if (genderCol != 0)
                {
                    modifier.Append(" Female");
                }
                if (rarityCol != 0)
                {
                    modifier.Append(modifier.Length == 0 ? " " : ", ");
                    modifier.Append("Non-rare (rarity left blank)");
                }

                throw new Exception($"Multi-column samples file '{name}' " +
                    $"must have at least one{modifier} sample");
            }

            return samples;
        }
    }
}
