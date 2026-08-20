using System;
using System.Collections.Generic;
using Isoline.Expressions;
using Xunit;

namespace Isoline.Tests
{
	public class ExpressionTests
	{
		private static double Eval(string s, IDictionary<string, double> vars = null)
		{
			return Expression.Parse(s).GetValue(vars);
		}

		[Theory]
		[InlineData("1", 1)]
		[InlineData("1 + 2", 3)]
		[InlineData("2 * 3 + 1", 7)]
		[InlineData("1 + 2 * 3", 7)]
		[InlineData("(1 + 2) * 3", 9)]
		[InlineData("10 / 4", 2.5)]
		[InlineData("10 % 3", 1)]
		[InlineData("2 ^ 10", 1024)]
		[InlineData("-5", -5)]
		[InlineData("-2 ^ 2", -4)]          // unary minus applies to the result, as in maths
		[InlineData("2 ^ -1", 0.5)]
		[InlineData("2 ^ 3 ^ 2", 512)]      // right associative
		[InlineData("1e-3", 0.001)]
		[InlineData("  7  ", 7)]
		public void EvaluatesArithmetic(string input, double expected)
		{
			Assert.Equal(expected, Eval(input), 9);
		}

		[Theory]
		[InlineData("sqrt(16)", 4)]
		[InlineData("abs(-3)", 3)]
		[InlineData("min(4, 2, 9)", 2)]
		[InlineData("max(4, 2, 9)", 9)]
		[InlineData("round(2.5)", 3)]
		[InlineData("round(2.345, 2)", 2.35)]
		[InlineData("floor(2.9)", 2)]
		[InlineData("ceil(2.1)", 3)]
		[InlineData("hypot(3, 4)", 5)]
		[InlineData("inch(1)", 25.4)]
		[InlineData("SQRT(9)", 3)]          // function names are case insensitive
		public void EvaluatesFunctions(string input, double expected)
		{
			Assert.Equal(expected, Eval(input), 9);
		}

		[Fact]
		public void TrigonometryIsInDegrees()
		{
			// a machinist reads degrees off a drawing, so that is what the evaluator takes
			Assert.Equal(1.0, Eval("sin(90)"), 9);
			Assert.Equal(0.0, Eval("cos(90)"), 9);
			Assert.Equal(45.0, Eval("atan(1)"), 9);
			Assert.Equal(1.0, Eval("sinr(pi / 2)"), 9);
		}

		[Fact]
		public void ResolvesVariables()
		{
			var vars = new Dictionary<string, double> { { "MX", 10 }, { "MY", -4.5 } };

			Assert.Equal(5, Eval("MX / 2", vars), 9);
			Assert.Equal(5.5, Eval("MX + MY", vars), 9);
			Assert.Equal(5, Eval("mx / 2", vars), 9);   // falls back to the upper case name
		}

		[Fact]
		public void KnowsPiAndE()
		{
			Assert.Equal(Math.PI, Eval("pi"), 9);
			Assert.Equal(Math.E, Eval("e"), 9);
		}

		[Theory]
		[InlineData("1 +")]
		[InlineData("(1 + 2")]
		[InlineData("1 + 2)")]
		[InlineData("@")]
		[InlineData("")]
		[InlineData("nosuchfunc(1)")]
		[InlineData("sqrt(1, 2)")]
		public void RejectsMalformedInput(string input)
		{
			Assert.Throws<ExpressionException>(() => Eval(input));
		}

		[Fact]
		public void UnknownVariableThrowsRatherThanReturningZero()
		{
			// silently evaluating to 0 would move the machine somewhere unexpected
			Assert.Throws<ExpressionException>(() => Eval("WX + 1"));
		}

		[Fact]
		public void DivisionByZeroThrows()
		{
			Assert.Throws<ExpressionException>(() => Eval("1 / 0"));
			Assert.Throws<ExpressionException>(() => Eval("1 % 0"));
		}
	}
}
