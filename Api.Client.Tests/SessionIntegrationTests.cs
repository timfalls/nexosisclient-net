using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Nexosis.Api.Client;
using Nexosis.Api.Client.Model;
using Xunit;

namespace Api.Client.Tests
{
#if !SKIP_INTEGRATION // integration tests will charge your account and require an API key in environment variables
    [Collection("Integration")]
    public class SessionIntegrationTests
    {
        private readonly IntegrationTestFixture fixture;
        private readonly string sensorFilePath = Path.Combine(new DirectoryInfo(AppContext.BaseDirectory).Parent.Parent.Parent.FullName, @"CsvFiles\sensor2.csv");

        public SessionIntegrationTests(IntegrationTestFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public async Task GetBalanceWillGiveItBack()
        {
            var actual = await fixture.Client.GetAccountBalance();
            Assert.NotNull(actual.Balance);
            Assert.Equal(0, actual.Cost.Amount);
            Assert.Equal("USD", actual.Balance.Currency);
        }

        [Fact]
        public async Task ForcastStartsNewSession()
        {
            var dataSetName = $"testDataSet-{DateTime.Now:s}";
            var dataSet = DataSetGenerator.Run(DateTime.Parse("2016-08-01"), DateTime.Parse("2017-03-26"), "instances");
            await fixture.Client.DataSets.Create(dataSetName, dataSet);

            var actual = await fixture.Client.Sessions.CreateForecast(dataSetName, "instances", DateTimeOffset.Parse("2017-03-26"), DateTimeOffset.Parse("2017-04-25"), ResultInterval.Day);
            Assert.NotNull(actual.SessionId);
        }

        [Fact]
        public async Task GivesBackForecastMatchingRequestedInterval()
        {
            var sessionId = fixture.SavedHourlySessionId;

            var results = await fixture.Client.Sessions.GetResults(sessionId);

            Assert.NotNull(results);
            var date1 = DateTimeOffset.Parse(results.Data[0]["timeStamp"]);
            var date2 = DateTimeOffset.Parse(results.Data[1]["timeStamp"]);
            Assert.Equal(1, (date2 - date1).Hours);
        }

        [Fact]
        public async Task StartImpactStartsNewSession()
        {
            var dataSetName = $"testDataSet-{DateTime.UtcNow:s}";
            var dataSet = DataSetGenerator.Run(DateTime.Parse("2016-08-01"), DateTime.Parse("2017-03-26"), "instances");
            await fixture.Client.DataSets.Create(dataSetName, dataSet);

            var actual = await fixture.Client.Sessions.CreateForecast(dataSetName, "instances", DateTimeOffset.Parse("2017-03-26"), DateTimeOffset.Parse("2017-04-25"), ResultInterval.Day);
            Assert.NotNull(actual.SessionId);
        }

        [Fact]
        public async Task GetSessionListHasItems()
        {
            var sessions = await fixture.Client.Sessions.List();
            Assert.True(sessions.Count > 0);
        }

        [Fact]
        public async Task GetSessionResultsHasResults()
        {
            var results = await fixture.Client.Sessions.GetResults(fixture.SavedSessionId);

            Assert.NotNull(results);
            Assert.Equal(fixture.SavedSessionId, results.SessionId);
            Assert.Equal(Status.Completed, results.Status);
        }

        [Fact]
        public async Task GetSessionResultsHasLinks()
        {
            var result = await fixture.Client.Sessions.GetResults(fixture.SavedSessionId);

            Assert.NotNull(result);
            Assert.Equal(2, result.Links.Count);
            Assert.Equal(new[] { "results", "data" }, result.Links.Select(l => l.Rel));
            Assert.Equal($"{fixture.Client.ConfiguredUrl}sessions/{fixture.SavedSessionId}/results", result.Links[0].Href);
            Assert.Equal($"{fixture.Client.ConfiguredUrl}data/{fixture.ForecastDataSetName}", result.Links[1].Href);
        }

        [Fact]
        public async Task GetSessionResultsWillWriteFile()
        {
            var filename = Path.Combine(AppContext.BaseDirectory, $"test-ouput-{DateTime.UtcNow:yyyyMMddhhmmss}.csv");
            try
            {
                using (var output = new StreamWriter(File.OpenWrite(filename)))
                {
                    await fixture.Client.Sessions.GetResults(fixture.SavedSessionId, output);
                }

                var results = File.ReadAllText(filename);

                Assert.True(results.Length > 0);
                Assert.StartsWith("time,instances", results);
            }
            finally
            {
                if (File.Exists(filename))
                    File.Delete(filename);
            }
        }

        [Fact]
        public async Task DeletingSessionThenQueryingReturns404()
        {
            var dataSetName = $"testDataSet-{DateTime.Now:s}";
            var dataSet = DataSetGenerator.Run(DateTime.Parse("2016-08-01"), DateTime.Parse("2017-03-26"), "instances");

            await fixture.Client.DataSets.Create(dataSetName, dataSet);

            var actual = await fixture.Client.Sessions.AnalyzeImpact(dataSetName, $"charlie-delta-{DateTime.UtcNow:s}", "instances", DateTimeOffset.Parse("2016-11-26"), DateTimeOffset.Parse("2016-12-25"), ResultInterval.Day);

            await fixture.Client.Sessions.Remove(actual.SessionId);
            var exception = await Assert.ThrowsAsync<NexosisClientException>(async () => await fixture.Client.Sessions.Get(actual.SessionId));

            Assert.Equal(exception.StatusCode, HttpStatusCode.NotFound);
        }

        [Fact]
        public async Task CheckingSessionStatusReturnsExpectedValue()
        {
            var dataSetName = $"testDataSet-{DateTime.Now:s}";
            var dataSet = DataSetGenerator.Run(DateTime.Parse("2016-08-01"), DateTime.Parse("2017-03-26"), "instances");

            await fixture.Client.DataSets.Create(dataSetName, dataSet);

            var actual = await fixture.Client.Sessions.EstimateImpact(dataSetName, $"charlie-delta-{DateTime.UtcNow:s}", "instances", DateTimeOffset.Parse("2016-11-26"), DateTimeOffset.Parse("2016-12-25"), ResultInterval.Day);

            var status = await fixture.Client.Sessions.GetStatus(actual.SessionId);

            Assert.Equal(actual.Status, status.Status);
        }

        [Fact]
        public async Task CanRemoveMultipleSessions()
        {
            var dataSetName = $"testDataSet-{DateTime.Now:s}";
            var dataSet = DataSetGenerator.Run(DateTime.Parse("2016-08-01"), DateTime.Parse("2017-03-26"), "instances");

            await fixture.Client.DataSets.Create(dataSetName, dataSet);

            var first = await fixture.Client.Sessions.AnalyzeImpact(dataSetName, "juliet-juliet-echo-1", "instances", DateTimeOffset.Parse("2016-11-26"), DateTimeOffset.Parse("2016-12-25"), ResultInterval.Day);
            var second = await fixture.Client.Sessions.AnalyzeImpact(dataSetName, "juliet-juliet-echo-2", "instances", DateTimeOffset.Parse("2016-11-26"), DateTimeOffset.Parse("2016-12-25"), ResultInterval.Day);

            await fixture.Client.Sessions.Remove(null, "juliet-juliet-echo-", SessionType.Impact);

            var exceptionTheFirst = await Assert.ThrowsAsync<NexosisClientException>(async () => await fixture.Client.Sessions.Get(first.SessionId));
            var exceptionTheSecond = await Assert.ThrowsAsync<NexosisClientException>(async () => await fixture.Client.Sessions.Get(second.SessionId));

            Assert.Equal(exceptionTheFirst.StatusCode, HttpStatusCode.NotFound);
            Assert.Equal(exceptionTheSecond.StatusCode, HttpStatusCode.NotFound);
        }

        [Fact(Skip = "Only run if changing the API key used.")]
        public async Task PopulateDataForTesting()
        {
            var dataSetName = $"testDataSet-{DateTime.Now:s}";
            var dataSet = DataSetGenerator.Run(DateTime.Parse("2016-08-01"), DateTime.Parse("2017-03-26"), "instances");

            await fixture.Client.DataSets.Create(dataSetName, dataSet);
            var session = await fixture.Client.Sessions.CreateForecast(dataSetName, "instances", DateTimeOffset.Parse("2017-03-26"), DateTimeOffset.Parse("2017-04-25"), ResultInterval.Day);

            Console.WriteLine($"{session.SessionId}, {session.DataSetName}");
            using (var file = File.OpenText(sensorFilePath))
            {
                var fileDataSetName = $"fileDataSet-{DateTime.Now:s}";
                await fixture.Client.DataSets.Create(fileDataSetName, file);
                var actual = await fixture.Client.Sessions.CreateForecast(fileDataSetName, "value", DateTimeOffset.Parse("2017-01-10 -0:00"), DateTimeOffset.Parse("2017-01-17 -0:00"), ResultInterval.Hour);
                Assert.NotNull(actual.SessionId);
                Console.WriteLine($"Hourly session: {actual.SessionId}");
            }
        }

    }
#endif
}
