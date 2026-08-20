using System;
using System.Collections.Generic;
using System.Linq;

namespace Isoline.GCode
{
	/// <summary>
	/// Summary of how flat (or not) a probed surface is. Surfaced in the UI so the operator
	/// can judge a probe run before committing a cut to it.
	/// </summary>
	public class HeightMapStatistics
	{
		public int Count { get; private set; }
		public double Min { get; private set; }
		public double Max { get; private set; }
		public double Mean { get; private set; }
		public double Median { get; private set; }

		/// <summary>Population standard deviation of the probed heights.</summary>
		public double StandardDeviation { get; private set; }

		/// <summary>Peak to peak height difference across the probed area.</summary>
		public double Range { get { return Max - Min; } }

		public static HeightMapStatistics FromHeights(IEnumerable<double> heights)
		{
			double[] values = heights.ToArray();

			if (values.Length == 0)
				return new HeightMapStatistics();

			Array.Sort(values);

			double mean = values.Average();
			double variance = values.Sum(v => (v - mean) * (v - mean)) / values.Length;

			return new HeightMapStatistics()
			{
				Count = values.Length,
				Min = values[0],
				Max = values[values.Length - 1],
				Mean = mean,
				Median = Median_(values),
				StandardDeviation = Math.Sqrt(variance),
			};
		}

		/// <param name="sorted">Must already be sorted ascending.</param>
		internal static double Median_(double[] sorted)
		{
			if (sorted.Length == 0)
				return 0;

			int mid = sorted.Length / 2;

			return sorted.Length % 2 == 0
				? (sorted[mid - 1] + sorted[mid]) / 2.0
				: sorted[mid];
		}
	}
}
