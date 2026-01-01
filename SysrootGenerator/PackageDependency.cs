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
	public class PackageDependency
	{
		public PackageDependency(string name, string architecture, PackageVersion version, VersionConstrain constrain)
		{
			Name = name;
			Architecture = architecture;
			Version = version;
			Constrain = constrain;
		}

		public bool IsSatisfiedBy(Package package)
		{
			if (Name != package.Name)
			{
				return false;
			}

			if (Architecture != package.Architecture)
			{
				return false;
			}

			switch (Constrain)
			{
				case VersionConstrain.NoConstraint:
					return true;
				case VersionConstrain.Equal:
					return package.Version.CompareTo(Version) == 0;
				case VersionConstrain.Greater:
					return package.Version.CompareTo(Version) > 0;
				case VersionConstrain.EqualOrGreater:
					return package.Version.CompareTo(Version) >= 0;
				case VersionConstrain.Less:
					return package.Version.CompareTo(Version) < 0;
				case VersionConstrain.EqualOrLess:
					return package.Version.CompareTo(Version) <= 0;
				default:
					throw new ArgumentOutOfRangeException();
			}
		}

		public string Name { get; }

		public string Architecture { get; }

		public PackageVersion Version { get; }

		public VersionConstrain Constrain { get; }
	}
}