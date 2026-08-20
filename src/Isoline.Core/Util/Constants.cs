using System;
using System.Globalization;

namespace Isoline.Util
{
	public class Constants
	{
		public static readonly NumberFormatInfo DecimalParseFormat = new NumberFormatInfo() { NumberDecimalSeparator = "." };

		public static NumberFormatInfo DecimalOutputFormat
		{
			get
			{
				return new NumberFormatInfo() { NumberDecimalSeparator = ".", NumberDecimalDigits = 3 };
			}
		}

		public static readonly string FileFilterGCode = "GCode|*.tap;*.nc;*.ngc|All Files|*.*";
		public static readonly string FileFilterHeightMap = "Height Maps|*.hmap|All Files|*.*";
		public static readonly string FileFilterSettings = "Grbl settings|*.gbl;*.nc;*.ngc|All Files|*.*";

		public static readonly string LogFile = "log.txt";

		public static readonly char[] NewLines = new char[] { '\n', '\r' };

		public static readonly Version MinimumGrblVersion = new Version(1, 1, (int)'f');

		static Constants()
		{

		}
	}
}
