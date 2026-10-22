using System.Globalization;
using System.Threading;
using LvlUp.RemoteConfig;
using LvlUp.Services;
using NUnit.Framework;

namespace LvlUp.Tests
{
    /// <summary>
    /// Remote config values must parse the same on every device locale. tr-TR and de-DE use "." as the
    /// thousands separator, so a culture-sensitive parse turns "0.05" into 5.
    /// </summary>
    public class RemoteConfigCultureTests
    {
        private CultureInfo _savedCulture;
        private CultureInfo _savedUICulture;

        [SetUp]
        public void SetUp()
        {
            _savedCulture = Thread.CurrentThread.CurrentCulture;
            _savedUICulture = Thread.CurrentThread.CurrentUICulture;
        }

        [TearDown]
        public void TearDown()
        {
            Thread.CurrentThread.CurrentCulture = _savedCulture;
            Thread.CurrentThread.CurrentUICulture = _savedUICulture;
        }

        private static void SetCulture(string name)
        {
            var culture = new CultureInfo(name);
            Thread.CurrentThread.CurrentCulture = culture;
            Thread.CurrentThread.CurrentUICulture = culture;
        }

        private static RemoteConfigService ServiceWith(params ConfigData[] configs)
        {
            var service = new RemoteConfigService();
            service.Initialize(null);
            service.ProcessConfigsResponse(new ConfigsResponse { configs = configs }, isFromCache: true);
            return service;
        }

        [TestCase("tr-TR")]
        [TestCase("de-DE")]
        [TestCase("en-US")]
        public void GetFloatParsesStringValueInvariantly(string culture)
        {
            SetCulture(culture);
            var service = ServiceWith(new ConfigData { key = "hit_push", value = "0.05", dataType = "number" });

            Assert.AreEqual(0.05f, service.GetFloat("hit_push"), 1e-7f);
        }

        [TestCase("tr-TR")]
        [TestCase("de-DE")]
        [TestCase("en-US")]
        public void GetFloatParsesServerJsonNumberInvariantly(string culture)
        {
            SetCulture(culture);
            // A JSON number in the server response lands in ConfigData.value as a boxed double, and
            // GetValueAsString serializes it back to text before GetFloat parses it.
            var service = ServiceWith(
                new ConfigData { key = "hit_push", value = 0.05d, dataType = "number" },
                new ConfigData { key = "big", value = 1234.5d, dataType = "number" });

            Assert.AreEqual(0.05f, service.GetFloat("hit_push"), 1e-7f);
            Assert.AreEqual(1234.5f, service.GetFloat("big"), 1e-3f);
        }

        [TestCase("tr-TR")]
        [TestCase("de-DE")]
        public void GetIntAndGetBoolParseInvariantly(string culture)
        {
            SetCulture(culture);
            var service = ServiceWith(
                new ConfigData { key = "lives", value = "-3", dataType = "number" },
                new ConfigData { key = "flag", value = "1", dataType = "number" });

            Assert.AreEqual(-3, service.GetInt("lives"));
            Assert.IsTrue(service.GetBool("flag"));
        }
    }
}
