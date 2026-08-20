using System;
using System.Linq;
using Clipper2Lib;
using Isoline.Gerber;
using Xunit;

namespace Isoline.Tests
{
	public class GerberParserTests
	{
		/// <summary>
		/// A minimal but realistic RS-274X layer: metric, 3.5 format, one round aperture
		/// flashed twice with a track drawn between the two flashes.
		/// </summary>
		private const string TwoPadsAndATrack = @"
G04 test layer*
%FSLAX35Y35*%
%MOMM*%
%ADD10C,1.00000*%
%ADD11C,0.25000*%
D10*
X0Y0D03*
X1000000Y0D03*
D11*
X0Y0D02*
X1000000Y0D01*
M02*
";

		[Fact]
		public void ParsesFlashesAndTracks()
		{
			GerberDocument doc = new GerberParser().Parse(TwoPadsAndATrack);

			Assert.False(doc.IsEmpty);
			Assert.Empty(doc.Warnings);

			// two 1 mm pads centred at (0,0) and (10,0): x from -0.5 to 10.5, y from -0.5 to 0.5
			Assert.Equal(-0.5, doc.Min.X, 2);
			Assert.Equal(10.5, doc.Max.X, 2);
			Assert.Equal(-0.5, doc.Min.Y, 2);
			Assert.Equal(0.5, doc.Max.Y, 2);
		}

		[Fact]
		public void CoordinateFormatScalesCorrectly()
		{
			// X1000000 in 3.5 format is 10.00000 mm
			GerberFormat format = new GerberFormat() { IntegerDigits = 3, DecimalDigits = 5, UnitScale = 1 };

			Assert.Equal(10.0, format.ToMillimetres("1000000"), 6);
			Assert.Equal(-2.5, format.ToMillimetres("-250000"), 6);
			Assert.Equal(0.0001, format.ToMillimetres("10"), 6);
		}

		[Fact]
		public void InchFilesAreConvertedToMillimetres()
		{
			GerberFormat format = new GerberFormat() { IntegerDigits = 2, DecimalDigits = 4, UnitScale = 25.4 };

			Assert.Equal(25.4, format.ToMillimetres("10000"), 6);
		}

		[Fact]
		public void AreaOfASingleFlashMatchesTheAperture()
		{
			string single = @"
%FSLAX35Y35*%
%MOMM*%
%ADD10C,2.00000*%
D10*
X0Y0D03*
M02*
";
			GerberDocument doc = new GerberParser().Parse(single);

			// a 2 mm circle has an area of pi mm^2; the polygon approximation is slightly under
			double area = Math.Abs(Clipper.Area(doc.Copper));

			Assert.InRange(area, Math.PI * 0.99, Math.PI);
		}

		[Fact]
		public void RectangularApertureFlashesAsARectangle()
		{
			string rect = @"
%FSLAX35Y35*%
%MOMM*%
%ADD10R,2.00000X1.00000*%
D10*
X0Y0D03*
M02*
";
			GerberDocument doc = new GerberParser().Parse(rect);

			Assert.Equal(2.0, Math.Abs(Clipper.Area(doc.Copper)), 6);

			Assert.Equal(-1.0, doc.Min.X, 6);
			Assert.Equal(0.5, doc.Max.Y, 6);
		}

		[Fact]
		public void RegionsAreFilled()
		{
			// a 10x10 mm filled square drawn as a G36 region
			string region = @"
%FSLAX35Y35*%
%MOMM*%
G36*
X0Y0D02*
X1000000Y0D01*
X1000000Y1000000D01*
X0Y1000000D01*
X0Y0D01*
G37*
M02*
";
			GerberDocument doc = new GerberParser().Parse(region);

			Assert.Equal(100.0, Math.Abs(Clipper.Area(doc.Copper)), 4);
		}

		[Fact]
		public void ClearPolarityCutsHolesInCopper()
		{
			// a 10x10 filled square with a 2 mm circle cleared out of the middle
			string withHole = @"
%FSLAX35Y35*%
%MOMM*%
G36*
X0Y0D02*
X1000000Y0D01*
X1000000Y1000000D01*
X0Y1000000D01*
X0Y0D01*
G37*
%LPC*%
%ADD10C,2.00000*%
D10*
X500000Y500000D03*
M02*
";
			GerberDocument doc = new GerberParser().Parse(withHole);
			double area = Math.Abs(Clipper.Area(doc.Copper));

			Assert.InRange(area, 100 - Math.PI, 100 - Math.PI * 0.99);
		}

		[Fact]
		public void FullCircleArcClosesInsteadOfCollapsing()
		{
			// start == end with G75 means a full circle, not a zero length move
			string circle = @"
%FSLAX35Y35*%
%MOMM*%
%ADD10C,0.20000*%
D10*
G75*
G03*
X500000Y0D02*
X500000Y0I-500000J0D01*
M02*
";
			GerberDocument doc = new GerberParser().Parse(circle);

			// a 5 mm radius ring stroked with a 0.2 mm aperture spans -5.1 .. 5.1
			Assert.Equal(-5.1, doc.Min.X, 1);
			Assert.Equal(5.1, doc.Max.X, 1);
			Assert.Equal(-5.1, doc.Min.Y, 1);
			Assert.Equal(5.1, doc.Max.Y, 1);
		}

		[Fact]
		public void UnknownApertureMacroIsReportedRatherThanSilentlyDropped()
		{
			string macro = @"
%FSLAX35Y35*%
%MOMM*%
%AMROUNDRECT*
21,1,1,1,0,0,0*%
%ADD10ROUNDRECT,1.00000*%
M02*
";
			GerberDocument doc = new GerberParser().Parse(macro);

			Assert.Contains(doc.Warnings, w => w.Contains("macro", StringComparison.OrdinalIgnoreCase));
		}

		[Fact]
		public void EmptyLayerIsReportedNotThrown()
		{
			GerberDocument doc = new GerberParser().Parse("%FSLAX35Y35*%\n%MOMM*%\nM02*\n");

			Assert.True(doc.IsEmpty);
			Assert.NotEmpty(doc.Warnings);
		}
	}
}
