// **********************************************************************
// 
// Sysroot generator
// 
// Copyright (C) 2025 Ioannis Panagiotopoulos
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// 
// **********************************************************************

using Newtonsoft.Json;
using SysrootGenerator;

namespace UnitTests
{
	public class ConfigurationUnitTests
	{
		[Test]
		public void ConfigurationFile_Valid_Success([Values] bool booleanValues)
		{
			var originalConfig = new Configuration
			{
				Arch = "armhf",
				Path = "/tmp/sysroot",
				Distribution = "focal",
				CachePath = "/tmp/cache",
				Packages = ["libc6-dev", "libssl-dev"],
				BannedPackages = ["linux-headers-dev", "linux-programs-dev", "linux-firmware"],
				NoDefaultBannedPackages = booleanValues,
				Sources =
				[
					new Source { Uri = "https://archive.ubuntu.com/ubuntu/", Components = ["main", "universe"] }
				],
				Purge = booleanValues,
				PurgeCache = booleanValues,
				NoUsrMerge = booleanValues,
				NoBins = booleanValues,
				NoDependencies = booleanValues,
				HttpTimeout = 4666,
				StoreInstallState = booleanValues
			};
			
			var filePath = Path.Combine(Directory.GetCurrentDirectory(), "config.json");
			File.WriteAllText(filePath, JsonConvert.SerializeObject(originalConfig));
			var result = Configuration.TryGetFromArgs([$"--config-file={filePath}"], out var config);
			Assert.That(result, Is.True);
			AssertAreEqual(originalConfig, config!);
		}

		private static void AssertAreEqual(Configuration originalConfig, Configuration config)
        {
            Assert.Multiple(() =>
            {
                Assert.That(config.Arch, Is.EqualTo(originalConfig.Arch));
                Assert.That(config.Path, Is.EqualTo(originalConfig.Path));
                Assert.That(config.Distribution, Is.EqualTo(originalConfig.Distribution));
                Assert.That(config.CachePath, Is.EqualTo(originalConfig.CachePath));
                Assert.That(config.Sources!, Has.Length.EqualTo(originalConfig.Sources!.Length));
            });
            for (var i = 0; i < originalConfig.Sources!.Length; i++)
            {
                Assert.Multiple(() =>
                {
                    Assert.That(config.Sources[i].Uri, Is.EqualTo(originalConfig.Sources[i].Uri));
                    Assert.That(config.Sources[i].Components!, Has.Length.EqualTo(originalConfig.Sources[i].Components!.Length));
                });
                for (var j = 0; j < originalConfig.Sources[i].Components!.Length; j++)
				{
					Assert.That(config.Sources[i].Components![j], Is.EqualTo(originalConfig.Sources[i].Components![j]));
				}
            }

            Assert.Multiple(() =>
            {
                Assert.That(config.Purge, Is.EqualTo(originalConfig.Purge));
                Assert.That(config.PurgeCache, Is.EqualTo(originalConfig.PurgeCache));
                Assert.That(config.HttpTimeout, Is.EqualTo(originalConfig.HttpTimeout));
                Assert.That(config.NoDefaultBannedPackages, Is.EqualTo(originalConfig.NoDefaultBannedPackages));
                Assert.That(config.BannedPackages!, Has.Length.EqualTo(originalConfig.BannedPackages!.Length));
            });
            for (var i = 0; i < originalConfig.BannedPackages!.Length; i++)
			{
				Assert.That(config.BannedPackages![i], Is.EqualTo(originalConfig.BannedPackages[i]));
			}

            Assert.Multiple(() =>
            {
                Assert.That(config.NoUsrMerge, Is.EqualTo(originalConfig.NoUsrMerge));
                Assert.That(config.NoDependencies, Is.EqualTo(originalConfig.NoDependencies));
                Assert.That(config.NoBins, Is.EqualTo(originalConfig.NoBins));
            });
        }

        [Test]
		[TestCase("--path")]
		[TestCase("--distribution")]
		[TestCase("--sources")]
		[TestCase("--packages")]
		public void CommandLineOptions_MissingRequired_ReturnsError(string arg)
		{
			var args = new List<string>
			{
				"--path=/tmp/sysroot",
				"--distribution=focal",
				"--sources=https://archive.ubuntu.com/ubuntu/|main,universe",
				"--packages=libc6-dev,libssl-dev"
			};

			args.RemoveAll(s => s.StartsWith(arg));
			var result = Configuration.TryGetFromArgs(args.ToArray(), out _);
			Assert.That(result, Is.False);
		}
		
		[Test]
		[TestCase("")]
		[TestCase("|")]
		[TestCase("|a")]
		[TestCase("a|b")]
		[TestCase("a://a.a|b c")]
		[TestCase("a://a.a|b |")]
		[TestCase("a://a.a|b |c")]
		[TestCase("a://a.a|b c|d")]
		[TestCase("a://a.a| c|d")]
		[TestCase("| a://a.a|d")]
		[TestCase("|b a://a.a|d")]
		public void CommandLineOptions_InvalidSource_ReturnsError(string source)
		{
			var args = new[]
			{
				"--arch=armhf",
				"--path=/tmp/sysroot",
				"--distribution=focal",
				"--sources",
				source,
				"--packages=libc6-dev,libssl-dev"
			};

			var result = Configuration.TryGetFromArgs(args, out _);
			Assert.That(result, Is.False);
		}
		
		[Test]
		[TestCase("a://a.a|b")]
		[TestCase("a://a.a|b,c")]
		[TestCase("a://a.a|b,c d://d.d|e")]
		[TestCase("a://a.a|b,c d://d.d|e,f")]
		[TestCase("a://a.a|b,c d://d.d|e,f g://g.g|h")]
		[TestCase("a://a.a|b,c d://d.d|e,f g://g.g|h,i")]
		public void CommandLineOptions_ValidSource_Success(string source)
		{
			var args = new[]
			{
				"--arch=amd64",
				"--path=/tmp/sysroot",
				"--distribution=focal",
				"--sources",
				source,
				"--packages=libc6-dev,libssl-dev"
			};

			var result = Configuration.TryGetFromArgs(args, out _);
			Assert.That(result, Is.True);
		}
		
		[Test]
		public void CommandLineOptions_SetBothFileAndArguments_ReturnsError()
		{
			var args = new[]
			{
				"--arch=amd64",
				"--path=/tmp/sysroot",
				"--distribution=focal",
				"--cache-path=/tmp/cache",
				"--sources=https://archive.ubuntu.com/ubuntu/|main,universe",
				"--packages=libc6-dev,libssl-dev",
				"--purge",
				"--purge-cache",
				"--http-timeout=465",
				"--verbose",
				"--no-default-banned-packages",
				"--banned-packages=linux-headers-dev,linux-programs-dev,linux-firmware",
				"--no-usr-merge",
				"--no-dependencies",
				"--no-bins",
				"--config-file=./config.json"
			};

			var result = Configuration.TryGetFromArgs(args, out _);
			Assert.That(result, Is.False);
		}
		
		[Test]
		public void CommandLineOptions_Booleans_NotSet_DefaultToFalse()
        {
            var args = new[]
			{
				"--arch=amd64",
				"--path=/tmp/sysroot",
				"--distribution=focal",
				"--cache-path=/tmp/cache",
				"--sources=https://archive.ubuntu.com/ubuntu/|main,universe",
				"--packages=libc6-dev,libssl-dev",
				"--http-timeout=465",
				"--banned-packages=linux-headers-dev,linux-programs-dev,linux-firmware",
			};

			var result = Configuration.TryGetFromArgs(args, out var config);
            Assert.Multiple(() =>
            {
                Assert.That(result, Is.True);
                Assert.That(config!.Purge, Is.False);
                Assert.That(config.PurgeCache, Is.False);
                Assert.That(Logger.EnableVerbose, Is.False);
                Assert.That(config.NoDefaultBannedPackages, Is.False);
                Assert.That(config.NoUsrMerge, Is.False);
                Assert.That(config.NoDependencies, Is.False);
                Assert.That(config.NoBins, Is.False);
            });
        }

        [Test]
		public void CommandLineOptions_Valid_Parsing()
		{
			var args = new[]
			{
				"--arch=amd64",
				"--path=/tmp/sysroot",
				"--distribution=focal",
				"--cache-path=/tmp/cache",
				"--sources=https://archive.ubuntu.com/ubuntu/|main,universe",
				"--packages=libc6-dev,libssl-dev",
				"--purge",
				"--purge-cache",
				"--http-timeout=465",
				"--verbose",
				"--no-default-banned-packages",
				"--banned-packages=linux-headers-dev,linux-programs-dev,linux-firmware",
				"--no-usr-merge",
				"--no-dependencies",
				"--no-bins"
			};

			var result = Configuration.TryGetFromArgs(args, out var config);
			Assert.That(result, Is.True);
			Assert.That(config!.Arch, Is.EqualTo("amd64"));
			Assert.That(config.Path, Is.EqualTo("/tmp/sysroot"));
			Assert.That(config.Distribution, Is.EqualTo("focal"));
			Assert.That(config.CachePath, Is.EqualTo("/tmp/cache"));
			Assert.That(config.Sources!.Length, Is.EqualTo(1));
			Assert.That(config.Sources[0].Uri, Is.EqualTo("https://archive.ubuntu.com/ubuntu/"));
			Assert.That(config.Sources[0].Components!.Length, Is.EqualTo(2));
			Assert.That(config.Sources[0].Components![0], Is.EqualTo("main"));
			Assert.That(config.Sources[0].Components![1], Is.EqualTo("universe"));
			Assert.That(config.Purge, Is.True);
			Assert.That(config.PurgeCache, Is.True);
			Assert.That(config.HttpTimeout, Is.EqualTo(465));
			Assert.That(Logger.EnableVerbose, Is.True);
			Assert.That(config.NoDefaultBannedPackages, Is.True);
			Assert.That(config.BannedPackages!.Length, Is.EqualTo(3));
			Assert.That(config.BannedPackages[0], Is.EqualTo("linux-headers-dev"));
			Assert.That(config.BannedPackages[1], Is.EqualTo("linux-programs-dev"));
			Assert.That(config.BannedPackages[2], Is.EqualTo("linux-firmware"));
			Assert.That(config.NoUsrMerge, Is.True);
			Assert.That(config.NoDependencies, Is.True);
			Assert.That(config.NoBins, Is.True);
		}
		
		[Test]
		public void CommandLineOptions_SpaceInvalid_ReturnError()
		{
			var args = new[]
			{
				"--arch",
				"armhf",
				"--path",
				"--distribution",
				"focal",
				"--cache-path",
				"/tmp/cache",
				"--sources",
				"https://archive.ubuntu.com/ubuntu/|main,universe",
				"--packages",
				"libc6-dev,libssl-dev",
				"--http-timeout",
				"465",
				"--banned-packages",
				"linux-headers-dev,linux-programs-dev,linux-firmware"
			};

			var result = Configuration.TryGetFromArgs(args, out _);
			Assert.That(result, Is.False);
		}
		
		[Test]
		public void CommandLineOptions_SpaceValid_Parsing()
		{
			var args = new[]
			{
				"--arch",
				"armhf",
				"--path",
				"/tmp/sysroot",
				"--distribution",
				"focal",
				"--cache-path",
				"/tmp/cache",
				"--sources",
				"https://archive.ubuntu.com/ubuntu/|main,universe",
				"--packages",
				"libc6-dev,libssl-dev",
				"--http-timeout",
				"465",
				"--banned-packages",
				"linux-headers-dev,linux-programs-dev,linux-firmware"
			};

			var result = Configuration.TryGetFromArgs(args, out var config);
			Assert.That(result, Is.True);
			Assert.That(config!.Arch, Is.EqualTo("armhf"));
			Assert.That(config.Path, Is.EqualTo("/tmp/sysroot"));
			Assert.That(config.Distribution, Is.EqualTo("focal"));
			Assert.That(config.CachePath, Is.EqualTo("/tmp/cache"));
			Assert.That(config.Sources!.Length, Is.EqualTo(1));
			Assert.That(config.Sources[0].Uri, Is.EqualTo("https://archive.ubuntu.com/ubuntu/"));
			Assert.That(config.Sources[0].Components!.Length, Is.EqualTo(2));
			Assert.That(config.Sources[0].Components![0], Is.EqualTo("main"));
			Assert.That(config.Sources[0].Components![1], Is.EqualTo("universe"));
			Assert.That(config.HttpTimeout, Is.EqualTo(465));
			Assert.That(config.BannedPackages!.Length, Is.EqualTo(3));
			Assert.That(config.BannedPackages[0], Is.EqualTo("linux-headers-dev"));
			Assert.That(config.BannedPackages[1], Is.EqualTo("linux-programs-dev"));
			Assert.That(config.BannedPackages[2], Is.EqualTo("linux-firmware"));
		}
	}
}