using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace Isoline.Expressions
{
	/// <summary>
	/// A parsed arithmetic expression that can be evaluated against a set of named variables.
	/// <para>
	/// Isoline lets you write live machine values into manual commands and macros, e.g.
	/// <c>G0 X(MX - 10) Y(MAXY / 2)</c>. This is the evaluator behind those parentheses.
	/// </para>
	/// <para>
	/// It replaces the martin2250.Calculator package the upstream project used: that
	/// package only ships .NET Framework binaries, and dropping it is what allowed the
	/// whole computation core to move to .NET 8 and become unit-testable on any OS.
	/// </para>
	/// </summary>
	public class Expression
	{
		private readonly Node _root;

		private Expression(Node root)
		{
			_root = root;
		}

		/// <summary>
		/// Parses an expression. Throws <see cref="ExpressionException"/> on malformed input.
		/// </summary>
		public static Expression Parse(string input)
		{
			if (input == null)
				throw new ArgumentNullException("input");

			Parser parser = new Parser(Tokenizer.Tokenize(input));
			Node root = parser.ParseExpression();
			parser.ExpectEnd();

			return new Expression(root);
		}

		/// <summary>
		/// Evaluates the expression. Unknown variable names throw
		/// <see cref="ExpressionException"/> rather than silently evaluating to zero - a
		/// typo in a macro should stop the job, not move the machine somewhere unexpected.
		/// </summary>
		public double GetValue(IDictionary<string, double> variables = null)
		{
			return _root.Evaluate(variables ?? EmptyVariables);
		}

		private static readonly Dictionary<string, double> EmptyVariables = new Dictionary<string, double>();

		#region Syntax tree

		private abstract class Node
		{
			public abstract double Evaluate(IDictionary<string, double> vars);
		}

		private sealed class ConstantNode : Node
		{
			private readonly double _value;
			public ConstantNode(double value) { _value = value; }
			public override double Evaluate(IDictionary<string, double> vars) { return _value; }
		}

		private sealed class VariableNode : Node
		{
			private readonly string _name;
			public VariableNode(string name) { _name = name; }

			public override double Evaluate(IDictionary<string, double> vars)
			{
				double value;

				if (vars.TryGetValue(_name, out value))
					return value;

				if (vars.TryGetValue(_name.ToUpperInvariant(), out value))
					return value;

				throw new ExpressionException(string.Format("unknown variable '{0}'", _name));
			}
		}

		private sealed class UnaryNode : Node
		{
			private readonly char _op;
			private readonly Node _operand;
			public UnaryNode(char op, Node operand) { _op = op; _operand = operand; }

			public override double Evaluate(IDictionary<string, double> vars)
			{
				double v = _operand.Evaluate(vars);
				return _op == '-' ? -v : v;
			}
		}

		private sealed class BinaryNode : Node
		{
			private readonly char _op;
			private readonly Node _left, _right;
			public BinaryNode(char op, Node left, Node right) { _op = op; _left = left; _right = right; }

			public override double Evaluate(IDictionary<string, double> vars)
			{
				double a = _left.Evaluate(vars);
				double b = _right.Evaluate(vars);

				switch (_op)
				{
					case '+': return a + b;
					case '-': return a - b;
					case '*': return a * b;
					case '/':
						if (b == 0)
							throw new ExpressionException("division by zero");
						return a / b;
					case '%':
						if (b == 0)
							throw new ExpressionException("division by zero");
						return a % b;
					case '^': return Math.Pow(a, b);
					default:
						throw new ExpressionException(string.Format("unknown operator '{0}'", _op));
				}
			}
		}

		private sealed class FunctionNode : Node
		{
			private readonly string _name;
			private readonly List<Node> _args;
			public FunctionNode(string name, List<Node> args) { _name = name; _args = args; }

			public override double Evaluate(IDictionary<string, double> vars)
			{
				double[] a = new double[_args.Count];
				for (int i = 0; i < _args.Count; i++)
					a[i] = _args[i].Evaluate(vars);

				return Functions.Call(_name, a);
			}
		}

		#endregion

		#region Tokenizer

		private enum TokenType { Number, Identifier, Operator, LeftParen, RightParen, Comma, End }

		private struct Token
		{
			public TokenType Type;
			public string Text;
			public double Value;
			public int Position;
		}

		private static class Tokenizer
		{
			public static List<Token> Tokenize(string input)
			{
				List<Token> tokens = new List<Token>();
				int i = 0;

				while (i < input.Length)
				{
					char c = input[i];

					if (char.IsWhiteSpace(c))
					{
						i++;
						continue;
					}

					if (char.IsDigit(c) || c == '.')
					{
						int start = i;

						while (i < input.Length && (char.IsDigit(input[i]) || input[i] == '.'))
							i++;

						// exponent notation: 1e-3
						if (i < input.Length && (input[i] == 'e' || input[i] == 'E')
							&& i + 1 < input.Length
							&& (char.IsDigit(input[i + 1]) || ((input[i + 1] == '+' || input[i + 1] == '-') && i + 2 < input.Length && char.IsDigit(input[i + 2]))))
						{
							i++;
							if (input[i] == '+' || input[i] == '-')
								i++;
							while (i < input.Length && char.IsDigit(input[i]))
								i++;
						}

						string text = input.Substring(start, i - start);
						double value;

						if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
							throw new ExpressionException(string.Format("'{0}' is not a valid number", text));

						tokens.Add(new Token() { Type = TokenType.Number, Text = text, Value = value, Position = start });
						continue;
					}

					if (char.IsLetter(c) || c == '_')
					{
						int start = i;

						while (i < input.Length && (char.IsLetterOrDigit(input[i]) || input[i] == '_'))
							i++;

						tokens.Add(new Token() { Type = TokenType.Identifier, Text = input.Substring(start, i - start), Position = start });
						continue;
					}

					switch (c)
					{
						case '(':
							tokens.Add(new Token() { Type = TokenType.LeftParen, Text = "(", Position = i });
							break;
						case ')':
							tokens.Add(new Token() { Type = TokenType.RightParen, Text = ")", Position = i });
							break;
						case ',':
							tokens.Add(new Token() { Type = TokenType.Comma, Text = ",", Position = i });
							break;
						case '+':
						case '-':
						case '*':
						case '/':
						case '%':
						case '^':
							tokens.Add(new Token() { Type = TokenType.Operator, Text = c.ToString(), Position = i });
							break;
						default:
							throw new ExpressionException(string.Format("unexpected character '{0}' at position {1}", c, i));
					}

					i++;
				}

				tokens.Add(new Token() { Type = TokenType.End, Text = "<end>", Position = input.Length });
				return tokens;
			}
		}

		#endregion

		#region Parser

		/// <summary>
		/// Recursive descent over the usual precedence ladder:
		/// additive -&gt; multiplicative -&gt; power (right associative) -&gt; unary -&gt; primary.
		/// </summary>
		private class Parser
		{
			private readonly List<Token> _tokens;
			private int _index;

			public Parser(List<Token> tokens)
			{
				_tokens = tokens;
			}

			private Token Current { get { return _tokens[_index]; } }

			public void ExpectEnd()
			{
				if (Current.Type != TokenType.End)
					throw new ExpressionException(string.Format("unexpected '{0}' at position {1}", Current.Text, Current.Position));
			}

			public Node ParseExpression()
			{
				Node left = ParseTerm();

				while (Current.Type == TokenType.Operator && (Current.Text == "+" || Current.Text == "-"))
				{
					char op = Current.Text[0];
					_index++;
					left = new BinaryNode(op, left, ParseTerm());
				}

				return left;
			}

			private Node ParseTerm()
			{
				Node left = ParseUnary();

				while (Current.Type == TokenType.Operator && (Current.Text == "*" || Current.Text == "/" || Current.Text == "%"))
				{
					char op = Current.Text[0];
					_index++;
					left = new BinaryNode(op, left, ParseUnary());
				}

				return left;
			}

			private Node ParseUnary()
			{
				if (Current.Type == TokenType.Operator && (Current.Text == "-" || Current.Text == "+"))
				{
					char op = Current.Text[0];
					_index++;
					return new UnaryNode(op, ParseUnary());
				}

				return ParsePower();
			}

			private Node ParsePower()
			{
				Node baseNode = ParsePrimary();

				if (Current.Type == TokenType.Operator && Current.Text == "^")
				{
					_index++;
					// right associative, and -x binds tighter than ^ on the exponent side
					// so that 2^-1 parses.
					return new BinaryNode('^', baseNode, ParseUnary());
				}

				return baseNode;
			}

			private Node ParsePrimary()
			{
				Token token = Current;

				switch (token.Type)
				{
					case TokenType.Number:
						_index++;
						return new ConstantNode(token.Value);

					case TokenType.Identifier:
						_index++;

						if (Current.Type == TokenType.LeftParen)
						{
							_index++;
							List<Node> args = new List<Node>();

							if (Current.Type != TokenType.RightParen)
							{
								args.Add(ParseExpression());

								while (Current.Type == TokenType.Comma)
								{
									_index++;
									args.Add(ParseExpression());
								}
							}

							if (Current.Type != TokenType.RightParen)
								throw new ExpressionException(string.Format("expected ')' after arguments of '{0}'", token.Text));

							_index++;
							return new FunctionNode(token.Text, args);
						}

						double constant;
						if (Functions.TryGetConstant(token.Text, out constant))
							return new ConstantNode(constant);

						return new VariableNode(token.Text);

					case TokenType.LeftParen:
						_index++;
						Node inner = ParseExpression();

						if (Current.Type != TokenType.RightParen)
							throw new ExpressionException("unbalanced parentheses");

						_index++;
						return inner;

					case TokenType.End:
						throw new ExpressionException("unexpected end of expression");

					default:
						throw new ExpressionException(string.Format("unexpected '{0}' at position {1}", token.Text, token.Position));
				}
			}
		}

		#endregion

		public override string ToString()
		{
			return "Expression";
		}
	}

	/// <summary>Thrown for malformed or un-evaluatable expressions.</summary>
	public class ExpressionException : Exception
	{
		public ExpressionException(string message) : base(message) { }
	}
}
