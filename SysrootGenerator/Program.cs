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

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Threading;

namespace SysrootGenerator
{
	internal static class Program
	{
		private static readonly HttpClientHandler Handler = new();

		public static int Main(string[] args)
		{
			try
			{
				var path = args.Length > 0 ? args[0] : Path.Combine(Directory.GetCurrentDirectory(), "sysroot.json");

				if (!File.Exists(path))
				{
					Logger.Error($"File not found: {path}");
					return 1;
				}

				if (!Configuration.TryGetFromFile(path, out var configuration))
				{
					return 1;
				}

				var packages = AptUpdate(configuration!);

				var packagesToInstall = GetPackagesToInstall(configuration!, packages);

				var targetDir = configuration!.Path;

				if (Directory.Exists(targetDir))
				{
					Directory.Delete(targetDir, true);
				}

				Directory.CreateDirectory(targetDir);

				InstallPackages(configuration!, packagesToInstall);
				CreateSymbolicLinks(configuration!);
			}
			catch (Exception ex)
			{
				Logger.Error("Error: ", ex);
				return 1;
			}

			return 0;
		}

		private static void InstallPackages(Configuration config, IEnumerable<Package> packages)
		{
			var packagesPath = Path.Combine(config.CachePath!, "packages");
			Directory.CreateDirectory(packagesPath);

			var tmpDir = Path.Combine(config.CachePath!, "tmp");
			Directory.CreateDirectory(tmpDir);

			var tarFile = Path.Combine(tmpDir, "data.tar");

			using var md5 = MD5.Create();

			foreach (var package in packages)
			{
				Logger.Info($"Installing package: {package.Name}");
				var uri = new Uri(package.Uri);
				var debPath = Path.Combine(packagesPath, $"{uri.Segments.Last()}");
				DownloadIfNotExist(uri, debPath);

				using (var debStream = File.OpenRead(debPath))
				{
					var hash = BitConverter.ToString(md5.ComputeHash(debStream)).Replace("-", string.Empty)
						.ToLowerInvariant();

					if (!hash.Equals(package.Md5Sum))
					{
						throw new Exception(
							$"Package '{package.Name}' checksum mismatch: {hash}, expected: {package.Md5Sum}");
					}
				}

				ArchiveHelpers.ExtractAr(debPath, tmpDir);
				var file = Directory.GetFiles(tmpDir).FirstOrDefault(f => Path.GetFileName(f).StartsWith("data.tar"));

				if (file == null)
				{
					throw new Exception($"Package '{package.Name}' could not find: data.xx.yy file");
				}

				if (file.EndsWith(".zst"))
				{
					ArchiveHelpers.DecompressZstd(file, tarFile);
				}
				else if (file.EndsWith(".xz"))
				{
					ArchiveHelpers.DecompressXz(file, tarFile);
				}
				else if (file.EndsWith(".gz"))
				{
					ArchiveHelpers.DecompressGzip(file, tarFile);
				}

				ArchiveHelpers.ExtractTar(tarFile, config.Path!);
				Directory.GetFiles(tmpDir, "*", SearchOption.AllDirectories)
					.ToList()
					.ForEach(File.Delete);
			}
		}

		private static void ResolveDependencies(
			Package package,
			IReadOnlyDictionary<string, Package> availablePackages,
			Dictionary<string, Package> packagesToInstall)
		{
			packagesToInstall.TryAdd(package.Name, package);

			foreach (var dependency in package.Depends)
			{
				if (!availablePackages.TryGetValue(dependency, out var dependentPackage))
				{
					throw new Exception($"Dependency '{dependency}' not found (Needed by '{package.Name}')");
				}

				if (!packagesToInstall.TryAdd(dependentPackage.Name, dependentPackage))
				{
					continue;
				}

				ResolveDependencies(dependentPackage, availablePackages, packagesToInstall);
			}
		}

		private static IEnumerable<Package> GetPackagesToInstall(
			Configuration config,
			IReadOnlyDictionary<string, Package> packages)
		{
			var packagesToInstall = new Dictionary<string, Package>();

			foreach (var packageName in config.Packages!)
			{
				if (!packages.TryGetValue(packageName, out var dependentPackage))
				{
					throw new Exception($"Dependency {packageName} not found");
				}

				ResolveDependencies(dependentPackage, packages, packagesToInstall);
			}

			return packagesToInstall.Values;
		}

		private static void DownloadIfNotExist(Uri uri, string path)
		{
			if (File.Exists(path))
			{
				Logger.Info($"File '{path}' is cached. Not downloading.");
				return;
			}

			Logger.Info($"Downloading '{uri}' to '{path}'");
			using var client = new HttpClient(Handler, false);

			var response = client.GetAsync(uri).Result;
			response.EnsureSuccessStatusCode();

			using var stream = new FileStream(path, FileMode.Create, FileAccess.Write);
			response.Content.CopyTo(stream, null, CancellationToken.None);
		}

		private static IReadOnlyDictionary<string, Package> AptUpdate(Configuration config)
		{
			Logger.Info($"Building sysroot on: {config.Path}");

			Directory.CreateDirectory(config.Path!);

			var databasesPath = Path.Combine(config.CachePath!, "databases");

			Directory.CreateDirectory(databasesPath);

			var packages = new List<Package>();

			foreach (var source in config.Sources!)
			{
				var baseUri = new Uri(source.Uri!.TrimEnd('/'));

				foreach (var section in source.Components!)
				{
					var uri = new Uri(
						$"{baseUri}/dists/{config.Distribution}/{section}/binary-{config.Arch}/Packages.gz");
					var targetPath = Path.Combine(databasesPath, $"{config.Distribution}-{section}-{config.Arch}.gz");
					DownloadIfNotExist(uri, targetPath);
					packages.AddRange(DatabaseParser.GetPackagesFromDatabaseFile(baseUri, targetPath).ToArray());
				}
			}

			return packages.ToDictionary(k => k.Name, v => v);
		}

		private static void CreateSymbolicLinks(Configuration config)
		{
			CreateSymbolicLink(Path.Combine(config.Path!, "bin"), "usr/bin");
			CreateSymbolicLink(Path.Combine(config.Path!, "lib"), "usr/lib");
			CreateSymbolicLink(Path.Combine(config.Path!, "sbin"), "usr/sbin");
			var lib64Path = Path.Combine(config.Path!, "usr", "lib64");

			if (Directory.Exists(lib64Path))
			{
				CreateSymbolicLink(lib64Path, "usr/lib64");
			}
		}

		private static void CreateSymbolicLink(string sourcePath, string targetPath)
		{
			//var directory = Path.GetDirectoryName(targetPath);
			var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "ln", Arguments = $"-s \"{targetPath}\" \"{sourcePath}\"", RedirectStandardError = true,
				});

			var error = process!.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new Exception($"ln failed: '{sourcePath}' -> {targetPath}: {error}");
			}
		}
	}
}