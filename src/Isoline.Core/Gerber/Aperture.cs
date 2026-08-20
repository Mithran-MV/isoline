using System;
using System.Collections.Generic;
using System.Globalization;
using Clipper2Lib;

namespace Isoline.Gerber
{
	/// <summary>
	/// A Gerber aperture: the shape that gets flashed at a point (D03) or dragged along a
	/// path (D01). Only the four standard shapes are modelled - C(ircle), R(ectangle),
	/// O(bround) and P(olygon) - which is what board houses accept for copper layers and
	/// what KiCad, EAGLE, Altium and Fusion all emit.
	/// </summary>
	public class Aperture
	{
		public int Code { get; set; }
		public ApertureShape Shape { get; set; }

		/// <summary>Diameter for C/P, X size for R/O.</summary>
		public double SizeX { get; set; }

		/// <summary>Y size for R/O, unused for C.</summary>
		public double SizeY { get; set; }

		/// <summary>Number of vertices for a regular polygon aperture.</summary>
		public int Vertices { get; set; }

		/// <summary>Rotation in degrees for a regular polygon aperture.</summary>
		public double Rotation { get; set; }

		/// <summary>Diameter of an optional central hole; drilled out of the flash.</summary>
		public double HoleDiameter { get; set; }

		/// <summary>
		/// Effective width when this aperture is dragged along a track. Rectangular
		/// apertures are approximated by their smaller dimension, which is what the
		/// isolation pass has to clear as a worst case.
		/// </summary>
		public double StrokeWidth
		{
			get
			{
				switch (Shape)
				{
					case ApertureShape.Circle:
					case ApertureShape.Polygon:
						return SizeX;
					case ApertureShape.Rectangle:
					case ApertureShape.Obround:
						return Math.Min(SizeX, SizeY);
					default:
						return SizeX;
				}
			}
		}

		/// <summary>
		/// The aperture outline as a closed polygon centred on the origin, ready to be
		/// translated to a flash position.
		/// </summary>
		/// <param name="arcSegments">Segments used to approximate a full circle.</param>
		public PathsD ToPolygon(int arcSegments = 64)
		{
			PathsD result = new PathsD();
			PathD outline = new PathD();

			switch (Shape)
			{
				case ApertureShape.Circle:
					AddCircle(outline, 0, 0, SizeX / 2, arcSegments);
					break;

				case ApertureShape.Rectangle:
					outline.Add(new PointD(-SizeX / 2, -SizeY / 2));
					outline.Add(new PointD(SizeX / 2, -SizeY / 2));
					outline.Add(new PointD(SizeX / 2, SizeY / 2));
					outline.Add(new PointD(-SizeX / 2, SizeY / 2));
					break;

				case ApertureShape.Obround:
					AddObround(outline, SizeX, SizeY, arcSegments);
					break;

				case ApertureShape.Polygon:
					AddRegularPolygon(outline, SizeX / 2, Math.Max(3, Vertices), Rotation, arcSegments);
					break;

				default:
					throw new GerberException("unsupported aperture shape " + Shape);
			}

			result.Add(outline);

			if (HoleDiameter > 0)
			{
				PathD hole = new PathD();
				AddCircle(hole, 0, 0, HoleDiameter / 2, arcSegments);
				hole.Reverse();     // opposite winding so it clips as a hole
				result.Add(hole);
			}

			return result;
		}

		public static void AddCircle(PathD path, double cx, double cy, double radius, int segments)
		{
			if (radius <= 0)
				return;

			for (int i = 0; i < segments; i++)
			{
				double a = 2 * Math.PI * i / segments;
				path.Add(new PointD(cx + radius * Math.Cos(a), cy + radius * Math.Sin(a)));
			}
		}

		private static void AddObround(PathD path, double sizeX, double sizeY, int segments)
		{
			// a stadium: two semicircular caps joined by straight flanks along the longer axis
			double radius = Math.Min(sizeX, sizeY) / 2;
			int half = Math.Max(4, segments / 2);

			if (sizeX >= sizeY)
			{
				double dx = (sizeX - sizeY) / 2;

				for (int i = 0; i <= half; i++)
				{
					double a = -Math.PI / 2 + Math.PI * i / half;
					path.Add(new PointD(dx + radius * Math.Cos(a), radius * Math.Sin(a)));
				}

				for (int i = 0; i <= half; i++)
				{
					double a = Math.PI / 2 + Math.PI * i / half;
					path.Add(new PointD(-dx + radius * Math.Cos(a), radius * Math.Sin(a)));
				}
			}
			else
			{
				double dy = (sizeY - sizeX) / 2;

				for (int i = 0; i <= half; i++)
				{
					double a = 0 + Math.PI * i / half;
					path.Add(new PointD(radius * Math.Cos(a), dy + radius * Math.Sin(a)));
				}

				for (int i = 0; i <= half; i++)
				{
					double a = Math.PI + Math.PI * i / half;
					path.Add(new PointD(radius * Math.Cos(a), -dy + radius * Math.Sin(a)));
				}
			}
		}

		private static void AddRegularPolygon(PathD path, double radius, int vertices, double rotationDegrees, int segments)
		{
			double rotation = rotationDegrees * Math.PI / 180.0;

			for (int i = 0; i < vertices; i++)
			{
				double a = rotation + 2 * Math.PI * i / vertices;
				path.Add(new PointD(radius * Math.Cos(a), radius * Math.Sin(a)));
			}
		}

		/// <summary>
		/// Parses the body of an %ADD directive, e.g. <c>10C,0.254</c> or <c>12R,1.6X0.8X0.4</c>.
		/// </summary>
		public static Aperture Parse(string body)
		{
			int letterIndex = 0;

			while (letterIndex < body.Length && char.IsDigit(body[letterIndex]))
				letterIndex++;

			if (letterIndex == 0 || letterIndex >= body.Length)
				throw new GerberException("malformed aperture definition: " + body);

			Aperture aperture = new Aperture()
			{
				Code = int.Parse(body.Substring(0, letterIndex), CultureInfo.InvariantCulture),
			};

			string rest = body.Substring(letterIndex);
			string shapeName;
			string parameters = string.Empty;

			int comma = rest.IndexOf(',');

			if (comma < 0)
			{
				shapeName = rest.Trim();
			}
			else
			{
				shapeName = rest.Substring(0, comma).Trim();
				parameters = rest.Substring(comma + 1);
			}

			double[] values = ParseParameters(parameters);

			switch (shapeName)
			{
				case "C":
					aperture.Shape = ApertureShape.Circle;
					aperture.SizeX = Value(values, 0, body);
					aperture.HoleDiameter = values.Length > 1 ? values[1] : 0;
					break;

				case "R":
					aperture.Shape = ApertureShape.Rectangle;
					aperture.SizeX = Value(values, 0, body);
					aperture.SizeY = Value(values, 1, body);
					aperture.HoleDiameter = values.Length > 2 ? values[2] : 0;
					break;

				case "O":
					aperture.Shape = ApertureShape.Obround;
					aperture.SizeX = Value(values, 0, body);
					aperture.SizeY = Value(values, 1, body);
					aperture.HoleDiameter = values.Length > 2 ? values[2] : 0;
					break;

				case "P":
					aperture.Shape = ApertureShape.Polygon;
					aperture.SizeX = Value(values, 0, body);
					aperture.Vertices = (int)Value(values, 1, body);
					aperture.Rotation = values.Length > 2 ? values[2] : 0;
					aperture.HoleDiameter = values.Length > 3 ? values[3] : 0;
					break;

				default:
					// An aperture macro (%AM) reference. Approximating it as a circle of the
					// first parameter is wrong in detail but keeps the rest of the layer
					// usable; the parser records a warning so the UI can say so.
					aperture.Shape = ApertureShape.Macro;
					aperture.MacroName = shapeName;
					aperture.SizeX = values.Length > 0 ? values[0] : 0;
					break;
			}

			return aperture;
		}

		/// <summary>Name of the aperture macro, when <see cref="Shape"/> is Macro.</summary>
		public string MacroName { get; set; }

		private static double Value(double[] values, int index, string body)
		{
			if (index >= values.Length)
				throw new GerberException("aperture definition is missing a parameter: " + body);

			return values[index];
		}

		private static double[] ParseParameters(string parameters)
		{
			if (string.IsNullOrWhiteSpace(parameters))
				return new double[0];

			string[] parts = parameters.Split('X');
			List<double> values = new List<double>(parts.Length);

			foreach (string part in parts)
			{
				double value;

				if (double.TryParse(part.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out value))
					values.Add(value);
			}

			return values.ToArray();
		}
	}

	public enum ApertureShape
	{
		Circle,
		Rectangle,
		Obround,
		Polygon,

		/// <summary>A %AM aperture macro; approximated by a circle.</summary>
		Macro,
	}
}
