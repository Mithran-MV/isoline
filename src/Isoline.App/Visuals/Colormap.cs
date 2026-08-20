using System;
using System.Windows.Media;

namespace Isoline.Visuals
{
	/// <summary>
	/// Colour ramps for the height map surface.
	/// <para>
	/// The default is a viridis-style ramp rather than the usual blue-to-red rainbow.
	/// A rainbow is not perceptually uniform: it invents a bright band around yellow and a
	/// flat stretch through cyan, so equal height differences look very unequal and a
	/// gentle bow can read as a sharp ridge. Viridis increases monotonically in lightness,
	/// which means the picture of the board matches the numbers, and it survives being
	/// printed or viewed by someone with red-green colour blindness.
	/// </para>
	/// </summary>
	public static class Colormap
	{
		/// <summary>Control points of the viridis ramp, from low to high.</summary>
		private static readonly Color[] Viridis =
		{
			Color.FromRgb(0x44, 0x01, 0x54),
			Color.FromRgb(0x41, 0x44, 0x87),
			Color.FromRgb(0x2A, 0x78, 0x8E),
			Color.FromRgb(0x22, 0xA8, 0x84),
			Color.FromRgb(0x7A, 0xD1, 0x51),
			Color.FromRgb(0xFD, 0xE7, 0x25),
		};

		/// <summary>A blue-white-red ramp, for judging a surface against a zero plane.</summary>
		private static readonly Color[] Diverging =
		{
			Color.FromRgb(0x21, 0x66, 0xAC),
			Color.FromRgb(0x92, 0xC5, 0xDE),
			Color.FromRgb(0xF7, 0xF7, 0xF7),
			Color.FromRgb(0xF4, 0xA5, 0x82),
			Color.FromRgb(0xB2, 0x18, 0x2B),
		};

		public static Color Sample(double t, bool diverging = false)
		{
			Color[] ramp = diverging ? Diverging : Viridis;

			t = Math.Max(0, Math.Min(1, t));

			double scaled = t * (ramp.Length - 1);
			int index = (int)Math.Floor(scaled);

			if (index >= ramp.Length - 1)
				return ramp[ramp.Length - 1];

			double f = scaled - index;

			return Color.FromRgb(
				(byte)(ramp[index].R + (ramp[index + 1].R - ramp[index].R) * f),
				(byte)(ramp[index].G + (ramp[index + 1].G - ramp[index].G) * f),
				(byte)(ramp[index].B + (ramp[index + 1].B - ramp[index].B) * f));
		}

		/// <summary>
		/// Builds the gradient brush used both by the height map material and by the legend
		/// in the probing tab, so the surface and its scale can never disagree.
		/// </summary>
		public static LinearGradientBrush CreateBrush(double opacity = 1.0, bool diverging = false, bool vertical = true)
		{
			GradientStopCollection stops = new GradientStopCollection();

			for (int i = 0; i <= 32; i++)
			{
				double t = i / 32.0;
				stops.Add(new GradientStop(Sample(t, diverging), t));
			}

			LinearGradientBrush brush = new LinearGradientBrush(stops)
			{
				StartPoint = vertical ? new System.Windows.Point(0, 1) : new System.Windows.Point(0, 0),
				EndPoint = vertical ? new System.Windows.Point(0, 0) : new System.Windows.Point(1, 0),
				Opacity = opacity,
			};

			brush.Freeze();
			return brush;
		}
	}
}
