using System;
using System.Collections.Generic;

namespace Isoline.Expressions
{
	/// <summary>
	/// The function and constant library available inside expressions.
	/// Names are matched case-insensitively so <c>SQRT(2)</c> and <c>sqrt(2)</c> both work.
	/// </summary>
	public static class Functions
	{
		private static readonly Dictionary<string, Func<double[], double>> Table =
			new Dictionary<string, Func<double[], double>>(StringComparer.OrdinalIgnoreCase)
			{
				{ "abs",   a => { Arity("abs", a, 1); return Math.Abs(a[0]); } },
				{ "sign",  a => { Arity("sign", a, 1); return Math.Sign(a[0]); } },
				{ "sqrt",  a => { Arity("sqrt", a, 1); if (a[0] < 0) throw new ExpressionException("sqrt of a negative number"); return Math.Sqrt(a[0]); } },
				{ "exp",   a => { Arity("exp", a, 1); return Math.Exp(a[0]); } },
				{ "ln",    a => { Arity("ln", a, 1); if (a[0] <= 0) throw new ExpressionException("ln of a non-positive number"); return Math.Log(a[0]); } },
				{ "log",   a => { Arity("log", a, 1); if (a[0] <= 0) throw new ExpressionException("log of a non-positive number"); return Math.Log10(a[0]); } },
				{ "floor", a => { Arity("floor", a, 1); return Math.Floor(a[0]); } },
				{ "ceil",  a => { Arity("ceil", a, 1); return Math.Ceiling(a[0]); } },
				{ "round", a => { if (a.Length == 1) return Math.Round(a[0], MidpointRounding.AwayFromZero);
				                  Arity("round", a, 2); return Math.Round(a[0], (int)a[1], MidpointRounding.AwayFromZero); } },
				{ "trunc", a => { Arity("trunc", a, 1); return Math.Truncate(a[0]); } },

				// trigonometry - degrees, because that is what a machinist reads off a drawing
				{ "sin",   a => { Arity("sin", a, 1); return Math.Sin(a[0] * DegToRad); } },
				{ "cos",   a => { Arity("cos", a, 1); return Math.Cos(a[0] * DegToRad); } },
				{ "tan",   a => { Arity("tan", a, 1); return Math.Tan(a[0] * DegToRad); } },
				{ "asin",  a => { Arity("asin", a, 1); return Math.Asin(a[0]) * RadToDeg; } },
				{ "acos",  a => { Arity("acos", a, 1); return Math.Acos(a[0]) * RadToDeg; } },
				{ "atan",  a => { Arity("atan", a, 1); return Math.Atan(a[0]) * RadToDeg; } },
				{ "atan2", a => { Arity("atan2", a, 2); return Math.Atan2(a[0], a[1]) * RadToDeg; } },

				// radian variants for anyone who prefers them
				{ "sinr",  a => { Arity("sinr", a, 1); return Math.Sin(a[0]); } },
				{ "cosr",  a => { Arity("cosr", a, 1); return Math.Cos(a[0]); } },
				{ "tanr",  a => { Arity("tanr", a, 1); return Math.Tan(a[0]); } },

				{ "min",   a => { AtLeast("min", a, 1); double m = a[0]; foreach (double v in a) m = Math.Min(m, v); return m; } },
				{ "max",   a => { AtLeast("max", a, 1); double m = a[0]; foreach (double v in a) m = Math.Max(m, v); return m; } },
				{ "hypot", a => { Arity("hypot", a, 2); return Math.Sqrt(a[0] * a[0] + a[1] * a[1]); } },

				// unit helpers, handy in macros written against imperial drawings
				{ "inch",  a => { Arity("inch", a, 1); return a[0] * 25.4; } },
				{ "mm",    a => { Arity("mm", a, 1); return a[0] / 25.4; } },
			};

		private static readonly Dictionary<string, double> Constants =
			new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase)
			{
				{ "pi", Math.PI },
				{ "e", Math.E },
			};

		private const double DegToRad = Math.PI / 180.0;
		private const double RadToDeg = 180.0 / Math.PI;

		public static double Call(string name, double[] args)
		{
			Func<double[], double> fn;

			if (!Table.TryGetValue(name, out fn))
				throw new ExpressionException(string.Format("unknown function '{0}'", name));

			return fn(args);
		}

		public static bool TryGetConstant(string name, out double value)
		{
			return Constants.TryGetValue(name, out value);
		}

		/// <summary>Names of every callable function, for documentation and UI hints.</summary>
		public static IEnumerable<string> Names { get { return Table.Keys; } }

		private static void Arity(string name, double[] args, int expected)
		{
			if (args.Length != expected)
				throw new ExpressionException(string.Format("{0}() takes {1} argument(s), got {2}", name, expected, args.Length));
		}

		private static void AtLeast(string name, double[] args, int minimum)
		{
			if (args.Length < minimum)
				throw new ExpressionException(string.Format("{0}() takes at least {1} argument(s), got {2}", name, minimum, args.Length));
		}
	}
}
