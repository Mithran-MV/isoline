using System;
using System.Collections.Generic;
using Clipper2Lib;
using Isoline.Util;

namespace Isoline.Gerber
{
	/// <summary>
	/// The result of reading a Gerber layer: the copper as a set of polygons in millimetres,
	/// plus anything the parser wants the operator to know about.
	/// </summary>
	public class GerberDocument
	{
		/// <summary>
		/// Copper regions, already unioned and with clear (%LPC) areas subtracted.
		/// Outer contours and holes follow Clipper's non-zero winding convention.
		/// </summary>
		public PathsD Copper { get; set; } = new PathsD();

		/// <summary>Non-fatal problems: unsupported constructs, approximations made.</summary>
		public List<string> Warnings { get; } = new List<string>();

		/// <summary>Aperture table, keyed by D-code.</summary>
		public Dictionary<int, Aperture> Apertures { get; } = new Dictionary<int, Aperture>();

		/// <summary>
		/// Bottom-left corner of the copper, in millimetres.
		/// <para>
		/// Deliberately not Clipper's own RectD: that type follows the screen convention
		/// where "top" is the smaller Y, and letting it through would invert every board
		/// the moment those numbers met machine coordinates.
		/// </para>
		/// </summary>
		public Vector2 Min { get { return BoundsMin(); } }

		/// <summary>Top-right corner of the copper, in millimetres.</summary>
		public Vector2 Max { get { return BoundsMax(); } }

		/// <summary>Size of the copper's bounding box, in millimetres.</summary>
		public Vector2 Size
		{
			get
			{
				Vector2 min = Min, max = Max;
				return new Vector2(max.X - min.X, max.Y - min.Y);
			}
		}

		public bool IsEmpty
		{
			get { return Copper.Count == 0; }
		}

		private Vector2 BoundsMin()
		{
			double x = double.MaxValue, y = double.MaxValue;

			foreach (PathD path in Copper)
				foreach (PointD p in path)
				{
					x = Math.Min(x, p.x);
					y = Math.Min(y, p.y);
				}

			return Copper.Count == 0 ? new Vector2(0, 0) : new Vector2(x, y);
		}

		private Vector2 BoundsMax()
		{
			double x = double.MinValue, y = double.MinValue;

			foreach (PathD path in Copper)
				foreach (PointD p in path)
				{
					x = Math.Max(x, p.x);
					y = Math.Max(y, p.y);
				}

			return Copper.Count == 0 ? new Vector2(0, 0) : new Vector2(x, y);
		}
	}
}
