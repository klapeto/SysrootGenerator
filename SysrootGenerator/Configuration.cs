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

using System.Text.Json;
using System.Text.Json.Serialization;
using CommandLine;

namespace SysrootGenerator
{
	public class Configuration
	{
		public const int DefaultHttpTimeout = 100;

		[JsonPropertyName("arch")]
		public string? Arch { get; set; }

		[JsonPropertyName("path")]
		public string? Path { get; set; }

		[JsonPropertyName("distribution")]
		public string? Distribution { get; set; }

		[JsonPropertyName("cachePath")]
		public string? CachePath { get; set; }

		[JsonPropertyName("packages")]
		public string[]? Packages { get; set; }

		[JsonPropertyName("bannedPackages")]
		public string[]? BannedPackages { get; set; }

		[JsonPropertyName("noDefaultBannedPackages")]
		public bool NoDefaultBannedPackages { get; set; }

		[JsonPropertyName("sources")]
		public Source[]? Sources { get; set; }

		[JsonPropertyName("purge")]
		public bool Purge { get; set; }

		[JsonPropertyName("purgeCache")]
		public bool PurgeCache { get; set; }

		[JsonPropertyName("noUsrMerge")]
		public bool NoUsrMerge { get; set; }

		[JsonPropertyName("noBins")]
		public bool NoBins { get; set; }

		[JsonPropertyName("noDependencies")]
		public bool NoDependencies { get; set; }

		[JsonPropertyName("httpTimeout")]
		public int HttpTimeout { get; set; } = DefaultHttpTimeout;

		[JsonPropertyName("storeInstallState")]
		public bool StoreInstallState { get; set; }

		[JsonPropertyName("configFile")]
		public string? ConfigFile { get; set; }

		public static bool TryGetFromArgs(string[] args, out Configuration? config)
		{
			var parser = new Parser(with =>
			{
				with.AutoHelp = false;
				with.AutoVersion = false;
			});
			var parserResult = parser.ParseArguments<CommandLineOptions>(args);

			if (parserResult.Tag == ParserResultType.NotParsed)
			{
				config = null;

				// Logger.Error(string.Join(parserResult.Errors));
				return false;
			}

			var mappedConfig = parserResult.MapResult(FromCommandLineOptions, _ => new Configuration());

			Configuration? draftConfig;

			if (!string.IsNullOrEmpty(mappedConfig.ConfigFile))
			{
				using var stream = File.OpenRead(mappedConfig.ConfigFile);
				draftConfig = JsonSerializer.Deserialize<Configuration>(
					stream,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});

				if (draftConfig == null)
				{
					Logger.Error($"File is empty: '{mappedConfig.ConfigFile}'.");
					config = null;
					return false;
				}
			}
			else
			{
				draftConfig = mappedConfig;
			}

			Logger.EnableVerbose = parserResult.Value.Verbose;

			if (ValidateConfig(draftConfig))
			{
				config = draftConfig;
				return true;
			}

			config = null;
			return false;
		}

		public static bool ValidateConfig(Configuration? config)
		{
			if (config == null)
			{
				Logger.Error("Configuration is empty.");
				return false;
			}

			if (string.IsNullOrEmpty(config.Path))
			{
				Logger.Error("Path is empty.");
				return false;
			}

			config.Path = System.IO.Path.GetFullPath(config.Path);

			if (string.IsNullOrEmpty(config.CachePath))
			{
				config.CachePath = System.IO.Path.Combine(config.Path, "tmp");
				Logger.Warning($"Cache path is empty. Will use '{config.CachePath}'");
			}

			config.CachePath = System.IO.Path.GetFullPath(config.CachePath);

			if (string.IsNullOrEmpty(config.Distribution))
			{
				Logger.Error("Distribution is empty.");
				return false;
			}

			if (string.IsNullOrEmpty(config.Arch))
			{
				Logger.Warning("Arch configuration is empty. Using default arch (amd64)");
				config.Arch = "amd64";
			}

			if (config.HttpTimeout <= 0)
			{
				Logger.Error($"Http timeout is invalid: {config.HttpTimeout}. Must be greater than 0.");
				return false;
			}

			if (config.Packages == null || config.Packages.Length == 0)
			{
				Logger.Error("Packages configuration is empty.");
				return false;
			}

			if (config.Sources == null || config.Sources.Length == 0)
			{
				Logger.Error("Sources configuration is empty.");
				return false;
			}

			for (var index = 0; index < config.Sources.Length; index++)
			{
				var source = config.Sources[index];

				if (source.Uri == null)
				{
					Logger.Error($"Uri of {index}th source is not defined.");
					return false;
				}

				if (!Uri.TryCreate(source.Uri, UriKind.Absolute, out _))
				{
					Logger.Error($"Uri of {index}th source is not a valid absolute URI.");
					return false;
				}

				if (source.Components == null || source.Components.Length == 0)
				{
					Logger.Error("Components configuration is empty. Using default component (main)");
					source.Components = ["main"];
				}
			}

			return true;
		}

		private static Configuration FromCommandLineOptions(CommandLineOptions options)
		{
			return new Configuration
			{
				Arch = options.Arch,
				Path = options.Path,
				Distribution = options.Distribution,
				CachePath = options.CachePath,
				Packages = options.Packages?.ToArray(),
				BannedPackages = options.BannedPackages?.ToArray(),
				NoDefaultBannedPackages = options.NoDefaultBannedPackages,
				Sources = ParseSources(options.Sources).ToArray(),
				Purge = options.Purge,
				PurgeCache = options.PurgeCache,
				NoUsrMerge = options.NoUsrMerge,
				NoBins = options.NoBins,
				NoDependencies = options.NoDependencies,
				HttpTimeout = options.HttpTimeout,
				StoreInstallState = options.StoreInstallState,
				ConfigFile = options.ConfigFile
			};
		}

		private static IEnumerable<Source> ParseSources(string? args)
		{
			if (string.IsNullOrEmpty(args))
			{
				yield break;
			}

			foreach (var arg in args.Split(' '))
			{
				var parts = arg.Trim().Split('|');

				if (parts.Length != 1 && parts.Length != 2)
				{
					throw new ArgumentException(
						$"Invalid source argument: '{arg}'. It needs to be in format 'uri1|component1,component2'.");
				}

				var uri = parts.First();
				var components = parts.Length == 2 ? parts.Last().Split(',').Select(s => s.Trim()).ToArray() : null;
				yield return new Source
				{
					Uri = uri, Components = components
				};
			}
		}
	}
}