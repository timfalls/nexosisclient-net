﻿using System;
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
    public class ViewIntegrationTests : IDisposable
    {
        private readonly IntegrationTestFixture fixture;

        public ViewIntegrationTests(IntegrationTestFixture fixture)
        {
            this.fixture = fixture;
            var data = DataSetGenerator.Run(DateTime.Parse("2017-01-01"), DateTime.Parse("2017-03-31"), "xray");

            var result = fixture.Client.DataSets.Create("mike", data).Result;


        }

        [Fact]
        public async Task CanSaveView()
        {
            var view = new ViewInfo()
            {
                DataSetName = "mike"
            };

            var result = await fixture.Client.Views.Create("mikeView", view);

            Assert.Equal("mike", result.DataSetName);
            Assert.Equal("mikeView", result.ViewName);
        }


        [Fact]
        public async Task ListViews()
        {

            var view = new ViewInfo()
            {
                DataSetName = "mike"
            };

            var result = await fixture.Client.Views.Create("mikeView", view);

            var list = await fixture.Client.Views.List();

            Assert.True(list.Count > 0);
        }

        [Fact]
        public async Task CanRemoveView()
        {
            var id = Guid.NewGuid().ToString("N");

            var view = new ViewInfo()
            {
                DataSetName = "mike"
            };

            await fixture.Client.Views.Create(id, view);
            await fixture.Client.Views.Remove(id);

            var exception = await Assert.ThrowsAsync<NexosisClientException>(async () => await fixture.Client.DataSets.Get(id));

            Assert.Equal(HttpStatusCode.NotFound, exception.StatusCode);
        }




        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    fixture.Client.DataSets.Remove("mike", DataSetDeleteOptions.CascadeAll);
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion

    }
#endif
}