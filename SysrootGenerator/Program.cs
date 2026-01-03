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

using System.Net;
using System.Security.Cryptography;

namespace SysrootGenerator
{
	internal static class Program
	{
		private static readonly HttpClientHandler Handler = new();

		private static readonly string[] DefaultBannedPackages =
		[
			"linux-base", "linux-image-", "linux-headers-", "linux-modules-", "linux-firmware"
		];

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

				var targetDir = configuration!.Path;

				if (configuration.Purge && Directory.Exists(targetDir))
				{
					Directory.Delete(targetDir, true);
				}

				Directory.CreateDirectory(targetDir!);

				if (configuration.PurgeCache && Directory.Exists(configuration.CachePath))
				{
					Directory.Delete(configuration.CachePath, true);
				}

				var bannedPackages = configuration.BannedPackages?.ToList() ?? [];

				if (!configuration.NoDefaultBannedPackages)
				{
					bannedPackages.AddRange(DefaultBannedPackages);
				}

				var packagesToInstall = GetPackagesToInstall(configuration!, packages, bannedPackages);

				InstallPackages(configuration, packagesToInstall.ToArray());

				if (configuration.NoBins)
				{
					DeleteBins(configuration!);
				}

				if (!configuration.NoUsrMerge)
				{
					MergeUsr(configuration!);
				}
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
			Console.WriteLine("  --config-file=<path>          Path to a JSON configuration file (will ignore rest of arguments).");
			Console.WriteLine("  --path=<path>                 The target directory for the sysroot.");
			Console.WriteLine("  --arch=<arch>                 The target architecture (e.g., amd64, armhf). Default: amd64.");
			Console.WriteLine("  --distribution=<dist>         The distribution name (e.g., bookworm, focal).");
			Console.WriteLine("  --cache-path=<path>           Path for downloaded packages and metadata. Default: <path>/tmp.");
			Console.WriteLine("  --packages=<pkg1,...>         Comma-separated list of packages to install.");
			Console.WriteLine("  --sources=\"<src1|comp1,comp2 ...>\" ");
			Console.WriteLine("                                Space-separated list of sources in format: 'uri|component1,component2'.");
			Console.WriteLine("  --verbose                     Enable verbose output.");
			Console.WriteLine("  --purge                       Purge existing sysroot.");
			Console.WriteLine("  --purge-cache                 Purge existing caches.");
			Console.WriteLine("  --http-timeout=<seconds>      The timeout for HTTP requests. Default: 100 seconds.");
			Console.WriteLine("  --banned-packages=<pkg1,...>  Comma-separated list of packages to ban from installation.");
			Console.WriteLine("  --no-default-banned-packages  Do not bypass default banned packages.");
			Console.WriteLine("  --no-usr-merge                Do not merge usr directory to root (/bin to point to /usr/bin, etc.).");
			Console.WriteLine("  --no-bins                     Remove binary directories.");
			Console.WriteLine("  --no-dependencies             Do not resolve and install dependencies.");
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

			var timeout = TimeSpan.FromSeconds(config.HttpTimeout);
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
				DownloadIfNotExist(uri, debPath, timeout);

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
			string baseArchitecture,
			Package package,
			IReadOnlyDictionary<string, Package> availablePackages,
			Dictionary<string, Package> packagesToInstall,
			HashSet<string> missingPackages,
			IReadOnlyCollection<string> bannedPackages)
		{
			packagesToInstall.TryAdd(package.Id, package);

			foreach (var dependency in package.Depends)
			{
				if (bannedPackages.Any(p => dependency.StartsWith(p)))
				{
					Logger.Verbose($"Package dependency '{dependency}' is banned.");
					continue;
				}

				var packageKey = $"{dependency}:{baseArchitecture}";
				if (!availablePackages.TryGetValue(packageKey, out var dependentPackage))
				{
					packageKey = $"{dependency}:all";

					if (!availablePackages.TryGetValue(packageKey, out dependentPackage))
					{
						missingPackages.Add(dependency);
						continue;
					}
				}

				if (!packagesToInstall.TryAdd(dependentPackage.Id, dependentPackage))
				{
					continue;
				}

				ResolveDependencies(baseArchitecture, dependentPackage, availablePackages, packagesToInstall, missingPackages, bannedPackages);
			}
		}

		private static IEnumerable<Package> GetPackagesToInstall(
			Configuration config,
			IReadOnlyDictionary<string, Package> packages,
			IReadOnlyCollection<string> bannedPackages)
		{
			var packagesToInstall = new Dictionary<string, Package>();
			var missingPackages = new HashSet<string>();

			foreach (var packageName in config.Packages!)
			{
				var packageKey = $"{packageName}:{config.Arch}";
				if (!packages.TryGetValue(packageKey, out var dependentPackage))
				{
					packageKey = $"{packageName}:all";

					if (!packages.TryGetValue(packageKey, out dependentPackage))
					{
						throw new Exception($"Dependency '{packageKey}' not found");
					}
				}

				if (config.NoDependencies)
				{
					packagesToInstall.TryAdd(dependentPackage.Id, dependentPackage);
					continue;
				}

				ResolveDependencies(config.Arch!, dependentPackage, packages, packagesToInstall, missingPackages, bannedPackages);
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
							Logger.Info(
								$"Additional Package '{extraPackage}' needs to be installed because it provides dependency for '{missingPackage}'");
							ResolveDependencies(
								config.Arch!,
								extraPackage,
								packages,
								packagesToInstall,
								newMissingPackages,
								bannedPackages);

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

		private static void DownloadIfNotExist(Uri uri, string path, TimeSpan timeout)
		{
			if (File.Exists(path))
			{
				Logger.Verbose($"File '{path}' is cached. Not downloading.");
				return;
			}

			Logger.Verbose($"Downloading '{uri}' to '{path}'");
			using var client = new HttpClient(Handler, false);
			client.Timeout = timeout;

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

			var timeout = TimeSpan.FromSeconds(config.HttpTimeout);

			var packages = new List<Package>();

			foreach (var source in config.Sources!)
			{
				var baseUri = new Uri(source.Uri!.TrimEnd('/'));

				foreach (var section in source.Components!)
				{
					var uri = new Uri(
						$"{baseUri}/dists/{config.Distribution}/{section}/binary-{config.Arch}/Packages.gz");
					var targetPath = Path.Combine(databasesPath, $"{config.Distribution}-{section}-{config.Arch}.gz");

					try
					{
						DownloadIfNotExist(uri, targetPath, timeout);
					}
					catch (HttpRequestException e) when (e.StatusCode == HttpStatusCode.NotFound)
					{
						Logger.Warning($"Source '{source.Uri}' does not contain section '{section}' for '{config.Arch}'. Skipping.");
						continue;
					}

					packages.AddRange(DatabaseParser.GetPackagesFromDatabaseFile(baseUri, targetPath).ToArray());
				}
			}

			var dictionary = new Dictionary<string, Package>();

			foreach (var package in packages)
			{
				if (!dictionary.TryAdd(package.Id, package))
				{
					Logger.Warning($"Package '{package.Id}' already exists in database. Skipping.");
				}
			}

			return dictionary;
		}

		private static void MoveDirectory(FileInfo source, string target)
		{
			var files = Directory.GetFileSystemEntries(source.FullName);

			foreach (var file in files)
			{
				var fileInfo = new FileInfo(file);

				if (fileInfo.Attributes.HasFlag(FileAttributes.ReparsePoint))
				{
					File.CreateSymbolicLink(Path.Combine(target, fileInfo.Name), fileInfo.LinkTarget!);
					continue;
				}

				if (fileInfo.Attributes.HasFlag(FileAttributes.Normal))
				{
					fileInfo.MoveTo(Path.Combine(target, fileInfo.Name));
					continue;
				}

				if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
				{
					Directory.CreateDirectory(Path.Combine(target, fileInfo.Name));
					MoveDirectory(fileInfo, Path.Combine(target, fileInfo.Name));
				}
			}
		}

		private static void MergeDirectory(string rootPath, string directoryName, string targetDirectory)
		{
			if (!Directory.Exists(Path.Combine(rootPath, targetDirectory)))
			{
				return;
			}

			var originalPath = Path.Combine(rootPath, directoryName);

			if (Directory.Exists(originalPath))
			{
				MoveDirectory(new FileInfo(originalPath), Path.Combine(rootPath, targetDirectory));
				Directory.Delete(originalPath, true);
			}

			File.CreateSymbolicLink(originalPath, targetDirectory);
		}

		private static void DeleteBins(Configuration config)
		{
			if (config.Path == "/")
			{
				Logger.Warning("Will not remove 'bin' directories from root ('/') directory.");
				return;
			}

			var directories = Directory.GetDirectories(config.Path!, "*bin", SearchOption.AllDirectories);

			foreach (var directory in directories)
			{
				if (directory.EndsWith("/bin") || directory.EndsWith("/sbin"))
				{
					DeleteDirectory(directory);
				}
			}
		}

		private static void DeleteDirectory(string path)
		{
			if (Directory.Exists(path))
			{
				Directory.Delete(path, true);
			}
		}

		private static void MergeUsr(Configuration config)
		{
			MergeDirectory(config.Path!, "bin", "usr/bin");
			MergeDirectory(config.Path!, "sbin", "usr/sbin");
			MergeDirectory(config.Path!, "lib", "usr/lib");
			MergeDirectory(config.Path!, "include", "usr/include");
			var lib64Path = Path.Combine(config.Path!, "usr", "lib64");

			if (Directory.Exists(lib64Path))
			{
				MergeDirectory(config.Path!, "lib64", "usr/lib64");
			}
		}
	}
}