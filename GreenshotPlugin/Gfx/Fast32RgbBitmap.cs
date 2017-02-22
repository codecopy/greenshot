#region Greenshot GNU General Public License

// Greenshot - a free and open source screenshot tool
// Copyright (C) 2007-2017 Thomas Braun, Jens Klingen, Robin Krom
// 
// For more information see: http://getgreenshot.org/
// The Greenshot project is hosted on GitHub https://github.com/greenshot/greenshot
// 
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 1 of the License, or
// (at your option) any later version.
// 
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
// 
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <http://www.gnu.org/licenses/>.

#endregion

#region Usings

using System.Drawing;

#endregion

namespace GreenshotPlugin.Core.Gfx
{
	/// <summary>
	///     This is the implementation of the IFastBitmap for 32 bit images (no Alpha)
	/// </summary>
	public unsafe class Fast32RgbBitmap : FastBitmap
	{
		public Fast32RgbBitmap(Bitmap source, Rectangle? area = null) : base(source, area)
		{
		}

		/// <summary>
		///     Retrieve the color at location x,y
		///     Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x">X coordinate</param>
		/// <param name="y">Y Coordinate</param>
		/// <returns>Color</returns>
		public override Color GetColorAt(int x, int y)
		{
			var offset = x * 4 + y * Stride;
			return Color.FromArgb(255, Pointer[PixelformatIndexR + offset], Pointer[PixelformatIndexG + offset], Pointer[PixelformatIndexB + offset]);
		}

		/// <summary>
		///     Set the color at location x,y
		///     Before the first time this is called the Lock() should be called once!
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color"></param>
		public override void SetColorAt(int x, int y, Color color)
		{
			var offset = x * 4 + y * Stride;
			Pointer[PixelformatIndexR + offset] = color.R;
			Pointer[PixelformatIndexG + offset] = color.G;
			Pointer[PixelformatIndexB + offset] = color.B;
		}

		/// <summary>
		///     Get the color from the specified location into the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (a,r,g,b)</param>
		public override void GetColorAt(int x, int y, byte[] color)
		{
			var offset = x * 4 + y * Stride;
			color[ColorIndexR] = Pointer[PixelformatIndexR + offset];
			color[ColorIndexG] = Pointer[PixelformatIndexG + offset];
			color[ColorIndexB] = Pointer[PixelformatIndexB + offset];
		}

		/// <summary>
		///     Set the color at the specified location from the specified array
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="color">byte[4] as reference (r,g,b)</param>
		public override void SetColorAt(int x, int y, byte[] color)
		{
			var offset = x * 4 + y * Stride;
			Pointer[PixelformatIndexR + offset] = color[ColorIndexR]; // R
			Pointer[PixelformatIndexG + offset] = color[ColorIndexG];
			Pointer[PixelformatIndexB + offset] = color[ColorIndexB];
		}
	}
}