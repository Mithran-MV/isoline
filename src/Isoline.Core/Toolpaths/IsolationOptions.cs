namespace Isoline.Toolpaths
{
	/// <summary>
	/// Everything the isolation generator needs to turn copper polygons into a cut.
	/// Defaults are a sane starting point for a 30 degree V-bit at 0.1 mm depth on FR4.
	/// </summary>
	public class IsolationOptions
	{
		/// <summary>
		/// Width of material the tool removes, in millimetres. For a V-bit this is the
		/// width at the cutting depth, not the shank diameter - see
		/// <see cref="VBitWidth"/> to compute it.
		/// </summary>
		public double ToolDiameter { get; set; } = 0.2;

		/// <summary>Number of isolation passes. More passes widen the gap between traces.</summary>
		public int Passes { get; set; } = 1;

		/// <summary>
		/// Fraction of the tool width each additional pass steps over. 1.0 means passes sit
		/// edge to edge; below that they overlap, which leaves a cleaner floor.
		/// </summary>
		public double Stepover { get; set; } = 0.8;

		/// <summary>Cutting depth (negative, in millimetres).</summary>
		public double CutDepth { get; set; } = -0.1;

		/// <summary>Travel height between cuts (positive, in millimetres).</summary>
		public double SafeHeight { get; set; } = 2.0;

		/// <summary>Cutting feed rate, mm/min.</summary>
		public double FeedRate { get; set; } = 300;

		/// <summary>Plunge feed rate, mm/min.</summary>
		public double PlungeRate { get; set; } = 100;

		/// <summary>Spindle speed; 0 leaves the spindle alone (for a laser or a manual spindle).</summary>
		public double SpindleSpeed { get; set; } = 10000;

		/// <summary>
		/// Drops contours whose enclosed area is below this, in square millimetres.
		/// Filters out the slivers that offsetting leaves in tight corners, which the
		/// machine would otherwise spend minutes tracing.
		/// </summary>
		public double MinimumContourArea { get; set; } = 0.01;

		/// <summary>
		/// Douglas-Peucker tolerance in millimetres applied to the offset contours. Removes
		/// the redundant vertices that arc flattening produces without visibly changing the
		/// path; a smaller G-code file also streams faster over a 115200 baud link.
		/// </summary>
		public double Simplify { get; set; } = 0.002;

		/// <summary>Reorder contours to shorten rapid moves between them.</summary>
		public bool OptimiseTravel { get; set; } = true;

		/// <summary>
		/// Effective cut width of a V-shaped cutter at a given depth.
		/// </summary>
		/// <param name="includedAngleDegrees">Full included angle of the tip, e.g. 30.</param>
		/// <param name="depth">Depth of cut (positive millimetres).</param>
		/// <param name="tipWidth">Flat width at the very tip, 0 for a true point.</param>
		public static double VBitWidth(double includedAngleDegrees, double depth, double tipWidth = 0)
		{
			double halfAngle = includedAngleDegrees / 2 * System.Math.PI / 180.0;

			return tipWidth + 2 * System.Math.Abs(depth) * System.Math.Tan(halfAngle);
		}
	}
}
