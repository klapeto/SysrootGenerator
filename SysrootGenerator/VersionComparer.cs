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
	/// <summary>
	///     Compares two strings using the Debian version comparison algorithm.
	/// </summary>
	/// <remarks>Ported from https://git.dpkg.org/cgit/dpkg/dpkg.git/tree/lib/dpkg/version.c</remarks>
	public class VersionComparer : IComparer<string>
	{
		public int Compare(string? x, string? y)
		{
			x ??= string.Empty;
			y ??= string.Empty;

			var ai = 0;
			var bi = 0;

			bool AL()
			{
				return ai < x.Length;
			}

			char A()
			{
				return AL() ? x[ai] : (char)0;
			}

			bool BL()
			{
				return bi < y.Length;
			}

			char B()
			{
				return BL() ? y[bi] : (char)0;
			}

			while (AL() || BL())
			{
				var firstDiff = 0;

				while ((AL() && !char.IsDigit(A())) || (BL() && !char.IsDigit(B())))
				{
					var ac = Order(A());
					var bc = Order(B());

					if (ac != bc)
					{
						return ac - bc;
					}

					ai++;
					bi++;
				}

				while (A() == '0')
				{
					ai++;
				}

				while (B() == '0')
				{
					bi++;
				}

				while (char.IsDigit(A()) && char.IsDigit(B()))
				{
					if (firstDiff == 0)
					{
						firstDiff = A() - B();
					}

					ai++;
					bi++;
				}

				if (char.IsDigit(A()))
				{
					return 1;
				}

				if (char.IsDigit(B()))
				{
					return -1;
				}

				if (firstDiff != 0)
				{
					return firstDiff;
				}
			}

			return 0;
		}

		private static int Order(char c)
		{
			if (char.IsDigit(c))
			{
				return 0;
			}

			if (char.IsLetter(c))
			{
				return c;
			}

			if (c == '~')
			{
				return -1;
			}

			if (c != 0)
			{
				return c + 256;
			}

			return 0;
		}
	}
}