using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Isoline.Firmware
{
	public static class GrblCodeTranslator
	{
		internal static Dictionary<int, string> Errors = new Dictionary<int, string>();
		internal static Dictionary<int, string> Alarms = new Dictionary<int, string>();
		/// <summary>
		/// setting name, unit, description
		/// </summary>
		public static Dictionary<int, Tuple<string, string, string>> Settings { get; } = new Dictionary<int, Tuple<string, string, string>>();
		public static string Firmware { get; private set; } = "not loaded";

		private static void LoadErr(Dictionary<int, string> dict, string path)
		{
			if (!File.Exists(path))
			{
				Console.WriteLine("File Missing: {0}", path);
				return;
			}

			string FileContents;

			try
			{
				FileContents = File.ReadAllText(path);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return;
			}

			Regex LineParser = new Regex(@"""([0-9]+)"",""[^\n\r""]*"",""([^\n\r""]*)""");     //test here https://regex101.com/r/hO5zI1/4

			MatchCollection mc = LineParser.Matches(FileContents);

			foreach (Match m in mc)
			{
				try //shouldn't be needed as regex matched already
				{
					int number = int.Parse(m.Groups[1].Value);

					dict.Add(number, m.Groups[2].Value);
				}
				catch { }
			}
		}

		private static void LoadSettings(Dictionary<int, Tuple<string, string, string>> dict, string path)
		{
			if (!File.Exists(path))
			{
				Console.WriteLine("File Missing: {0}", path);
				return;
			}

			string FileContents;

			try
			{
				FileContents = File.ReadAllText(path);
			}
			catch (Exception ex)
			{
				Console.WriteLine(ex.Message);
				return;
			}

			Regex LineParser = new Regex(@"""([0-9]+)"",""([^\n\r""]*)"",""([^\n\r""]*)"",""([^\n\r""]*)""");

			MatchCollection mc = LineParser.Matches(FileContents);

			foreach (Match m in mc)
			{
				try //shouldn't be needed as regex matched already
				{
					int number = int.Parse(m.Groups[1].Value);

					dict.Add(number, new Tuple<string, string, string>(m.Groups[2].Value, m.Groups[3].Value, m.Groups[4].Value));
				}
				catch { }
			}
		}

		static GrblCodeTranslator()
		{
			Console.WriteLine("Loading GRBL Code Database");

			Reload("Grbl");

			Console.WriteLine("Loaded GRBL Code Database");
		}

		/// <summary>
		/// Directory the firmware code tables (*.csv) are loaded from. The application sets
		/// this once at start-up; Core never reaches for an application settings object.
		/// </summary>
		public static string ResourceDirectory { get; set; } = "Resources";

		/// <summary>
		/// Loads the error/alarm/setting tables for the given firmware flavour.
		/// Re-loading the same flavour twice is a no-op.
		/// </summary>
		public static void Reload(string firmware)
		{
			if (Firmware == firmware)
				return;

			Errors.Clear();
			Alarms.Clear();
			Settings.Clear();
			Firmware = firmware;

			string prefix;

			switch (firmware)
			{
				case "uCNC":
					prefix = "ucnc";
					break;
				case "grblHAL":
				case "FluidNC":
				case "Grbl":
				default:
					prefix = "grbl";     // grblHAL and FluidNC both speak the Grbl code tables
					break;
			}

			LoadErr(Errors, Path.Combine(ResourceDirectory, prefix + "_error_codes_en_US.csv"));
			LoadErr(Alarms, Path.Combine(ResourceDirectory, prefix + "_alarm_codes_en_US.csv"));
			LoadSettings(Settings, Path.Combine(ResourceDirectory, prefix + "_setting_codes_en_US.csv"));
		}

		public static string GetErrorMessage(int errorCode, bool alarm = false)
		{
			string message;

			if (!alarm)
			{
				return Errors.TryGetValue(errorCode, out message)
					? message
					: $"Unknown Error: {errorCode}";
			}

			return Alarms.TryGetValue(errorCode, out message)
				? message
				: $"Unknown Alarm: {errorCode}";
		}

		static Regex ErrorExp = new Regex(@"error:(\d+)");
		private static string ErrorMatchEvaluator(Match m)
		{
			return GetErrorMessage(int.Parse(m.Groups[1].Value));
		}

		static Regex AlarmExp = new Regex(@"ALARM:(\d+)");
		private static string AlarmMatchEvaluator(Match m)
		{
			return GetErrorMessage(int.Parse(m.Groups[1].Value), true);
		}

		public static string ExpandError(string error)
		{
			string ret = ErrorExp.Replace(error, ErrorMatchEvaluator);
			return AlarmExp.Replace(ret, AlarmMatchEvaluator);
		}
	}
}
