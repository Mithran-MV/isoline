using Isoline.Expressions;
using Isoline.Util;
using System;
using System.Collections.Generic;
using System.Xml;

namespace Isoline.GCode
{
	public class HeightMap
	{
		public double?[,] Points { get; private set; }
		public int SizeX { get; private set; }
		public int SizeY { get; private set; }

		public int Progress { get { return TotalPoints - NotProbed.Count; } }
		public int TotalPoints { get { return SizeX * SizeY; } }

		public List<Tuple<int, int>> NotProbed { get; private set; } = new List<Tuple<int, int>>();

		public Vector2 Min { get; private set; }
		public Vector2 Max { get; private set; }

		public Vector2 Delta { get { return Max - Min; } }

		public double MinHeight { get; private set; } = double.MaxValue;
		public double MaxHeight { get; private set; } = double.MinValue;

		public event Action MapUpdated;

		public double GridX { get { return (Max.X - Min.X) / (SizeX - 1); } }
		public double GridY { get { return (Max.Y - Min.Y) / (SizeY - 1); } }


		public HeightMap(double gridSize, Vector2 min, Vector2 max)
		{
			if (min.X == max.X || min.Y == max.Y)
				throw new Exception("Height map can't be infinitely narrow");

			if (gridSize <= 0)
				throw new Exception("Grid size must be positive");

			// Normalise the corners *before* sizing the grid. Upstream swapped them after
			// computing the point counts, so a map defined from its top-right corner to its
			// bottom-left produced a negative count and failed with "must have at least 4
			// points" - which is exactly how someone probing a toolpath that runs in the
			// negative direction would naturally enter it.
			if (max.X < min.X)
			{
				double a = min.X;
				min.X = max.X;
				max.X = a;
			}

			if (max.Y < min.Y)
			{
				double a = min.Y;
				min.Y = max.Y;
				max.Y = a;
			}

			int pointsX = (int)Math.Ceiling((max.X - min.X) / gridSize) + 1;
			int pointsY = (int)Math.Ceiling((max.Y - min.Y) / gridSize) + 1;

			if (pointsX < 2 || pointsY < 2)
				throw new Exception("Height map must have at least 4 points");

			Points = new double?[pointsX, pointsY];

			Min = min;
			Max = max;

			SizeX = pointsX;
			SizeY = pointsY;


			for (int x = 0; x < SizeX; x++)
			{
				for (int y = 0; y < SizeY; y++)
					NotProbed.Add(new Tuple<int, int>(x, y));
			}
		}

		/// <summary>
		/// Interpolation scheme used by <see cref="InterpolateZ"/>. Bilinear matches the
		/// original OpenCNCPilot behaviour and stays the default; bicubic produces a C1
		/// continuous surface which avoids the faceted ridges bilinear leaves on coarse grids.
		/// </summary>
		public InterpolationMode Interpolation { get; set; } = InterpolationMode.Bilinear;

		public double InterpolateZ(double x, double y)
		{
			if (Interpolation == InterpolationMode.Bicubic)
				return InterpolateZBicubic(x, y);

			return InterpolateZBilinear(x, y);
		}

		private double InterpolateZBilinear(double x, double y)
		{
			if (x > Max.X || x < Min.X || y > Max.Y || y < Min.Y)
				return MaxHeight;

			x -= Min.X;
			y -= Min.Y;

			x /= GridX;
			y /= GridY;

			int iLX = (int)Math.Floor(x);   //lower integer part
			int iLY = (int)Math.Floor(y);

			int iHX = (int)Math.Ceiling(x); //upper integer part
			int iHY = (int)Math.Ceiling(y);

			double fX = x - iLX;             //fractional part
			double fY = y - iLY;

			double linUpper = Points[iHX, iHY].Value * fX + Points[iLX, iHY].Value * (1 - fX);       //linear immediates
			double linLower = Points[iHX, iLY].Value * fX + Points[iLX, iLY].Value * (1 - fX);

			return linUpper * fY + linLower * (1 - fY);     //bilinear result
		}

		public Vector2 GetCoordinates(int x, int y)
		{
			return new Vector2(x * (Delta.X / (SizeX - 1)) + Min.X, y * (Delta.Y / (SizeY - 1)) + Min.Y);
		}

		public Vector2 GetCoordinates(Tuple<int, int> index)
		{
			return GetCoordinates(index.Item1, index.Item2);
		}

		private HeightMap()
		{

		}

		public void AddPoint(int x, int y, double height)
		{
			Points[x, y] = height;

			if (height > MaxHeight)
				MaxHeight = height;
			if (height < MinHeight)
				MinHeight = height;

			if (MapUpdated != null)
				MapUpdated();
		}

		public static HeightMap Load(string path)
		{
			HeightMap map = new HeightMap();

			XmlReader r = XmlReader.Create(path);

			while (r.Read())
			{
				if (!r.IsStartElement())
					continue;

				switch (r.Name)
				{
					case "heightmap":
						map.Min = new Vector2(double.Parse(r["MinX"], Constants.DecimalParseFormat), double.Parse(r["MinY"], Constants.DecimalParseFormat));
						map.Max = new Vector2(double.Parse(r["MaxX"], Constants.DecimalParseFormat), double.Parse(r["MaxY"], Constants.DecimalParseFormat));
						map.SizeX = int.Parse(r["SizeX"]);
						map.SizeY = int.Parse(r["SizeY"]);
						map.Points = new double?[map.SizeX, map.SizeY];
						break;
					case "point":
						int x = int.Parse(r["X"]), y = int.Parse(r["Y"]);
						double height = double.Parse(r.ReadInnerXml(), Constants.DecimalParseFormat);

						map.Points[x, y] = height;

						if (height > map.MaxHeight)
							map.MaxHeight = height;
						if (height < map.MinHeight)
							map.MinHeight = height;

						break;
				}
			}

			r.Dispose();

			for (int x = 0; x < map.SizeX; x++)
			{
				for (int y = 0; y < map.SizeY; y++)
					if (!map.Points[x, y].HasValue)
						map.NotProbed.Add(new Tuple<int, int>(x, y));
			}

			return map;
		}

		public void Save(string path)
		{
			XmlWriterSettings set = new XmlWriterSettings();
			set.Indent = true;
			XmlWriter w = XmlWriter.Create(path, set);
			w.WriteStartDocument();
			w.WriteStartElement("heightmap");
			w.WriteAttributeString("MinX", Min.X.ToString(Constants.DecimalParseFormat));
			w.WriteAttributeString("MinY", Min.Y.ToString(Constants.DecimalParseFormat));
			w.WriteAttributeString("MaxX", Max.X.ToString(Constants.DecimalParseFormat));
			w.WriteAttributeString("MaxY", Max.Y.ToString(Constants.DecimalParseFormat));
			w.WriteAttributeString("SizeX", SizeX.ToString(Constants.DecimalParseFormat));
			w.WriteAttributeString("SizeY", SizeY.ToString(Constants.DecimalParseFormat));

			for (int x = 0; x < SizeX; x++)
			{
				for (int y = 0; y < SizeY; y++)
				{
					if (!Points[x, y].HasValue)
						continue;

					w.WriteStartElement("point");
					w.WriteAttributeString("X", x.ToString());
					w.WriteAttributeString("Y", y.ToString());
					w.WriteString(Points[x, y].Value.ToString(Constants.DecimalParseFormat));
					w.WriteEndElement();
				}
			}
			w.WriteEndElement();
			w.Close();
		}

		/// <summary>
		/// Bicubic (Catmull-Rom) interpolation. Falls back to bilinear near the border where
		/// the 4x4 support window would leave the grid, so behaviour at the edges is unchanged.
		/// </summary>
		private double InterpolateZBicubic(double x, double y)
		{
			if (x > Max.X || x < Min.X || y > Max.Y || y < Min.Y)
				return MaxHeight;

			double gx = (x - Min.X) / GridX;
			double gy = (y - Min.Y) / GridY;

			int ix = (int)Math.Floor(gx);
			int iy = (int)Math.Floor(gy);

			if (ix < 1 || iy < 1 || ix > SizeX - 3 || iy > SizeY - 3)
				return InterpolateZBilinear(x, y);

			double fx = gx - ix;
			double fy = gy - iy;

			double[] col = new double[4];

			for (int j = 0; j < 4; j++)
			{
				double[] row = new double[4];

				for (int i = 0; i < 4; i++)
				{
					double? v = Points[ix - 1 + i, iy - 1 + j];

					if (!v.HasValue)
						return InterpolateZBilinear(x, y);  // hole in the support window

					row[i] = v.Value;
				}

				col[j] = CatmullRom(row[0], row[1], row[2], row[3], fx);
			}

			return CatmullRom(col[0], col[1], col[2], col[3], fy);
		}

		private static double CatmullRom(double p0, double p1, double p2, double p3, double t)
		{
			double t2 = t * t;
			double t3 = t2 * t;

			return 0.5 * ((2 * p1)
				+ (-p0 + p2) * t
				+ (2 * p0 - 5 * p1 + 4 * p2 - p3) * t2
				+ (-p0 + 3 * p1 - 3 * p2 + p3) * t3);
		}

		/// <summary>
		/// Every probed height in the map, row-major. Un-probed cells are skipped.
		/// </summary>
		public IEnumerable<double> ProbedHeights()
		{
			for (int x = 0; x < SizeX; x++)
				for (int y = 0; y < SizeY; y++)
					if (Points[x, y].HasValue)
						yield return Points[x, y].Value;
		}

		/// <summary>
		/// Summary statistics over the probed points, used by the UI to report how warped
		/// the stock actually is and to flag maps that look like a bad probe run.
		/// </summary>
		public HeightMapStatistics GetStatistics()
		{
			return HeightMapStatistics.FromHeights(ProbedHeights());
		}

		/// <summary>
		/// Discards probe points that sit more than <paramref name="threshold"/> median absolute
		/// deviations away from the local median, then re-fills them from their neighbours.
		/// A single bad contact (chip under the probe, dirty pad) otherwise drags a whole
		/// region of the compensated toolpath with it.
		/// </summary>
		/// <returns>The number of points that were replaced.</returns>
		public int RejectOutliers(double threshold = 3.5)
		{
			return OutlierFilter.Apply(this, threshold);
		}

		/// <summary>
		/// Replaces the height at a grid index without raising <see cref="MapUpdated"/> for
		/// every single point; used by bulk operations such as outlier rejection.
		/// </summary>
		internal void SetPointQuiet(int x, int y, double height)
		{
			Points[x, y] = height;
		}

		/// <summary>
		/// Recomputes <see cref="MinHeight"/>/<see cref="MaxHeight"/> after a bulk edit.
		/// </summary>
		internal void RecalculateBounds()
		{
			MinHeight = double.MaxValue;
			MaxHeight = double.MinValue;

			foreach (double h in ProbedHeights())
			{
				if (h > MaxHeight)
					MaxHeight = h;
				if (h < MinHeight)
					MinHeight = h;
			}

			if (MapUpdated != null)
				MapUpdated();
		}

		public void FillWithTestPattern(string pattern)
		{
			Expression expr = Expression.Parse(pattern);

			for (int x = 0; x < SizeX; x++)
			{
				for (int y = 0; y < SizeY; y++)
				{
					Dictionary<string, double> variables = new Dictionary<string, double>();

					variables.Add("X", (x * (Max.X - Min.X)) / (SizeX - 1) + Min.X);
					variables.Add("Y", (y * (Max.Y - Min.Y)) / (SizeY - 1) + Min.Y);

					AddPoint(x, y, expr.GetValue(variables));
				}
			}
		}
	}
}
