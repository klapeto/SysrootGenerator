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

using System.Diagnostics;

namespace SysrootGenerator
{
	public static class ArchiveHelpers
	{
		public static void ExtractTar(string archivePath, string outputPath)
		{
			var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "tar",
					Arguments = $"-x --overwrite --file=\"{archivePath}\" --directory=\"{outputPath}\"",
					RedirectStandardError = true
				});

			var error = process!.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new Exception($"tar failed: '{archivePath}': {error}");
			}
		}

		public static void ExtractAr(string archivePath, string outputPath)
		{
			var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "ar",
					Arguments = $"-x -f --output=\"{outputPath}\" \"{archivePath}\"",
					RedirectStandardError = true
				});

			var error = process!.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new Exception($"tar failed: '{archivePath}': {error}");
			}
		}

		public static void DecompressXz(string file, string outputFile)
		{
			var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "xz",
					WorkingDirectory = Path.GetDirectoryName(outputFile),
					Arguments = $"-d -f \"{Path.GetFullPath(file)}\"",
					RedirectStandardError = true
				});

			var error = process!.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new Exception($"xz failed: '{file}': {error}");
			}
		}

		public static void DecompressGzip(string file, string outputFile)
		{
			var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "gzip",
					WorkingDirectory = Path.GetDirectoryName(outputFile),
					Arguments = $"-d -f \"{Path.GetFullPath(file)}\"",
					RedirectStandardError = true
				});

			var error = process!.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new Exception($"gzip failed: '{file}': {error}");
			}
		}

		public static void DecompressZstd(string file, string outputFile)
		{
			var process = Process.Start(
				new ProcessStartInfo
				{
					FileName = "zstd", Arguments = $"-d -f -o \"{outputFile}\" \"{file}\"", RedirectStandardError = true
				});

			var error = process!.StandardError.ReadToEnd();
			process.WaitForExit();

			if (process.ExitCode != 0)
			{
				throw new Exception($"zstd failed: '{file}': {error}");
			}
		}
	}
}