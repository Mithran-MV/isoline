using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Isoline.GCode;
using Isoline.Util;
using Xunit;

namespace Isoline.Tests
{
	public class HeightMapTests
	{
		/// <summary>A fully probed map over [0,10]x[0,10] whose height follows f(x, y).</summary>
		private static HeightMap Probed(Func<double, double, double> f, double gridSize = 2.5)
		{
			HeightMap map = new HeightMap(gridSize, new Vector2(0, 0), new Vector2(10, 10));

			for (int x = 0; x < map.SizeX; x++)
			{
				for (int y = 0; y < map.SizeY; y++)
				{
					Vector2 p = map.GetCoordinates(x, y);
					map.AddPoint(x, y, f(p.X, p.Y));
				}
			}

			map.NotProbed.Clear();
			return map;
		}

		[Fact]
		public void GridIsSizedToCoverTheRequestedArea()
		{
			HeightMap map = new HeightMap(2.5, new Vector2(0, 0), new Vector2(10, 10));

			Assert.Equal(5, map.SizeX);
			Assert.Equal(5, map.SizeY);
			Assert.Equal(25, map.TotalPoints);
			Assert.Equal(2.5, map.GridX, 9);
		}

		[Fact]
		public void ReversedCornersAreNormalised()
		{
			HeightMap map = new HeightMap(2.5, new Vector2(10, 10), new Vector2(0, 0));

			Assert.Equal(0, map.Min.X, 9);
			Assert.Equal(10, map.Max.X, 9);
		}








		[Fact]
		public void SaveAndLoadRoundTrips()
		{
			HeightMap map = Probed((x, y) => 0.01 * x * y);
			string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".hmap");

			try
			{
				map.Save(path);
				HeightMap loaded = HeightMap.Load(path);

				Assert.Equal(map.SizeX, loaded.SizeX);
				Assert.Equal(map.SizeY, loaded.SizeY);
				Assert.Equal(map.Min.X, loaded.Min.X, 9);
				Assert.Equal(map.Max.Y, loaded.Max.Y, 9);

				for (int x = 0; x < map.SizeX; x++)
					for (int y = 0; y < map.SizeY; y++)
						Assert.Equal(map.Points[x, y].Value, loaded.Points[x, y].Value, 6);
			}
			finally
			{
				if (File.Exists(path))
					File.Delete(path);
			}
		}

		[Fact]
		public void TestPatternUsesTheBuiltInExpressionEvaluator()
		{
			HeightMap map = new HeightMap(2.5, new Vector2(0, 0), new Vector2(10, 10));

			map.FillWithTestPattern("X / 10 + Y / 20");

			Assert.Equal(0, map.Points[0, 0].Value, 9);
			Assert.Equal(1.5, map.Points[4, 4].Value, 9);
		}

		[Fact]
		public void ApplyingAHeightMapShiftsTheToolpathOntoTheSurface()
		{
			HeightMap map = Probed((x, y) => 0.1, 2.5);   // a board sitting 0.1 mm high

			GCodeFile file = GCodeFile.FromList(new[]
			{
				"G21 G90",
				"G0 X1 Y1 Z0",
				"G1 X9 Y9 Z-0.05 F100",
			});

			GCodeFile compensated = file.ArcsToLines(1).Split(1).ApplyHeightMap(map);

			// every commanded Z should have been lifted by the height of the surface
			double lowest = compensated.Toolpath
				.OfType<Isoline.GCode.GCodeCommands.Line>()
				.Where(m => m.StartValid)
				.Min(m => m.End.Z);

			Assert.Equal(-0.05 + 0.1, lowest, 6);
		}

		[Fact]
		public void NarrowMapsAreRejected()
		{
			Assert.ThrowsAny<Exception>(() => new HeightMap(1, new Vector2(0, 0), new Vector2(0, 10)));
		}
	}
}
