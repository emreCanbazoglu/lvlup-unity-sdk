using System;
using System.Collections.Generic;
using System.IO;
using LvlUp.RemoteConfig;
using LvlUp.Services;
using NUnit.Framework;

namespace LvlUp.Tests
{
    public class RemoteConfigCacheFallbackTests
    {
        private const string FUNNEL_KEY = "level_funnel";
        private const string USER_ID = "user-a";

        private string _directory;
        private string _savedEnvironmentOverride;

        [SetUp]
        public void SetUp()
        {
            _directory = Path.Combine(Path.GetTempPath(), $"lvlup_config_cache_test_{Guid.NewGuid():N}");
            Directory.CreateDirectory(_directory);

            _savedEnvironmentOverride = LvlUpDebugSettings.EnvironmentOverride;
            LvlUpDebugSettings.ClearEnvironmentOverride();
        }

        [TearDown]
        public void TearDown()
        {
            LvlUpDebugSettings.EnvironmentOverride = _savedEnvironmentOverride;

            if (Directory.Exists(_directory))
                Directory.Delete(_directory, true);
        }

        [Test]
        public void CacheOlderThanFiveMinutesStillLoadsAndStaysOnDisk()
        {
            SaveFetchedConfigs(USER_ID, "funnel_lh1");
            AgeCacheFiles(TimeSpan.FromDays(3));

            RemoteConfigService service = CreateService(USER_ID);

            Assert.IsTrue(service.TryLoadCachedConfigs());
            Assert.AreEqual("funnel_lh1", service.GetString(FUNNEL_KEY));
            Assert.AreEqual(1, Directory.GetFiles(_directory).Length);
        }

        [Test]
        public void CachedLoadRestoresAbAssignments()
        {
            SaveFetchedConfigs(USER_ID, "funnel_lh1");

            RemoteConfigService service = CreateService(USER_ID);
            service.TryLoadCachedConfigs();

            Assert.IsTrue(service.HasLoadedConfigs);
            Assert.AreEqual("late_hard", service.GetCurrentAbTests()["late_hard_lh1"]);
        }

        [Test]
        public void WithoutCacheNothingLoads()
        {
            RemoteConfigService service = CreateService(USER_ID);

            Assert.IsFalse(service.TryLoadCachedConfigs());
            Assert.IsFalse(service.HasLoadedConfigs);
        }

        [Test]
        public void CacheNeverReplacesConfigsAlreadyLoaded()
        {
            SaveFetchedConfigs(USER_ID, "funnel_cached");

            RemoteConfigService service = CreateService(USER_ID);
            service.ProcessConfigsResponse(Response("funnel_live"), isFromCache: true);

            Assert.IsFalse(service.TryLoadCachedConfigs());
            Assert.AreEqual("funnel_live", service.GetString(FUNNEL_KEY));
        }

        [Test]
        public void CacheWrittenForAnotherUserIsIgnored()
        {
            SaveFetchedConfigs("user-b", "funnel_lh1");

            RemoteConfigService service = CreateService(USER_ID);

            Assert.IsFalse(service.TryLoadCachedConfigs());
            Assert.IsFalse(service.HasLoadedConfigs);
        }

        [Test]
        public void CacheWithoutUserIdLoads()
        {
            SaveFetchedConfigs(null, "funnel_lh1");

            RemoteConfigService service = CreateService(USER_ID);

            Assert.IsTrue(service.TryLoadCachedConfigs());
        }

        private void SaveFetchedConfigs(string userId, string funnel)
        {
            RemoteConfigService service = CreateService(userId);
            service.ProcessConfigsResponse(Response(funnel));
        }

        private RemoteConfigService CreateService(string userId)
        {
            var service = new RemoteConfigService(new RemoteConfigCacheService("default", _directory));
            service.Initialize(null);
            service.SetUserId(userId);
            return service;
        }

        private void AgeCacheFiles(TimeSpan age)
        {
            foreach (string file in Directory.GetFiles(_directory))
                File.SetLastWriteTimeUtc(file, DateTime.UtcNow - age);
        }

        private static ConfigsResponse Response(string funnel)
        {
            return new ConfigsResponse
            {
                configs = new[] { new ConfigData { key = FUNNEL_KEY, value = funnel, dataType = "string" } },
                abTests = new Dictionary<string, string> { { "late_hard_lh1", "late_hard" } }
            };
        }
    }
}
