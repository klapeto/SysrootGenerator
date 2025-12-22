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
using Microsoft.Extensions.Configuration;

namespace SysrootGenerator
{
	public class Configuration
	{
		public string? Arch { get; set; }

		public string? Path { get; set; }

		public string? Distribution { get; set; }

		public string? CachePath { get; set; }

		public string[]? Packages { get; set; }

		public Source[]? Sources { get; set; }

		public static bool TryGetFromArgs(string[] args, out Configuration? config)
		{
			IConfiguration rootConfig = new ConfigurationBuilder()
				.AddCommandLine(args)
				.Build();

			var configValue = rootConfig.GetSection("config-file");

			Configuration? draftConfig;

			if (!string.IsNullOrEmpty(configValue.Value))
			{
				using var stream = File.OpenRead(configValue.Value);
				draftConfig = JsonSerializer.Deserialize<Configuration>(
					stream,
					new JsonSerializerOptions
					{
						PropertyNameCaseInsensitive = true
					});
			}
			else
			{
				draftConfig = new Configuration
				{
					Path = rootConfig.GetSection("path").Value,
					Arch = rootConfig.GetSection("arch").Value,
					CachePath = rootConfig.GetSection("cache-path").Value,
					Distribution = rootConfig.GetSection("distribution").Value,
					Packages = rootConfig.GetSection("packages").Value?.Split(','),
					Sources = ParseSources(rootConfig.GetSection("sources").Value).ToArray()
				};
			}

			Logger.EnableVerbose = !string.IsNullOrEmpty(rootConfig.GetSection("verbose").Value);

			if (ValidateConfig(draftConfig))
			{
				config = draftConfig;
				return true;
			}

			config = null;
			return false;
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

		private static bool ValidateConfig(Configuration? config)
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
				Logger.Warning("Arch configuration file is empty. Using default arch (amd64)");
				config.Arch = "amd64";
			}

			if (config.Packages == null || config.Packages.Length == 0)
			{
				Logger.Error("Packages configuration file is empty.");
				return false;
			}

			if (config.Sources == null || config.Sources.Length == 0)
			{
				Logger.Error("Sources configuration file is empty.");
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
					Logger.Error("Sections configuration file is empty. Using default section (main)");
					source.Components = ["main"];
				}
			}

			return true;
		}
	}
}