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
	internal static class Logger
	{
		public static bool EnableVerbose { get; set; }

		public static void Verbose(string message, Exception? exception = null)
		{
			if (EnableVerbose)
			{
				Console.Error.WriteLine($"[Verbose] {message} {exception?.Message}");
			}
		}

		public static void Error(string message, Exception? exception = null)
		{
			Console.Error.WriteLine($"[Error] {message} {exception?.Message}");
		}

		public static void Warning(string message, Exception? exception = null)
		{
			Console.WriteLine($"[Warning] {message} {exception?.Message}");
		}

		public static void Info(string message)
		{
			Console.WriteLine($"[Info] {message}");
		}
	}
}