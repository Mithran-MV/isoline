using System;
using System.IO;
using Isoline.Gerber;
using Isoline.Toolpaths;
using Xunit;

namespace Isoline.Tests
{
	/// <summary>
	/// Parses the sample shipped in docs/samples. It exists so the file people are told to
	/// try the importer with cannot quietly stop working.
	/// </summary>
	public class SampleFileTests
	{
		private static GerberDocument Sample()
		{
			string path = Path.Combine(AppContext.BaseDirectory, "samples", "sample-board.gbr");

			Assert.True(File.Exists(path), "sample-board.gbr was not copied to the test output");

			return GerberParser.ParseFile(path);
		}

		[Fact]
		public void SampleBoardParsesToTheDocumentedSize()
		{
			GerberDocument document = Sample();

			Assert.Empty(document.Warnings);

			// the ground pour runs from (-5, -5) to (15, 5) mm
			Assert.Equal(20, document.Size.X, 3);
			Assert.Equal(10, document.Size.Y, 3);
			Assert.Equal(-5, document.Min.X, 3);
			Assert.Equal(5, document.Max.Y, 3);
		}

		[Fact]
		public void ClearedRingCutsAHoleInThePour()
		{
			GerberDocument document = Sample();

			// The %LPC ring removes copper around the left pad, and the pad and track are
			// drawn back on top of it - so the result is the pour's outline plus one hole,
			// with the pad still joined to the pour through the track that crosses the ring.
			Assert.Equal(2, document.Copper.Count);

			double outer = 0, holes = 0;

			foreach (var contour in document.Copper)
			{
				double area = Clipper2Lib.Clipper.Area(contour);

				if (area > 0)
					outer += area;
				else
					holes += -area;
			}

			Assert.True(holes > 0, "the cleared ring should have left a hole in the copper");

			// the pour is 20 x 10 mm; the hole is what is left of a 2.6 mm ring after the
			// 1.6 mm pad and the track were drawn back over it
			Assert.InRange(outer, 195, 200);
			Assert.InRange(holes, 1.0, 4.0);
		}

		[Fact]
		public void SampleBoardGeneratesARunnableProgram()
		{
			GerberDocument document = Sample();

			var gcode = IsolationToolpathGenerator.Generate(document.Copper, new IsolationOptions());

			Isoline.GCode.GCodeFile file = Isoline.GCode.GCodeFile.FromList(gcode);

			Assert.True(file.ContainsMotion);
			Assert.Empty(file.Warnings);
		}
	}
}
