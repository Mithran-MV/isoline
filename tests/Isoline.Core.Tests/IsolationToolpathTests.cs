using System;
using System.Collections.Generic;
using System.Linq;
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
}
