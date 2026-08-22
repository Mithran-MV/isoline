using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Clipper2Lib;
using Isoline.Gerber;
using Isoline.Toolpaths;
using Xunit;

namespace Isoline.Tests
{
	public class IsolationToolpathTests
	{
		private static PathsD Square(double size)
		{
			return new PathsD
			{
				new PathD
				{
					new PointD(0, 0),
					new PointD(size, 0),
					new PointD(size, size),
					new PointD(0, size),
				}
			};
		}

		[Fact]
		public void FirstPassSurroundsTheCopper()
		{
			IsolationOptions options = new IsolationOptions() { ToolDiameter = 0.2, Passes = 1 };

			List<PathsD> passes = IsolationToolpathGenerator.GenerateContours(Square(10), options);

			Assert.Single(passes);

			// the contour is the copper offset outwards by half the tool width
			double area = Math.Abs(Clipper.Area(passes[0]));
			double expected = 10 * 10 + 4 * 10 * 0.1 + Math.PI * 0.1 * 0.1;

			Assert.InRange(area, expected * 0.99, expected * 1.01);
		}

		[Fact]
		public void EachPassIsLargerThanTheLast()
		{
			IsolationOptions options = new IsolationOptions() { ToolDiameter = 0.2, Passes = 3, Stepover = 0.8 };

			List<PathsD> passes = IsolationToolpathGenerator.GenerateContours(Square(10), options);

			Assert.Equal(3, passes.Count);

			double previous = 0;

			foreach (PathsD pass in passes)
			{
				double area = Math.Abs(Clipper.Area(pass));
				Assert.True(area > previous, "each isolation pass must enclose more area than the last");
				previous = area;
			}
		}

		[Fact]
		public void NearbyIslandsMergeIntoOneContour()
		{
			// two squares 0.1 mm apart, cut with a 0.4 mm tool: the offsets overlap, so the
			// tool cannot fit between them and the result has to be a single outline
			PathsD copper = new PathsD
			{
				new PathD { new PointD(0, 0), new PointD(5, 0), new PointD(5, 5), new PointD(0, 5) },
				new PathD { new PointD(5.1, 0), new PointD(10, 0), new PointD(10, 5), new PointD(5.1, 5) },
			};

			var passes = IsolationToolpathGenerator.GenerateContours(copper, new IsolationOptions() { ToolDiameter = 0.4 });

			Assert.Single(passes[0]);
		}

		[Fact]
		public void GeneratesRunnableGCode()
		{
			IsolationOptions options = new IsolationOptions()
			{
				ToolDiameter = 0.2,
				CutDepth = -0.1,
				SafeHeight = 2,
				FeedRate = 250,
				PlungeRate = 60,
				SpindleSpeed = 12000,
			};

			List<string> gcode = IsolationToolpathGenerator.Generate(Square(10), options);

			Assert.Contains("G21 G90 G17 G94", gcode);
			Assert.Contains(gcode, l => l.StartsWith("S12000 M3", StringComparison.Ordinal));
			Assert.Contains("M5", gcode);
			Assert.Contains("M30", gcode);

			// it must reach the cut depth, and lift to the safe height before travelling
			Assert.Contains(gcode, l => l.Contains("Z-0.1"));
			Assert.Contains(gcode, l => l.Contains("G0 Z2"));

			// and it must parse back as valid G-code
			Isoline.GCode.GCodeFile file = Isoline.GCode.GCodeFile.FromList(gcode);

			Assert.True(file.ContainsMotion);
			Assert.Empty(file.Warnings);
		}

		[Fact]
		public void SpindleIsLeftAloneWhenSpeedIsZero()
		{
			List<string> gcode = IsolationToolpathGenerator.Generate(
				Square(10), new IsolationOptions() { SpindleSpeed = 0 });

			Assert.DoesNotContain(gcode, l => l.Trim().EndsWith("M3", StringComparison.Ordinal));
			Assert.DoesNotContain(gcode, l => l.Trim() == "M5");
		}

		[Fact]
		public void TravelOptimisationShortensRapidsWithoutChangingTheCut()
		{
			// eight pads laid out so the file order is deliberately the worst order to cut in
			PathsD copper = new PathsD();

			for (int i = 0; i < 8; i++)
			{
				double x = (i % 2 == 0) ? i * 5 : 40 - i * 5;
				PathD pad = new PathD();
				Aperture.AddCircle(pad, x, i * 2, 0.5, 24);
				copper.Add(pad);
			}

			IsolationOptions optimised = new IsolationOptions() { OptimiseTravel = true };
			IsolationOptions asIs = new IsolationOptions() { OptimiseTravel = false };

			double optimisedTravel = RapidLength(IsolationToolpathGenerator.Generate(copper, optimised));
			double naiveTravel = RapidLength(IsolationToolpathGenerator.Generate(copper, asIs));

			Assert.True(optimisedTravel <= naiveTravel,
				$"optimised travel {optimisedTravel:0.##} should not exceed naive {naiveTravel:0.##}");
		}

		private static double RapidLength(List<string> gcode)
		{
			Isoline.GCode.GCodeFile file = Isoline.GCode.GCodeFile.FromList(gcode);
			double total = 0;

			foreach (var command in file.Toolpath)
			{
				var line = command as Isoline.GCode.GCodeCommands.Line;

				if (line != null && line.Rapid && line.StartValid)
					total += (line.End - line.Start).Magnitude;
			}

			return total;
		}

		[Fact]
		public void VBitWidthMatchesTheGeometry()
		{
			// a 90 degree V-bit at 0.1 mm deep cuts a 0.2 mm wide trench
			Assert.Equal(0.2, IsolationOptions.VBitWidth(90, 0.1), 6);

			// a 30 degree bit with a 0.1 mm tip flat at 0.1 mm deep
			Assert.Equal(0.1 + 2 * 0.1 * Math.Tan(15 * Math.PI / 180), IsolationOptions.VBitWidth(30, 0.1, 0.1), 6);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(-1)]
		public void RejectsImpossibleToolDiameter(double diameter)
		{
			Assert.Throws<ArgumentException>(() =>
				IsolationToolpathGenerator.GenerateContours(Square(10), new IsolationOptions() { ToolDiameter = diameter }));
		}
	}

	/// <summary>
	/// Which way round the tool goes is not cosmetic on copper: climb milling has each
	/// tooth enter at full chip thickness and leave at zero, which is what leaves a trace
	/// edge that does not need deburring. Before this was configurable the generator
	/// emitted whatever winding Clipper produced, which is conventional in both cases.
	/// </summary>
	public class CutDirectionTests
	{
		/// <summary>A ring of copper: an outer boundary with a gap enclosed inside it.</summary>
		private static PathsD Ring()
		{
			PathsD outer = new PathsD
			{
				new PathD { new PointD(0, 0), new PointD(20, 0), new PointD(20, 20), new PointD(0, 20) },
			};

			PathsD hole = new PathsD
			{
				new PathD { new PointD(6, 6), new PointD(14, 6), new PointD(14, 14), new PointD(6, 14) },
			};

			return Clipper.Difference(outer, hole, FillRule.NonZero);
		}

		/// <summary>Reads the cut moves of each contour back out of the program.</summary>
		private static List<List<PointD>> ContoursFrom(List<string> gcode)
		{
			List<List<PointD>> contours = new List<List<PointD>>();
			List<PointD> current = null;

			foreach (string line in gcode)
			{
				if (line.StartsWith("G0 X", StringComparison.Ordinal))
				{
					current = new List<PointD>();
					contours.Add(current);
					continue;
				}

				if (current == null || !line.StartsWith("G1 X", StringComparison.Ordinal))
					continue;

				Match m = Regex.Match(line, @"X(-?[\d.]+) Y(-?[\d.]+)");

				if (m.Success)
				{
					current.Add(new PointD(
						double.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture),
						double.Parse(m.Groups[2].Value, CultureInfo.InvariantCulture)));
				}
			}

			return contours.FindAll(c => c.Count >= 3);
		}

		private static List<double> SignedAreas(CutDirection direction)
		{
			List<string> gcode = IsolationToolpathGenerator.Generate(
				Ring(),
				new IsolationOptions() { ToolDiameter = 0.4, Passes = 1, Direction = direction });

			return ContoursFrom(gcode).ConvertAll(c => Clipper.Area(new PathD(c)));
		}

		[Fact]
		public void ProducesBothAnOuterRingAndAnEnclosedOne()
		{
			// the fixture is only meaningful if it exercises both cases
			List<double> areas = SignedAreas(CutDirection.Conventional);

			Assert.Equal(2, areas.Count);
			Assert.Contains(areas, a => Math.Abs(a) > 300);
			Assert.Contains(areas, a => Math.Abs(a) < 100);
		}

		[Fact]
		public void ConventionalRunsRoundCopperTheWayClipperOrdersIt()
		{
			// outer ring counter-clockwise, enclosed gap clockwise
			List<double> areas = SignedAreas(CutDirection.Conventional);

			Assert.True(areas.Find(a => Math.Abs(a) > 300) > 0, "the outer ring should be counter-clockwise");
			Assert.True(areas.Find(a => Math.Abs(a) < 100) < 0, "the enclosed gap should be clockwise");
		}

		[Fact]
		public void ClimbReversesBoth()
		{
			// clockwise around an island of copper, counter-clockwise inside a gap it surrounds
			List<double> areas = SignedAreas(CutDirection.Climb);

			Assert.True(areas.Find(a => Math.Abs(a) > 300) < 0, "the outer ring should be clockwise");
			Assert.True(areas.Find(a => Math.Abs(a) < 100) > 0, "the enclosed gap should be counter-clockwise");
		}

		[Fact]
		public void DirectionChangesNothingButTheDirection()
		{
			// the same copper comes off either way; only the order of the points differs
			List<double> conventional = SignedAreas(CutDirection.Conventional);
			List<double> climb = SignedAreas(CutDirection.Climb);

			Assert.Equal(conventional.Count, climb.Count);

			for (int i = 0; i < conventional.Count; i++)
				Assert.Equal(Math.Abs(conventional[i]), Math.Abs(climb[i]), 6);
		}

		[Fact]
		public void DefaultsToConventional()
		{
			// an existing setup that has been dialled in must not change underneath anyone
			Assert.Equal(CutDirection.Conventional, new IsolationOptions().Direction);
		}
	}
}
