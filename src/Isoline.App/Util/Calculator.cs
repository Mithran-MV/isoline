using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using Isoline.Communication;
using Isoline.Expressions;

namespace Isoline.Util
{
	/// <summary>
	/// Substitutes parenthesised expressions in a manual command or macro with live machine
	/// values, e.g. <c>G0 X(MX - 10)</c>.
	/// <para>
	/// The arithmetic itself lives in <see cref="Expression"/> in Isoline.Core; this class
	/// only supplies the variables and stitches the result back into the line.
	/// </para>
	/// </summary>
	public class Calculator
	{
		private Machine machine;
		public Func<GCode.GCodeFile> GetGCode;
		private bool Success = true;

		public Calculator(Machine machine)
		{
			this.machine = machine;
		}

		private static string[] Axes = new string[] { "X", "Y", "Z" };

		/// <summary>
		/// The variables available inside an expression, with a one line description each.
		/// Drives the in-app help so the list can never drift from what actually resolves.
		/// </summary>
		public static readonly (string Name, string Description)[] VariableHelp =
		{
			("MX, MY, MZ", "machine position"),
			("WX, WY, WZ", "work position"),
			("PMX, PMY, PMZ", "last probed position, machine coordinates"),
			("PWX, PWY, PWZ", "last probed position, work coordinates"),
			("MINX, MINY, MINZ", "lowest cutting move in the loaded file"),
			("MAXX, MAXY, MAXZ", "highest cutting move in the loaded file"),
			("TLO", "current tool length offset"),
		};

		private Dictionary<string, double> BuildVariables()
		{
			Dictionary<string, double> variables = new Dictionary<string, double>();

			for (int i = 0; i < 3; i++)
			{
				variables.Add("M" + Axes[i], machine.MachinePosition[i]);
				variables.Add("W" + Axes[i], machine.WorkPosition[i]);
				variables.Add("PM" + Axes[i], machine.LastProbePosMachine[i]);
				variables.Add("PW" + Axes[i], machine.LastProbePosWork[i]);
			}

			if (GetGCode != null)
			{
				try
				{
					var file = GetGCode();
					var min = file.MinFeed;
					var max = file.MaxFeed;

					if (!file.ContainsMotion)
					{
						min = new Vector3(0, 0, 0);
						max = new Vector3(0, 0, 0);
					}

					for (int i = 0; i < 3; i++)
					{
						variables.Add("MAX" + Axes[i], max[i]);
						variables.Add("MIN" + Axes[i], min[i]);
					}
				}
				catch { }
			}

			variables.Add("TLO", machine.CurrentTLO);

			return variables;
		}

		private string ExpressionEvaluator(string input)
		{
			try
			{
				double value = Expression.Parse(input).GetValue(BuildVariables());

				return value.ToString("0.###", Constants.DecimalOutputFormat);
			}
			catch (Exception ex)
			{
				Success = false;
				Console.WriteLine(ex.Message);
				return $"[{ex.Message}]";
			}
		}

		public string Evaluate(string input, out bool success)
		{
			Success = true;

			try
			{
				int depth = 0;
				int start = 0;

				StringBuilder output = new StringBuilder(input.Length);

				for (int i = 0; i < input.Length; i++)
				{
					if (input[i] == '(')
					{
						if (depth == 0)
							start = i + 1;
						depth++;
					}
					else if (input[i] == ')')
					{
						depth--;
						if (depth == 0)
						{
							if (i - start > 0)
								output.Append(ExpressionEvaluator(input.Substring(start, i - start)));
						}
						else if (depth == -1)
						{
							Success = false;
							depth = 0;
						}
					}
					else if (depth == 0)
						output.Append(input[i]);
				}

				if (depth != 0)
					Success = false;

				success = Success;
				return output.ToString();
			}
			catch
			{
				success = false;
				return "ERROR";
			}
		}
	}
}
