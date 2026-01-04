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

using System.Diagnostics.CodeAnalysis;
using CommandLine;

namespace SysrootGenerator
{
	[SuppressMessage("ReSharper", "UnusedAutoPropertyAccessor.Global", Justification = "Used by CommandLineParser")]
	public class CommandLineOptions
	{
		[Option("arch", SetName = "CLI", HelpText = "The debian based target architecture. Default: amd64. See: https://wiki.debian.org/Multiarch/Tuples#Architectures_in_Debian")]
		public string? Arch { get; set; }

		[Option("path", SetName = "CLI", HelpText = "The path to the directory where the sysroot will be generated.")]
		public string? Path { get; set; }

		[Option("distribution", SetName = "CLI", HelpText = "The distribution name (e.g., bookworm, focal).")]
		public string? Distribution { get; set; }

		[Option("cache-path", SetName = "CLI", HelpText = "Path for downloaded packages and metadata. Defaults to: <path>/tmp.")]
		public string? CachePath { get; set; }

		[Option("packages", SetName = "CLI", HelpText = "Comma-separated list of packages to install.", Separator = ',')]
		public IEnumerable<string>? Packages { get; set; }

		[Option("banned-packages", SetName = "CLI", HelpText = "Comma-separated list of packages to ban from installation.", Separator = ',')]
		public IEnumerable<string>? BannedPackages { get; set; }

		[Option("no-default-banned-packages", HelpText = "Do not bypass default banned packages.")]
		public bool NoDefaultBannedPackages { get; set; }

		[Option("sources", SetName = "CLI", HelpText = "Comma-separated list of sources to download packages from. Each source is defined as 'uri|component1,component2'.")]
		public string? Sources { get; set; }

		[Option('v', "verbose", HelpText = "Enable verbose output.")]
		public bool Verbose { get; set; }

		[Option("purge", SetName = "CLI", HelpText = "Purge existing sysroot.")]
		public bool Purge { get; set; }

		[Option("purge-cache", SetName = "CLI", HelpText = "Purge existing caches.")]
		public bool PurgeCache { get; set; }

		[Option("no-usr-merge", SetName = "CLI", HelpText = "Do not merge usr directory to root (/bin to point to /usr/bin, etc.).")]
		public bool NoUsrMerge { get; set; }

		[Option("no-bins", SetName = "CLI", HelpText = "Do not install binaries.")]
		public bool NoBins { get; set; }

		[Option("no-dependencies", SetName = "CLI", HelpText = "Do not install dependencies.")]
		public bool NoDependencies { get; set; }

		[Option("http-timeout", SetName = "CLI", HelpText = "Http timeout in milliseconds. Default: 100.", Default = 100)]
		public int HttpTimeout { get; set; }

		[Option("store-install-state", SetName = "CLI", HelpText = "Store install state in a file.")]
		public bool StoreInstallState { get; set; }

		[Option("config-file", SetName = "File", HelpText = "Path to the configuration file.")]
		public string? ConfigFile { get; set; }
	}
}