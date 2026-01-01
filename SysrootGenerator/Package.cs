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
	public class Package
	{
		public Package(string name, string uri, string md5Sum, string depends, string provides, string architecture, PackageVersion version)
		{
			Name = name;
			Uri = uri;
			Md5Sum = md5Sum;
			Architecture = architecture;
			Version = version;
			Depends = ParseDependencies(depends).Select(s => s.Split(':').First()).ToArray();
			Provides = ParseDependencies(provides).Select(s => s.Split(':').First()).ToArray();
		}

		public string Name { get; }

		public string Md5Sum { get; }

		public PackageDependency[] Depends { get; }

		public string[] Provides { get; }

		public string Uri { get; }

		public string Architecture { get; }

		public PackageVersion Version { get; }

		public string Id => $"{Name}:{Architecture}";

		public override bool Equals(object? obj)
		{
			if (obj is null)
			{
				return false;
			}

			if (ReferenceEquals(this, obj))
			{
				return true;
			}

			if (obj.GetType() != GetType())
			{
				return false;
			}

			return Equals((Package)obj);
		}

		public override int GetHashCode()
		{
			return HashCode.Combine(Name, Md5Sum);
		}

		private static IEnumerable<string> ParseDependencies(string dependencies)
		{
			foreach (var dependency in dependencies.Split(','))
			{
				if (dependency.Contains('|'))
				{
					yield return dependency.Split('|').First().Trim();

					continue;
				}

				if (dependency.Contains('('))
				{
					yield return dependency.Split('(').First().Trim();
				}
				else
				{
					yield return dependency.Trim();
				}
			}
		}

		private bool Equals(Package other)
		{
			return Name == other.Name && Md5Sum == other.Md5Sum;
		}
	}
}