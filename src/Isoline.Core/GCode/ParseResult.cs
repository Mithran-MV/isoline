using System.Collections.Generic;
using Isoline.GCode.GCodeCommands;

namespace Isoline.GCode
{
	/// <summary>
	/// The output of one parse: the commands, and anything the parser wanted to say about
	/// the file. Returned rather than left in static fields so that two parses running at
	/// once cannot see each other's work.
	/// </summary>
	public class ParseResult
	{
		public List<Command> Commands { get; private set; }
		public List<string> Warnings { get; private set; }

		public ParseResult(List<Command> commands, List<string> warnings)
		{
			Commands = commands;
			Warnings = warnings;
		}
	}
}
