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

namespace SysrootGenerator
{
	public class PackageVersion
	{
		public PackageVersion(int epoch, string upstreamVersion, string debianRevision)
		{
			Epoch = epoch;
			UpstreamVersion = upstreamVersion;
			DebianRevision = debianRevision;
		}

		public int Epoch { get; }

		public string UpstreamVersion { get; }

		public string DebianRevision { get; }

		public static PackageVersion Parse(string version)
		{
			var epoch = 0;
			var upstreamVersion = string.Empty;
			var debianRevision = string.Empty;

			for (var i = 0; i < version.Length; i++)
			{
				var c = version[i];
				var nextChar = i + 1 < version.Length ? version[i + 1] : 0;

				if (c == ':')
				{
					epoch = int.Parse(version[..1]);
					continue;
				}

				if (c == '-' && nextChar != '-')
				{
					upstreamVersion = version[1..i];
					debianRevision = version[(i + 1)..];
					break;
				}
			}

			return new PackageVersion(epoch, upstreamVersion, debianRevision);
		}

		public int CompareTo(PackageVersion other)
		{
			if (Epoch > other.Epoch)
			{
				return 1;
			}

			if (Epoch < other.Epoch)
			{
				return -1;
			}

			var comparer = new VersionComparer();
			var upstreamComparison = comparer.Compare(UpstreamVersion, other.UpstreamVersion);

			return upstreamComparison != 0 ? upstreamComparison : comparer.Compare(DebianRevision, other.DebianRevision);
		}

		public override string ToString()
		{
			return $"{Epoch}:{UpstreamVersion}-{DebianRevision}";
		}
	}
}