using System;
using System.Collections.Generic;

namespace Isoline.GCode
{
	/// <summary>
	/// Removes single bad probe readings from a height map.
	/// <para>
	/// A probe cycle that touches a chip, a solder blob or a piece of swarf reports a height
	/// that can be tenths of a millimetre off. Because the toolpath is wrapped onto the map,
	/// one such point pulls a whole neighbourhood of the cut with it - on a V-bit isolation
	/// job that is the difference between a clean trace and a cut through the copper.
	/// </para>
	/// <para>
	/// Detection uses the median absolute deviation (MAD) of the 8-neighbourhood rather than
	/// mean/standard deviation, because the mean is itself dragged around by the outlier it
	/// is supposed to find. A point is rejected when it sits further than
	/// <c>threshold</c> * 1.4826 * MAD from the local median; 1.4826 makes the MAD a
	/// consistent estimator of the standard deviation for normally distributed data, so a
	/// threshold of 3.5 is "3.5 sigma" in the usual sense.
	/// </para>
	/// </summary>
	public static class OutlierFilter
	{
		/// <summary>
		/// Replaces outlying probe points with their local median.
		/// </summary>
		/// <returns>The number of points replaced.</returns>
		public static int Apply(HeightMap map, double threshold = 3.5)
		{
			if (map == null)
				throw new ArgumentNullException("map");

			if (threshold <= 0)
				throw new ArgumentOutOfRangeException("threshold", "threshold must be positive");

			// Work off a snapshot: replacing points in place would let a corrected value
			// influence the verdict on its neighbours, which makes the result depend on
			// iteration order.
			double?[,] original = (double?[,])map.Points.Clone();
			int replaced = 0;

			for (int x = 0; x < map.SizeX; x++)
			{
				for (int y = 0; y < map.SizeY; y++)
				{
					if (!original[x, y].HasValue)
						continue;

					List<double> neighbours = Neighbourhood(original, map.SizeX, map.SizeY, x, y);

					if (neighbours.Count < 3)
						continue;   // not enough context to judge

					double[] sorted = neighbours.ToArray();
					Array.Sort(sorted);

					double median = HeightMapStatistics.Median_(sorted);

					double[] deviations = new double[sorted.Length];
					for (int i = 0; i < sorted.Length; i++)
						deviations[i] = Math.Abs(sorted[i] - median);
					Array.Sort(deviations);

					double mad = HeightMapStatistics.Median_(deviations);

					// A perfectly flat neighbourhood has MAD 0; anything other than an exact
					// match would then look infinitely deviant, so fall back to a comparison
					// against the map's own scale.
					double scale = mad > 1e-9
						? 1.4826 * mad
						: 1e-9;

					if (Math.Abs(original[x, y].Value - median) > threshold * scale)
					{
						map.SetPointQuiet(x, y, median);
						replaced++;
					}
				}
			}

			if (replaced > 0)
				map.RecalculateBounds();

			return replaced;
		}

		private static List<double> Neighbourhood(double?[,] points, int sizeX, int sizeY, int x, int y)
		{
			List<double> result = new List<double>(8);

			for (int dx = -1; dx <= 1; dx++)
			{
				for (int dy = -1; dy <= 1; dy++)
				{
					if (dx == 0 && dy == 0)
						continue;

					int nx = x + dx;
					int ny = y + dy;

					if (nx < 0 || ny < 0 || nx >= sizeX || ny >= sizeY)
						continue;

					if (points[nx, ny].HasValue)
						result.Add(points[nx, ny].Value);
				}
			}

			return result;
		}
	}
}
