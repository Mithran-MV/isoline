using System;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Isoline.GCode;
using Isoline.GCode.GCodeCommands;

namespace Isoline.Visuals
{
	/// <summary>
	/// Turns a parsed G-code file into the three line sets the viewport draws:
	/// cutting moves, rapids and arcs.
	/// </summary>
	public static class ToolpathVisuals
	{
		/// <summary>
		/// Segments used to draw one full circle of an arc in the preview. Purely visual -
		/// the arcs are still sent to the machine as arcs.
		/// </summary>
		public static double ViewportArcSplit { get; set; } = 1;

		public static void GetModel(this GCodeFile file, LinesVisual3D line, LinesVisual3D rapid, LinesVisual3D arc)
		{
			var stopwatch = System.Diagnostics.Stopwatch.StartNew();

			Point3DCollection linePoints = new Point3DCollection();
			Point3DCollection rapidPoints = new Point3DCollection();
			Point3DCollection arcPoints = new Point3DCollection();

			foreach (Command command in file.Toolpath)
			{
				var straight = command as Line;

				if (straight != null)
				{
					if (!straight.StartValid)
						continue;

					Point3DCollection target = straight.Rapid ? rapidPoints : linePoints;

					target.Add(straight.Start.ToPoint3D());
					target.Add(straight.End.ToPoint3D());

					continue;
				}

				var curve = command as Arc;

				if (curve != null)
				{
					foreach (Motion segment in curve.Split(ViewportArcSplit))
					{
						arcPoints.Add(segment.Start.ToPoint3D());
						arcPoints.Add(segment.End.ToPoint3D());
					}
				}
			}

			line.Points = linePoints;
			rapid.Points = rapidPoints;
			arc.Points = arcPoints;

			stopwatch.Stop();
			Console.WriteLine("Generating the toolpath model took {0} ms", stopwatch.ElapsedMilliseconds);
		}
	}
}
