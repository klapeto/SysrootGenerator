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

using System.Collections.Generic;
using System.Linq;

namespace SysrootGenerator
{
	public class Package
	{
		public Package(string name, string uri, string md5Sum, string depends)
		{
			Name = name;
			Uri = uri;
			Md5Sum = md5Sum;
			Depends = ParseDependencies(depends).Select(s => s.Split(':').First()).ToArray();
		}

		public string Name { get; }

		public string Md5Sum { get; }

		public string[] Depends { get; }

		public string Uri { get; }

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
	}
}