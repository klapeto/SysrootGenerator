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

using System.Security.Cryptography;

namespace SysrootGenerator
{
	internal static class Program
	{
		private readonly static HttpClientHandler Handler = new();

		public static int Main(string[] args)
		{
			try
			{
				if (args.Contains("--help") || args.Contains("-h"))
				{
					PrintHelp();
					return 0;
				}

				if (!Configuration.TryGetFromArgs(args, out var configuration))
				{
					PrintHelp();
					return 1;
				}

				var packages = AptUpdate(configuration!);

				var packagesToInstall = GetPackagesToInstall(configuration!, packages);

				var targetDir = configuration!.Path;

				if (configuration.Purge && Directory.Exists(targetDir))
				{
					Directory.Delete(targetDir, true);
				}

				Directory.CreateDirectory(targetDir);

				InstallPackages(configuration, packagesToInstall.ToArray());
				CreateSymbolicLinks(configuration!);
			}
			catch (Exception ex)
			{
				Logger.Error("Error: ", ex);
				return 1;
			}

			return 0;
		}

		private static void PrintHelp()
		{
			Console.WriteLine("Sysroot Generator");
			Console.WriteLine("Usage: SysrootGenerator [options]");
			Console.WriteLine();
			Console.WriteLine("Options:");
			Console.WriteLine("  --verbose               Enable verbose output.");
			Console.WriteLine("  --purge                 Purge existing sysroot.");
			Console.WriteLine("  --config-file=<path>    Path to a JSON configuration file (will ignore rest of arguments).");
			Console.WriteLine("  --path=<path>           The target directory for the sysroot.");
			Console.WriteLine("  --arch=<arch>           The target architecture (e.g., amd64, armhf). Default: amd64.");
			Console.WriteLine("  --distribution=<dist>   The distribution name (e.g., bookworm, focal).");
			Console.WriteLine("  --cache-path=<path>     Path for downloaded packages and metadata. Default: <path>/tmp.");
			Console.WriteLine("  --packages=<pkg1,...>   Comma-separated list of packages to install.");
			Console.WriteLine("  --sources=<src1|comp1,comp2 ...> ");
			Console.WriteLine(
				"                          Space-separated list of sources in format: 'uri|component1,component2'");
			Console.WriteLine();
			Console.WriteLine("Examples:");
			Console.WriteLine("  SysrootGenerator --path=./sysroot --distribution=noble --packages=libc6-dev,libssl-dev --sources=\"https://archive.ubuntu.com/ubuntu/|main,universe\"");
			Console.WriteLine("  SysrootGenerator --config-file=./config.json");
		}

		private static void InstallPackages(Configuration config, Package[] packages)
		{
			var packagesPath = Path.Combine(config.CachePath!, "packages");
			Directory.CreateDirectory(packagesPath);

			var tmpDir = Path.Combine(config.CachePath!, "tmp");

			using var md5 = MD5.Create();

			var tarFile = Path.Combine(tmpDir, "data.tar");

			var i = 0;
			var total = packages.Length;
			foreach (var package in packages)
			{
				Logger.Info($"Installing package: {package.Name} ({i++}/{total})");

				if (Directory.Exists(tmpDir))
				{
					Directory.Delete(tmpDir, true);
				}

				Directory.CreateDirectory(tmpDir);

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
				var file = Directory.GetFiles(tmpDir, "*", SearchOption.AllDirectories)
					.FirstOrDefault(f => Path.GetFileName(f).StartsWith("data.tar"));

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
			}
		}

		private static void ResolveDependencies(
			Package package,
			IReadOnlyDictionary<string, Package> availablePackages,
			Dictionary<string, Package> packagesToInstall,
			HashSet<string> missingPackages)
		{
			packagesToInstall.TryAdd(package.Name, package);

			foreach (var dependency in package.Depends)
			{
				if (!availablePackages.TryGetValue(dependency, out var dependentPackage))
				{
					missingPackages.Add(dependency);
					continue;
				}

				if (!packagesToInstall.TryAdd(dependentPackage.Name, dependentPackage))
				{
					continue;
				}

				ResolveDependencies(dependentPackage, availablePackages, packagesToInstall, missingPackages);
			}
		}

		private static IEnumerable<Package> GetPackagesToInstall(
			Configuration config,
			IReadOnlyDictionary<string, Package> packages)
		{
			var packagesToInstall = new Dictionary<string, Package>();
			var missingPackages = new HashSet<string>();

			foreach (var packageName in config.Packages!)
			{
				if (!packages.TryGetValue(packageName, out var dependentPackage))
				{
					throw new Exception($"Dependency {packageName} not found");
				}

				ResolveDependencies(dependentPackage, packages, packagesToInstall, missingPackages);
			}

			while (missingPackages.Count > 0)
			{
				var newMissingPackages = new HashSet<string>();
				foreach (var missingPackage in missingPackages)
				{
					var providedBy = packagesToInstall.Values.FirstOrDefault(p => p.Provides.Contains(missingPackage));
					if (providedBy != null)
					{
						Logger.Info($"Package '{missingPackage}' is provided by '{providedBy.Name}'. Skipping.");
					}
					else
					{
						var extraPackage = packages.Values.FirstOrDefault(p => p.Provides.Contains(missingPackage));
						if (extraPackage != null)
						{
							Logger.Info($"Additional Package '{extraPackage.Name}' needs to be installed because it provides dependency for '{missingPackage}'");
							ResolveDependencies(extraPackage, packages, packagesToInstall, newMissingPackages);

							if (newMissingPackages.Contains(missingPackage))
							{
								throw new Exception($"Could not find: {missingPackage}");
							}
						}
					}
				}

				missingPackages = newMissingPackages;
			}

			return packagesToInstall.Values;
		}

		private static void DownloadIfNotExist(Uri uri, string path)
		{
			if (File.Exists(path))
			{
				Logger.Verbose($"File '{path}' is cached. Not downloading.");
				return;
			}

			Logger.Verbose($"Downloading '{uri}' to '{path}'");
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

			var dictionary = new Dictionary<string, Package>();

			foreach (var package in packages)
			{
				if (!dictionary.TryAdd(package.Name, package))
				{
					Logger.Warning($"Package '{package.Name}' already exists in database. Skipping.");
				}
			}

			return dictionary;
		}

		private static void CreateSymbolicLinkIfNotExisting(string path, string target)
		{
			if (!Directory.Exists(path))
			{
				File.CreateSymbolicLink(path, target);
			}
		}

		private static void CreateSymbolicLinks(Configuration config)
		{
			CreateSymbolicLinkIfNotExisting(Path.Combine(config.Path!, "bin"), "usr/bin");
			CreateSymbolicLinkIfNotExisting(Path.Combine(config.Path!, "lib"), "usr/lib");
			CreateSymbolicLinkIfNotExisting(Path.Combine(config.Path!, "sbin"), "usr/sbin");
			var lib64Path = Path.Combine(config.Path!, "usr", "lib64");
			if (Directory.Exists(lib64Path))
			{
				CreateSymbolicLinkIfNotExisting(Path.Combine(config.Path!, "lib64"), "usr/lib64");
			}
		}
	}
}