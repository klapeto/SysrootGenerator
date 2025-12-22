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

using Microsoft.CST.RecursiveExtractor;
using Microsoft.CST.RecursiveExtractor.Extractors;
using ZstdSharp;

namespace SysrootGenerator
{
	public static class ArchiveHelpers
	{
		public static void ExtractTar(string file, string outputPath)
		{
			ExtractAr(file, outputPath);
		}

		public static void ExtractAr(string file, string outputPath)
		{
			using var stream = File.OpenRead(file);
			ExtractAr(stream, outputPath);
		}

		public static void ExtractXz(string file, string outputPath)
		{
			ExtractAr(file, outputPath);
		}

		public static void ExtractGzip(string file, string outputPath)
		{
			ExtractAr(file, outputPath);
		}

		public static void ExtractZstd(string file, string outputPath)
		{
			using var input = File.OpenRead(file);
			using var buffer = new MemoryStream();
			using var decompressionStream = new DecompressionStream(input);
			decompressionStream.CopyTo(buffer);
			ExtractAr(buffer, outputPath);
		}

		private static void ExtractAr(Stream stream, string outputPath)
		{
			var extractor = new Extractor();
			extractor.SetExtractor(ArchiveFileType.DEB, new GnuArExtractor(extractor)); // Workaround bug with certain debs

			foreach (var file in extractor.Extract(
						string.Empty,
						stream,
						new ExtractorOptions
						{
							Recurse = false
						}))
			{
				if (string.IsNullOrEmpty(file.FullPath))
				{
					continue;
				}

				var outFilePath = Path.Combine(outputPath, file.FullPath);
				Directory.CreateDirectory(Path.GetDirectoryName(outFilePath));
				using var output = File.OpenWrite(outFilePath);
				file.Content.CopyTo(output);
			}
		}
	}
}