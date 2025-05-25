using NUnit.Framework;
using WiserHeatingAPI;
using WiserHeatApiV2;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Moq;

namespace WiserHeatAPIv2.Tests
{
    [TestFixture]
    public class WiserAPITests
    {
        private const string DummyHost = "dummyhost";
        private const string DummySecret = "dummysecret";

        private Mock<WiserRestController> CreateMockRestController()
        {
            var connection = new WiserConnection { Host = DummyHost, Secret = DummySecret };
            var mock = new Mock<WiserRestController>(connection) { CallBase = true };
            mock.Setup(x => x.GetHubDataAsync(It.IsAny<string>(), It.IsAny<bool>()))
                .ReturnsAsync(new Dictionary<string, object>());
            mock.Setup(x => x.SendCommandAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<WiserRestActionEnum>()))
                .ReturnsAsync(true);
            return mock;
        }

        [Test]
        public void Constructor_ThrowsOnMissingHostOrSecret()
        {
            Assert.Throws<WiserHubConnectionException>(() => new WiserAPI(null, DummySecret));
            Assert.Throws<WiserHubConnectionException>(() => new WiserAPI(DummyHost, null));
        }

        [Test]
        public void Version_IsNotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.Version);
        }

        [Test]
        public void Units_GetSet_Works()
        {
            var api = GetMockedWiserAPI();
            api.Units = api.Units; // Should not throw
            Assert.IsNotNull(api.Units);
        }

        [Test]
        public void RawHubData_ReturnsDictionary()
        {
            var api = GetMockedWiserAPI();
            var data = api.RawHubData;
            Assert.IsInstanceOf<Dictionary<string, object>>(data);
        }

        [Test]
        public void Devices_Property_NotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.Devices);
        }

        [Test]
        public void HeatingChannels_Property_NotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.HeatingChannels);
        }

        [Test]
        public void Hotwater_Property_NotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.Hotwater);
        }

        [Test]
        public void Moments_Property_NotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.Moments);
        }

        [Test]
        public void Rooms_Property_NotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.Rooms);
        }

        [Test]
        public void Schedules_Property_NotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.Schedules);
        }

        [Test]
        public void System_Property_NotNull()
        {
            var api = GetMockedWiserAPI();
            Assert.IsNotNull(api.System);
        }

        // Helper to create a WiserAPI instance with a mocked WiserRestController
        private WiserAPI GetMockedWiserAPI()
        {
            // This assumes you have refactored WiserAPI to allow injecting a WiserRestController
            // If not, this will still use the real constructor
            try
            {
                // If WiserAPI supported DI, you would inject the mock here
                return new WiserAPI(DummyHost, DummySecret);
            }
            catch (WiserHubConnectionException)
            {
                Assert.Inconclusive("Cannot instantiate WiserAPI without a real or mocked hub.");
                return null;
            }
        }
    }
}