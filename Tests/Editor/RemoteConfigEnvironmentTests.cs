using System;
using LvlUp.RemoteConfig;
using LvlUp.Services;
using NUnit.Framework;

namespace LvlUp.Tests
{
    public class RemoteConfigEnvironmentTests
    {
        private string _savedOverride;

        [SetUp]
        public void SetUp()
        {
            _savedOverride = LvlUpDebugSettings.EnvironmentOverride;
            LvlUpDebugSettings.ClearEnvironmentOverride();
        }

        [TearDown]
        public void TearDown()
        {
            LvlUpDebugSettings.EnvironmentOverride = _savedOverride;
        }

        [TestCase("development")]
        [TestCase("staging")]
        [TestCase("production")]
        [TestCase(null)]
        [TestCase("")]
        public void PlayerBuildsAlwaysResolveToProduction(string configured)
        {
            Assert.AreEqual("production", LvlUpManager.ResolveRemoteConfigEnvironment(configured, isEditor: false));
        }

        [Test]
        public void EditorUsesTheConfiguredEnvironment()
        {
            Assert.AreEqual("development", LvlUpManager.ResolveRemoteConfigEnvironment("development", isEditor: true));
            Assert.AreEqual("staging", LvlUpManager.ResolveRemoteConfigEnvironment(" staging ", isEditor: true));
        }

        [Test]
        public void EditorFallsBackToProductionWhenUnset()
        {
            Assert.AreEqual("production", LvlUpManager.ResolveRemoteConfigEnvironment(null, isEditor: true));
            Assert.AreEqual("production", LvlUpManager.ResolveRemoteConfigEnvironment("  ", isEditor: true));
        }

        [Test]
        public void ServiceUsesResolvedBuildEnvironment()
        {
            var service = new RemoteConfigService();
            service.Initialize(null, LvlUpManager.ResolveRemoteConfigEnvironment("development", isEditor: false));

            Assert.AreEqual("production", service.GetCurrentEnvironment());
        }

        [Test]
        public void EnvironmentOverrideStillWinsOverResolvedEnvironment()
        {
            var service = new RemoteConfigService();
            service.Initialize(null, LvlUpManager.ResolveRemoteConfigEnvironment("production", isEditor: false));

            LvlUpDebugSettings.EnvironmentOverride = "development";
            Assert.AreEqual("development", service.GetCurrentEnvironment());

            LvlUpDebugSettings.ClearEnvironmentOverride();
            Assert.AreEqual("production", service.GetCurrentEnvironment());
        }

        [Test]
        public void DevelopmentCacheIsNotServedForProduction()
        {
            var cache = new RemoteConfigCacheService("envtest_" + Guid.NewGuid().ToString("N"));
            try
            {
                var devResponse = new ConfigsResponse
                {
                    configs = new[] { new ConfigData { key = "k", value = "dev", dataType = "string" } }
                };
                cache.SaveConfigs(devResponse, "development");

                Assert.IsFalse(cache.TryLoadConfigs("production", out _));
                Assert.IsTrue(cache.TryLoadConfigs("development", out var loaded));
                Assert.AreEqual("dev", loaded.configs[0].value);
            }
            finally
            {
                cache.ClearCache();
            }
        }
    }
}
