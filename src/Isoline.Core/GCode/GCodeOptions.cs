namespace Isoline.GCode
{
	/// <summary>
	/// Controls what the G-code writer emits. The WPF application copies the user's
	/// settings into <see cref="Current"/> at start-up, which keeps Isoline.Core free of
	/// any dependency on an application settings object (and therefore testable).
	/// </summary>
	public class GCodeOutputOptions
	{
		public static GCodeOutputOptions Current { get; set; } = new GCodeOutputOptions();

		/// <summary>Emit M2/M30 program-end codes.</summary>
		public bool IncludeProgramEnd { get; set; } = true;

		/// <summary>Emit S spindle-speed words.</summary>
		public bool IncludeSpindle { get; set; } = true;

		/// <summary>Emit G4 dwell commands.</summary>
		public bool IncludeDwell { get; set; } = true;
	}

	/// <summary>
	/// Controls how leniently the parser treats input files.
	/// </summary>
	public class GCodeParserOptions
	{
		public static GCodeParserOptions Current { get; set; } = new GCodeParserOptions();

		/// <summary>
		/// Silently drop A/B/C/U/V/W words instead of warning about them. Useful for files
		/// exported for a rotary-axis machine that are being run on a 3 axis one.
		/// </summary>
		public bool IgnoreAdditionalAxes { get; set; } = true;
	}
}
