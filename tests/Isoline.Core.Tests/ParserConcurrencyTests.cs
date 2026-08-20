using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Isoline.GCode;
using Xunit;

namespace Isoline.Tests
{
	/// <summary>
	/// The parser keeps its modal state, command list and warning list in statics. That was
	/// safe while only one thread ever parsed, but the sender parses on its worker thread
	/// while the interface may be parsing another file - and the two used to interleave into
	/// a single command list.
	/// </summary>
	public class ParserConcurrencyTests
	{
		private static string[] Program(int index, int lines)
		{
			List<string> program = new List<string> { "G21 G90", "G0 X0 Y0 Z0" };

			for (int i = 0; i < lines; i++)
				program.Add($"G1 X{index}.{i:000} Y{index} F100");

			return program.ToArray();
		}

		[Fact]
		public void ParallelParsesDoNotContaminateEachOther()
		{
			const int parsers = 8;
			const int lines = 200;

			GCodeFile[] results = new GCodeFile[parsers];

			Parallel.For(0, parsers, i =>
			{
				results[i] = GCodeFile.FromList(Program(i + 1, lines));
			});

			for (int i = 0; i < parsers; i++)
			{
				// one rapid to the origin plus one cutting move per generated line; the
				// "G21 G90" line sets modal state and produces no command. A contaminated
				// parse produces some multiple of this.
				Assert.Equal(lines + 1, results[i].Toolpath.Count);

				// and every cutting move belongs to this program, not a neighbour's
				double expectedY = i + 1;

				foreach (var line in results[i].Toolpath.OfType<Isoline.GCode.GCodeCommands.Line>())
				{
					if (line.Rapid || !line.StartValid)
						continue;

					Assert.Equal(expectedY, line.End.Y, 6);
				}
			}
		}

		[Fact]
		public void ParallelHeightMapApplicationIsStable()
		{
			// the failure this guards against showed up as a height map that had apparently
			// not been applied at all, because another parse had replaced the toolpath
			Isoline.GCode.HeightMap map = new HeightMap(2.5,
				new Isoline.Util.Vector2(0, 0), new Isoline.Util.Vector2(10, 10));

			for (int x = 0; x < map.SizeX; x++)
				for (int y = 0; y < map.SizeY; y++)
					map.AddPoint(x, y, 0.1);

			map.NotProbed.Clear();

			Parallel.For(0, 16, i =>
			{
				GCodeFile file = GCodeFile.FromList(new[]
				{
					"G21 G90",
					"G0 X1 Y1 Z0",
					"G1 X9 Y9 Z-0.05 F100",
				});

				GCodeFile compensated = file.ArcsToLines(1).Split(1).ApplyHeightMap(map);

				double lowest = compensated.Toolpath
					.OfType<Isoline.GCode.GCodeCommands.Line>()
					.Where(m => m.StartValid)
					.Min(m => m.End.Z);

				Assert.Equal(0.05, lowest, 6);
			});
		}
	}
}
