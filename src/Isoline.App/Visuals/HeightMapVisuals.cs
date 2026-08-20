using System;
using System.Windows;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Isoline.GCode;
using Isoline.Util;

namespace Isoline.Visuals
{
	/// <summary>
	/// Builds the 3D representation of a height map. Kept out of Isoline.Core so the map
	/// itself stays a plain data structure.
	/// </summary>
	public static class HeightMapVisuals
	{
		/// <summary>
		/// Builds the probed surface as a textured quad mesh. The texture coordinate is the
		/// point's height normalised to 0..1, which the colour ramp material turns into the
		/// visible gradient.
		/// </summary>
		public static void GetModel(this HeightMap map, MeshGeometryVisual3D mesh)
		{
			MeshBuilder mb = new MeshBuilder(false, true);

			double span = map.MaxHeight - map.MinHeight;

			// A perfectly flat map has zero span; dividing by it would send every texture
			// coordinate to infinity and the surface would render black.
			bool flat = span < 1e-9;

			for (int x = 0; x < map.SizeX - 1; x++)
			{
				for (int y = 0; y < map.SizeY - 1; y++)
				{
					if (!map.Points[x, y].HasValue || !map.Points[x, y + 1].HasValue
						|| !map.Points[x + 1, y].HasValue || !map.Points[x + 1, y + 1].HasValue)
						continue;

					mb.AddQuad(
						Corner(map, x + 1, y),
						Corner(map, x + 1, y + 1),
						Corner(map, x, y + 1),
						Corner(map, x, y),
						Texture(map, x + 1, y, span, flat),
						Texture(map, x + 1, y + 1, span, flat),
						Texture(map, x, y + 1, span, flat),
						Texture(map, x, y, span, flat));
				}
			}

			mesh.MeshGeometry = mb.ToMesh();
		}

		private static Point3D Corner(HeightMap map, int x, int y)
		{
			Vector2 p = map.GetCoordinates(x, y);

			return new Point3D(p.X, p.Y, map.Points[x, y].Value);
		}

		/// <summary>
		/// Height normalised into the 0..1 texture range.
		/// <para>
		/// Upstream multiplied by the height span here instead of dividing by it, so the
		/// colour of the surface depended on how warped the board was rather than on where
		/// each point sat within that warp - a nearly flat board came out uniformly dark
		/// and a badly warped one saturated.
		/// </para>
		/// </summary>
		private static Point Texture(HeightMap map, int x, int y, double span, bool flat)
		{
			double t = flat ? 0.5 : (map.Points[x, y].Value - map.MinHeight) / span;

			return new Point(0, t);
		}

		public static void GetPreviewModel(this HeightMap map, LinesVisual3D border, PointsVisual3D points)
		{
			GetPreviewModel(map.Min, map.Max, map.SizeX, map.SizeY, border, points);
		}

		public static void GetPreviewModel(Vector2 min, Vector2 max, double gridSize, LinesVisual3D border, PointsVisual3D points)
		{
			Vector2 low = new Vector2(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y));
			Vector2 high = new Vector2(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y));

			if ((high.X - low.X) == 0 || (high.Y - low.Y) == 0 || gridSize <= 0)
			{
				points.Points.Clear();
				border.Points.Clear();
				return;
			}

			int pointsX = (int)Math.Ceiling((high.X - low.X) / gridSize) + 1;
			int pointsY = (int)Math.Ceiling((high.Y - low.Y) / gridSize) + 1;

			GetPreviewModel(low, high, pointsX, pointsY, border, points);
		}

		public static void GetPreviewModel(Vector2 min, Vector2 max, int pointsX, int pointsY, LinesVisual3D border, PointsVisual3D points)
		{
			Vector2 low = new Vector2(Math.Min(min.X, max.X), Math.Min(min.Y, max.Y));
			Vector2 high = new Vector2(Math.Max(min.X, max.X), Math.Max(min.Y, max.Y));

			if (pointsX < 2 || pointsY < 2)
			{
				points.Points.Clear();
				border.Points.Clear();
				return;
			}

			double gridX = (high.X - low.X) / (pointsX - 1);
			double gridY = (high.Y - low.Y) / (pointsY - 1);

			Point3DCollection grid = new Point3DCollection(pointsX * pointsY);

			for (int x = 0; x < pointsX; x++)
				for (int y = 0; y < pointsY; y++)
					grid.Add(new Point3D(low.X + x * gridX, low.Y + y * gridY, 0));

			points.Points.Clear();
			points.Points = grid;

			Point3DCollection outline = new Point3DCollection(8)
			{
				new Point3D(low.X, low.Y, 0),
				new Point3D(low.X, high.Y, 0),
				new Point3D(low.X, high.Y, 0),
				new Point3D(high.X, high.Y, 0),
				new Point3D(high.X, high.Y, 0),
				new Point3D(high.X, low.Y, 0),
				new Point3D(high.X, low.Y, 0),
				new Point3D(low.X, low.Y, 0),
			};

			border.Points.Clear();
			border.Points = outline;
		}
	}
}
