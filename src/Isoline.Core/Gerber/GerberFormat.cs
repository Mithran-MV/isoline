using System;
using System.Globalization;

namespace Isoline.Gerber
{
	/// <summary>
	/// The coordinate format declared by a Gerber file's <c>%FS</c> directive, plus the
	/// unit declared by <c>%MO</c>.
	/// <para>
	/// Gerber coordinates are integers whose decimal point is implied by this format:
	/// with <c>%FSLAX34Y34*%</c> the token <c>12345</c> means 1.2345 units. Getting this
	/// wrong scales the whole board by a power of ten, so it is parsed strictly.
	/// </para>
	/// </summary>
	public class GerberFormat
	{
		public int IntegerDigits { get; set; } = 3;
		public int DecimalDigits { get; set; } = 5;

		/// <summary>False when coordinates are incremental (deprecated, rarely emitted).</summary>
		public bool Absolute { get; set; } = true;

		/// <summary>True when leading zeros are omitted (the modern default, "L").</summary>
		public bool OmitLeadingZeros { get; set; } = true;

		/// <summary>Millimetres per file unit: 1 for %MOMM, 25.4 for %MOIN.</summary>
		public double UnitScale { get; set; } = 1.0;

		/// <summary>
		/// Converts a raw coordinate token to millimetres.
		/// </summary>
		public double ToMillimetres(string token)
		{
			if (string.IsNullOrEmpty(token))
				throw new GerberException("empty coordinate");

			bool negative = false;
			int start = 0;

			if (token[0] == '+' || token[0] == '-')
			{
				negative = token[0] == '-';
				start = 1;
			}

			string digits = token.Substring(start);

			// A file may also carry an explicit decimal point; some CAM tools emit that
			// even though the spec discourages it.
			if (digits.Contains('.'))
			{
				double explicitValue = double.Parse(digits, CultureInfo.InvariantCulture);
				return (negative ? -explicitValue : explicitValue) * UnitScale;
			}

			if (!OmitLeadingZeros)
			{
				// trailing zeros omitted: pad on the right instead
				digits = digits.PadRight(IntegerDigits + DecimalDigits, '0');
			}
			else
			{
				digits = digits.PadLeft(IntegerDigits + DecimalDigits, '0');
			}

			double raw = double.Parse(digits, CultureInfo.InvariantCulture);
			double value = raw / Math.Pow(10, DecimalDigits);

			return (negative ? -value : value) * UnitScale;
		}
	}

	/// <summary>Raised for malformed or unsupported Gerber input.</summary>
	public class GerberException : Exception
	{
		public GerberException(string message) : base(message) { }
	}
}
