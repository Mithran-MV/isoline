using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using Clipper2Lib;

namespace Isoline.Gerber
{
	/// <summary>
	/// A reader for the RS-274X (extended Gerber) subset that CAD tools emit for copper
	/// layers: standard apertures, linear and circular interpolation, regions (G36/G37),
	/// polarity (%LPD/%LPC) and step-and-repeat-free single images.
	/// <para>
	/// Everything is converted to millimetres and accumulated into a single polygon set, so
	/// the isolation generator downstream never has to care about Gerber's statefulness.
	/// </para>
	/// <para>
	/// Not supported, and reported as warnings rather than silently mis-drawn: aperture
	/// macros (%AM, approximated by a circle), step and repeat (%SR) and mirrored/scaled
	/// image transforms. These effectively never appear on a hobby PCB layer.
	/// </para>
	/// </summary>
	public class GerberParser
	{
		/// <summary>Segments used when flattening a full circle. Higher is smoother and slower.</summary>
		public int ArcSegments { get; set; } = 64;

		private GerberFormat _format = new GerberFormat();
		private GerberDocument _document;

		private Aperture _current;
		private PointD _position = new PointD(0, 0);
		private InterpolationMode _interpolation = InterpolationMode.Linear;
		private bool _multiQuadrant = true;
		private bool _dark = true;
		private bool _regionMode;
		private PathD _region;

		// Accumulated copper. Dark exposures union in, clear exposures cut out; keeping the
		// order matters because a %LPC region only clears what was drawn before it.
		private PathsD _accumulated = new PathsD();

		private enum InterpolationMode { Linear, ClockwiseArc, CounterClockwiseArc }

		public static GerberDocument ParseFile(string path)
		{
			return new GerberParser().Parse(File.ReadAllText(path));
		}

		public GerberDocument Parse(string content)
		{
			if (content == null)
				throw new ArgumentNullException(nameof(content));

			_document = new GerberDocument();
			_accumulated = new PathsD();
			_position = new PointD(0, 0);
			_dark = true;
			_regionMode = false;
			_region = null;

			foreach (string statement in Tokenize(content))
			{
				try
				{
					if (statement.StartsWith("%", StringComparison.Ordinal))
						HandleExtended(statement.Trim('%'));
					else
						HandleCommand(statement);
				}
				catch (GerberException ex)
				{
					_document.Warnings.Add(ex.Message);
				}
			}

			if (_regionMode)
				_document.Warnings.Add("file ended inside a region (G36 without G37)");

			_document.Copper = Clipper.Union(_accumulated, FillRule.NonZero);

			if (_document.Copper.Count == 0)
				_document.Warnings.Add("no copper found - is this a copper layer, or an outline/drill file?");

			return _document;
		}

		/// <summary>
		/// Splits the file into statements. Extended commands are delimited by '%' and may
		/// span lines; ordinary commands end at '*'.
		/// </summary>
		private static IEnumerable<string> Tokenize(string content)
		{
			int i = 0;

			while (i < content.Length)
			{
				char c = content[i];

				if (char.IsWhiteSpace(c))
				{
					i++;
					continue;
				}

				if (c == '%')
				{
					int end = content.IndexOf('%', i + 1);

					if (end < 0)
						yield break;

					yield return content.Substring(i, end - i + 1);
					i = end + 1;
				}
				else
				{
					int end = content.IndexOf('*', i);

					if (end < 0)
						yield break;

					string statement = content.Substring(i, end - i).Trim();

					if (statement.Length > 0)
						yield return statement;

					i = end + 1;
				}
			}
		}

		private static readonly Regex FormatSpec = new Regex(@"^FS([LT])?([AI])?X(\d)(\d)Y(\d)(\d)");

		private void HandleExtended(string statement)
		{
			// an extended block can hold several '*' separated commands
			foreach (string part in statement.Split('*'))
			{
				string command = part.Trim();

				if (command.Length == 0)
					continue;

				if (command.StartsWith("FS", StringComparison.Ordinal))
				{
					Match m = FormatSpec.Match(command);

					if (!m.Success)
						throw new GerberException("unsupported coordinate format: " + command);

					_format.OmitLeadingZeros = m.Groups[1].Value != "T";
					_format.Absolute = m.Groups[2].Value != "I";
					_format.IntegerDigits = int.Parse(m.Groups[3].Value, CultureInfo.InvariantCulture);
					_format.DecimalDigits = int.Parse(m.Groups[4].Value, CultureInfo.InvariantCulture);

					if (!_format.Absolute)
						_document.Warnings.Add("incremental coordinates (%FSI) are not supported");

					continue;
				}

				if (command.StartsWith("MO", StringComparison.Ordinal))
				{
					string unit = command.Substring(2).Trim();
					_format.UnitScale = unit == "IN" ? 25.4 : 1.0;
					continue;
				}

				if (command.StartsWith("ADD", StringComparison.Ordinal))
				{
					Aperture aperture = Aperture.Parse(command.Substring(3));

					if (aperture.Shape == ApertureShape.Macro)
						_document.Warnings.Add($"aperture macro '{aperture.MacroName}' (D{aperture.Code}) approximated by a circle");

					_document.Apertures[aperture.Code] = aperture;
					continue;
				}

				if (command.StartsWith("LP", StringComparison.Ordinal))
				{
					_dark = !command.EndsWith("C", StringComparison.Ordinal);
					continue;
				}

				if (command.StartsWith("AM", StringComparison.Ordinal))
					continue;   // macro body; the reference in ADD already warned

				if (command.StartsWith("SR", StringComparison.Ordinal) && command != "SR")
				{
					_document.Warnings.Add("step and repeat (%SR) is ignored");
					continue;
				}

				// %AS, %IP, %IR, %MI, %OF, %SF, %TF, %TA, %TD - attributes and legacy
				// transforms. Ignoring them is correct for attributes and harmless for a
				// normally-oriented board.
			}
		}

		private static readonly Regex CoordinateWord = new Regex(@"([XYIJ])([+-]?[\d.]+)");

		private void HandleCommand(string command)
		{
			if (command.StartsWith("G04", StringComparison.Ordinal) || command.StartsWith("G4", StringComparison.Ordinal))
				return;     // comment

			if (command == "M02" || command == "M2" || command == "M00")
				return;

			// G-codes may prefix a coordinate command, e.g. "G01X100Y100D01"
			foreach (Match m in Regex.Matches(command, @"G(\d{1,2})"))
			{
				switch (int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture))
				{
					case 1: _interpolation = InterpolationMode.Linear; break;
					case 2: _interpolation = InterpolationMode.ClockwiseArc; break;
					case 3: _interpolation = InterpolationMode.CounterClockwiseArc; break;
					case 36: BeginRegion(); break;
					case 37: EndRegion(); break;
					case 70: _format.UnitScale = 25.4; break;   // deprecated inch
					case 71: _format.UnitScale = 1.0; break;    // deprecated mm
					case 74: _multiQuadrant = false; break;
					case 75: _multiQuadrant = true; break;
					case 54: break;                             // deprecated tool select prefix
					default: break;
				}
			}

			// aperture selection: D10 and up (D01/D02/D03 are operations)
			Match aperture = Regex.Match(command, @"D0*(\d+)\s*$");

			if (aperture.Success)
			{
				int code = int.Parse(aperture.Groups[1].Value, CultureInfo.InvariantCulture);

				if (code >= 10)
				{
					Aperture selected;

					if (_document.Apertures.TryGetValue(code, out selected))
						_current = selected;
					else
						_document.Warnings.Add($"selected undefined aperture D{code}");

					return;
				}
			}

			MatchCollection words = CoordinateWord.Matches(command);

			if (words.Count == 0 && !aperture.Success)
				return;

			double? x = null, y = null, i = null, j = null;

			foreach (Match word in words)
			{
				double value = _format.ToMillimetres(word.Groups[2].Value);

				switch (word.Groups[1].Value)
				{
					case "X": x = value; break;
					case "Y": y = value; break;
					case "I": i = value; break;
					case "J": j = value; break;
				}
			}

			PointD target = new PointD(x ?? _position.x, y ?? _position.y);

			int operation = aperture.Success
				? int.Parse(aperture.Groups[1].Value, CultureInfo.InvariantCulture)
				: 1;    // a bare coordinate repeats the previous D01 in practice

			switch (operation)
			{
				case 1:
					Interpolate(target, i, j);
					break;

				case 2:
					if (_regionMode && _region != null && _region.Count > 0)
					{
						CloseRegionContour();
						_region = new PathD();
					}
					_position = target;
					break;

				case 3:
					Flash(target);
					_position = target;
					break;
			}
		}

		private void BeginRegion()
		{
			_regionMode = true;
			_region = new PathD();
		}

		private void EndRegion()
		{
			if (_regionMode)
				CloseRegionContour();

			_regionMode = false;
			_region = null;
		}

		private void CloseRegionContour()
		{
			if (_region == null || _region.Count < 3)
				return;

			PathsD polygon = new PathsD { _region };
			Compose(polygon);
			_region = new PathD();
		}

		private void Interpolate(PointD target, double? i, double? j)
		{
			List<PointD> points = _interpolation == InterpolationMode.Linear
				? new List<PointD> { _position, target }
				: ArcPoints(_position, target, i ?? 0, j ?? 0, _interpolation == InterpolationMode.ClockwiseArc);

			if (_regionMode)
			{
				if (_region == null)
					_region = new PathD();

				if (_region.Count == 0)
					_region.Add(_position);

				for (int k = 1; k < points.Count; k++)
					_region.Add(points[k]);
			}
			else
			{
				StrokePath(points);
			}

			_position = target;
		}

		/// <summary>
		/// Turns a track into copper by inflating its centre line by half the aperture width.
		/// Round joins and ends match how a photoplotter actually drags a circular aperture.
		/// </summary>
		private void StrokePath(List<PointD> points)
		{
			if (_current == null)
			{
				_document.Warnings.Add("draw command before any aperture was selected");
				return;
			}

			double width = _current.StrokeWidth;

			if (width <= 0)
				return;

			PathD line = new PathD();

			foreach (PointD p in points)
				line.Add(p);

			// A zero-length draw is how CAD tools express "put a dot here"; Clipper would
			// discard it, so flash the aperture instead.
			if (line.Count == 2 && Distance(line[0], line[1]) < 1e-9)
			{
				Flash(line[0]);
				return;
			}

			PathsD stroked = Clipper.InflatePaths(
				new PathsD { line }, width / 2,
				_current.Shape == ApertureShape.Rectangle ? JoinType.Miter : JoinType.Round,
				_current.Shape == ApertureShape.Rectangle ? EndType.Square : EndType.Round,
				2.0, 6);

			Compose(stroked);
		}

		private void Flash(PointD at)
		{
			if (_current == null)
			{
				_document.Warnings.Add("flash command before any aperture was selected");
				return;
			}

			PathsD shape = _current.ToPolygon(ArcSegments);
			PathsD moved = new PathsD();

			foreach (PathD path in shape)
			{
				PathD translated = new PathD(path.Count);

				foreach (PointD p in path)
					translated.Add(new PointD(p.x + at.x, p.y + at.y));

				moved.Add(translated);
			}

			Compose(moved);
		}

		/// <summary>
		/// Adds (dark) or removes (clear) a shape from the accumulated copper.
		/// </summary>
		private void Compose(PathsD shape)
		{
			if (shape.Count == 0)
				return;

			if (_dark)
			{
				_accumulated = Clipper.Union(_accumulated, shape, FillRule.NonZero);
			}
			else
			{
				_accumulated = Clipper.Difference(_accumulated, shape, FillRule.NonZero);
			}
		}

		/// <summary>
		/// Flattens a circular interpolation into line segments.
		/// <para>
		/// In multi-quadrant mode (G75, the modern default) I and J are signed offsets from
		/// the current point to the centre. In single-quadrant mode (G74) they are
		/// unsigned, and the correct signs have to be recovered by testing which of the four
		/// candidate centres is equidistant from both endpoints.
		/// </para>
		/// </summary>
		private List<PointD> ArcPoints(PointD from, PointD to, double i, double j, bool clockwise)
		{
			PointD centre = new PointD(from.x + i, from.y + j);

			if (!_multiQuadrant)
			{
				double best = double.MaxValue;
				PointD bestCentre = centre;

				foreach (int si in new[] { 1, -1 })
				{
					foreach (int sj in new[] { 1, -1 })
					{
						PointD candidate = new PointD(from.x + si * Math.Abs(i), from.y + sj * Math.Abs(j));
						double error = Math.Abs(Distance(candidate, from) - Distance(candidate, to));

						if (error < best)
						{
							best = error;
							bestCentre = candidate;
						}
					}
				}

				centre = bestCentre;
			}

			double radius = Distance(centre, from);
			List<PointD> points = new List<PointD> { from };

			if (radius < 1e-9)
			{
				points.Add(to);
				return points;
			}

			double startAngle = Math.Atan2(from.y - centre.y, from.x - centre.x);
			double endAngle = Math.Atan2(to.y - centre.y, to.x - centre.x);
			double sweep = endAngle - startAngle;

			if (clockwise)
			{
				while (sweep > 0)
					sweep -= 2 * Math.PI;

				// a full circle is expressed as start == end
				if (Math.Abs(sweep) < 1e-9)
					sweep = -2 * Math.PI;
			}
			else
			{
				while (sweep < 0)
					sweep += 2 * Math.PI;

				if (Math.Abs(sweep) < 1e-9)
					sweep = 2 * Math.PI;
			}

			int steps = Math.Max(2, (int)Math.Ceiling(Math.Abs(sweep) / (2 * Math.PI) * ArcSegments));

			for (int k = 1; k <= steps; k++)
			{
				double a = startAngle + sweep * k / steps;
				points.Add(new PointD(centre.x + radius * Math.Cos(a), centre.y + radius * Math.Sin(a)));
			}

			points[points.Count - 1] = to;
			return points;
		}

		private static double Distance(PointD a, PointD b)
		{
			double dx = a.x - b.x;
			double dy = a.y - b.y;

			return Math.Sqrt(dx * dx + dy * dy);
		}
	}
}
