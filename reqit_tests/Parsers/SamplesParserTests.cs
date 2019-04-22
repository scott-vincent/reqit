using Moq;
using reqit.Engine;
using reqit.Models;
using reqit.Parsers;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using Xunit.Abstractions;

namespace reqit_tests.Parsers
{
    public class SamplesParserTestsBase : IDisposable
    {
        protected SamplesParser samplesParser;

        protected SamplesParserTestsBase()
        {
            // Called before every test
            this.samplesParser = new SamplesParser();
        }

        public void Dispose()
        {
            // Called after every test
        }
    }

    public class SamplesParserTests : SamplesParserTestsBase
    {
        private static string COMPLEX_SAMPLES = @"
# Title, Gender, Rarity
<null>,,5
Mr.,M
Mrs.,F
Miss,F
Ms.,F
Dr.,,10
Prof.,,5
Lord,M,2
Lady,F,2
Blah1
Blah2,
Blah3,,
            ";

        //private readonly ITestOutputHelper output;

        //public SamplesParserTests(ITestOutputHelper output)
        //{
        //    this.output = output;
        //    this.output.WriteLine("Running Test");
        //}

        [Fact]
        public void TestGenderSamples()
        {
            Samples samples = this.samplesParser.LoadSamples("test", COMPLEX_SAMPLES.Split("\r\n"));
            Assert.NotNull(samples);

            // male + neutral samples
            Assert.Equal(8, samples.SampleList.Count);
            Assert.Equal("Dr.", samples.SampleList[2].Value);
            Assert.Equal(Sample.Genders.NEUTRAL, samples.SampleList[2].Gender);
            Assert.Equal(10, samples.SampleList[2].Rarity);
            Assert.Equal("Lord", samples.SampleList[4].Value);
            Assert.Equal(Sample.Genders.MALE, samples.SampleList[4].Gender);
            Assert.Equal(2, samples.SampleList[4].Rarity);

            // female + neutral samples
            Assert.Equal(10, samples.FemaleSampleList.Count);
            Assert.Equal("Mrs.", samples.FemaleSampleList[1].Value);
            Assert.Equal(Sample.Genders.FEMALE, samples.FemaleSampleList[1].Gender);
            Assert.Equal(0, samples.FemaleSampleList[1].Rarity);
            Assert.Equal("Dr.", samples.FemaleSampleList[4].Value);
            Assert.Equal(Sample.Genders.NEUTRAL, samples.FemaleSampleList[4].Gender);
            Assert.Equal(10, samples.FemaleSampleList[4].Rarity);
        }

        [Fact]
        public void TestSimpleSamples()
        {
            string[] sampleList = { "Sample 1", "Sample 2", "Sample 3" };

            Samples samples = this.samplesParser.LoadSamples("test", sampleList);
            Assert.NotNull(samples);
            Assert.Equal(3, samples.SampleList.Count);

            Assert.Equal("Sample 3", samples.SampleList[2].Value);
            Assert.Equal(Sample.Genders.NEUTRAL, samples.SampleList[2].Gender);
            Assert.Equal(0, samples.SampleList[2].Rarity);
        }

        [Theory]
        [InlineData("")]
        [InlineData("# Comment")]
        [InlineData("# Name, Gender, Rarity")]
        [InlineData("# Comment", "# Name, Rarity")]
        public void TestNoSamples(params string[] sampleList)
        {
            Action samplesLoad = () => this.samplesParser.LoadSamples("test", sampleList);
            var ex = Record.Exception(samplesLoad);
            Assert.NotNull(ex);
            Assert.Contains("at least one", ex.Message);
        }

        [Theory]
        [InlineData("sample", "My,Sample")]
        [InlineData("sample", "My, 2nd, Sample")]
        [InlineData("sample", ",sample")]
        public void TestCommaSamples(params string[] sampleList)
        {
            Samples samples = this.samplesParser.LoadSamples("test", sampleList);
            Assert.NotNull(samples);
            Assert.Equal(2, samples.SampleList.Count);
            Assert.Equal(sampleList[1], samples.SampleList[1].Value);
        }

        [Theory]
        [InlineData("# Name, Gender, Rarity", "sample1,M", "sample2,F")]
        [InlineData("", "# Name, Gender, Rarity", "sample1, M", "sample2,F")]
        [InlineData("", "", "", "# Name, Gender", "sample1,M", "sample2, F")]
        [InlineData("#Comment1", "# Comment 2", "", "#Name,Rarity", "sample1", "sample2,")]
        [InlineData("#Comment1", "# Comment 2", "", "#Name,Gender,Rarity", "sample1,M", "sample2,F")]
        public void TestGoodHeader(params string[] sampleList)
        {
            Samples samples = this.samplesParser.LoadSamples("test", sampleList);
            Assert.NotNull(samples);
        }

        [Theory]
        [InlineData("# Name, Blah, Rarity")]
        [InlineData("# Name, Gender, Blah")]
        [InlineData("# Name, Gender, Rarity, Blah")]
        [InlineData("# Name,")]
        public void TestBadHeader(params string[] sampleList)
        {
            Action samplesLoad = () => this.samplesParser.LoadSamples("test", sampleList);
            var ex = Record.Exception(samplesLoad);
            Assert.NotNull(ex);
            Assert.Contains("header", ex.Message);
        }

        [Theory]
        [InlineData("# Name, Gender, Rarity", "sample, M, 1, Blah")]
        [InlineData("# Name, Gender, Rarity", "sample,M,1,")]
        [InlineData("# Name, Gender", "sample, M, 1")]
        [InlineData("# Name, Gender", "sample, M,")]
        public void TestBadColumnCount(params string[] sampleList)
        {
            Action samplesLoad = () => this.samplesParser.LoadSamples("test", sampleList);
            var ex = Record.Exception(samplesLoad);
            Assert.NotNull(ex);
            Assert.Contains("too many columns", ex.Message);
        }

        [Theory]
        [InlineData("# Name, Gender, Rarity", "sample, Bad")]
        [InlineData("# Name, Gender, Rarity", "sample, Male")]
        [InlineData("# Name, Gender", "sample, Female")]
        [InlineData("# Name, Gender, Rarity", "sample,blah,10")]
        public void TestBadGender(params string[] sampleList)
        {
            Action samplesLoad = () => this.samplesParser.LoadSamples("test", sampleList);
            var ex = Record.Exception(samplesLoad);
            Assert.NotNull(ex);
            Assert.Contains("expected M or F", ex.Message);
        }

        [Theory]
        [InlineData("# Name, Gender, Rarity", "sample, M, 0")]
        [InlineData("# Name, Gender, Rarity", "sample,, 100")]
        [InlineData("# Name, Rarity", "sample, 123")]
        [InlineData("# Name, Rarity", "sample, blah")]
        public void TestBadRarity(params string[] sampleList)
        {
            Action samplesLoad = () => this.samplesParser.LoadSamples("test", sampleList);
            var ex = Record.Exception(samplesLoad);
            Assert.NotNull(ex);

            if (sampleList[1].Contains("blah"))
            {
                Assert.Contains("non-integer", ex.Message);
            }
            else
            {
                Assert.Contains("value between 1 and 99", ex.Message);
            }
        }

        [Theory]
        [InlineData("# Name, Gender, Rarity", "sample, M")]
        [InlineData("# Name, Gender, Rarity", "sample1, M", "sample2, F, 5")]
        [InlineData("# Name, Gender, Rarity", "sample1, M, 5", "sample2, F")]
        [InlineData("# Name, Gender, Rarity", "sample, F")]
        [InlineData("# Name, Gender, Rarity", "sample1, F", "sample2, M, 5")]
        [InlineData("# Name, Gender, Rarity", "sample1, F, 5", "sample2, M")]
        [InlineData("# Name, Rarity", "sample, 5")]
        [InlineData("# Name, Rarity", "sample1, 5", "sample2,10")]
        public void TestAllRare(params string[] sampleList)
        {
            Action samplesLoad = () => this.samplesParser.LoadSamples("test", sampleList);
            var ex = Record.Exception(samplesLoad);
            Assert.NotNull(ex);
            Assert.Contains("at least one", ex.Message);
        }

        [Fact]
        public void TestPickMale()
        {
            string sampleData = @"
# Title, Gender, Rarity
<null>,,5
Mr.,M
Mrs.,F
Miss,F
Ms.,F
Dr.,,10
Prof.,,5
Lord,M,2
Lady,F,2
            ";

            Samples samples = this.samplesParser.LoadSamples("test", sampleData.Split("\r\n"));
            Assert.NotNull(samples);

            var validList = new List<Sample.Genders>() { Sample.Genders.MALE, Sample.Genders.NEUTRAL };

            // Pick 10 male samples at random
            for (int i = 0; i < 10; i++)
            {
                Sample sample = samples.Pick(Sample.Genders.MALE);
                Assert.Contains(sample.Gender, validList);
            }
        }

        [Fact]
        public void TestPickFemale()
        {
            string sampleData = @"
# Title, Gender, Rarity
<null>,,5
Mr.,M
Mrs.,F
Miss,F
Ms.,F
Dr.,,10
Prof.,,5
Lord,M,2
Lady,F,2
            ";

            Samples samples = this.samplesParser.LoadSamples("test", sampleData.Split("\r\n"));
            Assert.NotNull(samples);

            var validList = new List<Sample.Genders>() { Sample.Genders.FEMALE, Sample.Genders.NEUTRAL };

            // Pick 10 male samples at random
            for (int i = 0; i < 10; i++)
            {
                Sample sample = samples.Pick(Sample.Genders.FEMALE);
                Assert.Contains(sample.Gender, validList);
            }
        }

        [Fact]
        public void TestPickAny()
        {
            string sampleData = @"
# Title, Gender, Rarity
<null>,,5
Mr.,M
Mrs.,F
Miss,F
Ms.,F
Dr.,,10
Prof.,,5
Lord,M,2
Lady,F,2
            ";

            Samples samples = this.samplesParser.LoadSamples("test", sampleData.Split("\r\n"));
            Assert.NotNull(samples);

            // Pick 10 samples at random
            for (int i = 0; i < 10; i++)
            {
                Sample sample = samples.Pick();
                Assert.NotNull(sample);
            }
        }

        [Fact]
        public void TestPickNoGender()
        {
            string sampleData = @"
# Title, Rarity
<null>,5
Dr.,10
Prof.
            ";

            Samples samples = this.samplesParser.LoadSamples("test", sampleData.Split("\r\n"));
            Assert.NotNull(samples);

            // Pick 10 samples at random
            for (int i = 0; i < 10; i++)
            {
                Sample sample = samples.Pick();
                Assert.Equal(Sample.Genders.NEUTRAL, sample.Gender);
            }
        }

        [Fact]
        public void TestPickBadGender()
        {
            string sampleData = @"
<null>
Dr.
Prof.
            ";

            Samples samples = this.samplesParser.LoadSamples("test", sampleData.Split("\r\n"));
            Assert.NotNull(samples);

            Action pickSample = () => samples.Pick(Sample.Genders.MALE);
            var ex = Record.Exception(pickSample);
            Assert.NotNull(ex);
            Assert.Contains("un-gendered samples", ex.Message);
        }
    }
}
