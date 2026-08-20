namespace Isoline.GCode
{
	/// <summary>
	/// How heights between probed grid points are reconstructed.
	/// </summary>
	public enum InterpolationMode
	{
		/// <summary>
		/// Weighted average of the four surrounding points. Fast, exactly reproduces the
		/// probed values, but the surface has a crease along every grid line - on a coarse
		/// grid that shows up as visible steps in the depth of an engraved trace.
		/// </summary>
		Bilinear = 0,

		/// <summary>
		/// Catmull-Rom bicubic over a 4x4 window. Still passes through every probed point,
		/// but the first derivative is continuous, so the compensated toolpath follows the
		/// real board curvature instead of a faceted approximation of it.
		/// </summary>
		Bicubic = 1,
	}
}
