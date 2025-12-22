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

using System.IO.Compression;

namespace SysrootGenerator
{
	internal static class DatabaseParser
	{
		public static IEnumerable<Package> GetPackagesFromDatabaseFile(Uri baseUri, string filePath)
		{
			using var stream = File.OpenRead(filePath);
			using var gzStream = new GZipStream(stream, CompressionMode.Decompress);
			using var reader = new StreamReader(gzStream);

			var name = string.Empty;
			var filename = string.Empty;
			var md5sum = string.Empty;
			var depends = string.Empty;
			var provides = string.Empty;

			while (!reader.EndOfStream)
			{
				var line = reader.ReadLine();

				if (line == null)
				{
					yield break;
				}

				if (string.IsNullOrEmpty(line))
				{
					if (!string.IsNullOrEmpty(name))
					{
						yield return new Package(name, $"{baseUri}/{filename}", md5sum, depends, provides);

						name = string.Empty;
					}

					continue;
				}

				var separator = line.IndexOf(':');

				if (separator == -1)
				{
					continue;
				}

				var fieldName = line[..separator];
				var rest = line[(separator + 1)..].Trim();

				switch (fieldName)
				{
					case "Package":
						name = rest.Trim();
						break;
					case "Filename":
						filename = rest.Trim();
						break;
					case "Depends":
						depends = rest.Trim();
						break;
					case "MD5sum":
						md5sum = rest.Trim();
						break;
					case "Provides":
						provides = rest.Trim();
						break;
				}
			}
		}
	}
}