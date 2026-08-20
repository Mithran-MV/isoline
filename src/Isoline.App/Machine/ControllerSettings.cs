using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Isoline.Machines
{
	/// <summary>
	/// A live cache of the controller's <c>$n</c> settings, filled by watching the reply to
	/// a <c>$$</c> query.
	/// <para>
	/// Upstream parsed these inside the Grbl settings dialog and threw them away when it
	/// closed, which is why the calibration panel had no way to read the current steps/mm
	/// and simply asked the user to type them in.
	/// </para>
	/// </summary>
	public class ControllerSettings
	{
		private static readonly Regex SettingLine = new Regex(@"^\$(\d+)\s*=\s*(-?[\d.]+)", RegexOptions.Compiled);

		private readonly Dictionary<int, double> values = new Dictionary<int, double>();

		/// <summary>Raised once for every setting line received.</summary>
		public event Action<int, double> SettingReceived;

		/// <summary>Raised when a burst of settings has finished arriving.</summary>
		public event Action Refreshed;

		private DateTime lastLine = DateTime.MinValue;
		private bool receiving;

		public bool TryGet(int number, out double value)
		{
			return values.TryGetValue(number, out value);
		}

		public double Get(int number, double fallback = 0)
		{
			double value;

			return values.TryGetValue(number, out value) ? value : fallback;
		}

		public bool Has(int number)
		{
			return values.ContainsKey(number);
		}

		public int Count { get { return values.Count; } }

		/// <summary>Feed every line the controller sends here.</summary>
		public void OnLineReceived(string line)
		{
			if (string.IsNullOrEmpty(line))
				return;

			Match match = SettingLine.Match(line.Trim());

			if (!match.Success)
			{
				// "ok" closes the burst that a $$ query opened
				if (receiving && line.Trim().Equals("ok", StringComparison.OrdinalIgnoreCase))
				{
					receiving = false;

					if (Refreshed != null)
						Refreshed();
				}

				return;
			}

			int number = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
			double value = double.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

			values[number] = value;
			receiving = true;
			lastLine = DateTime.UtcNow;

			if (SettingReceived != null)
				SettingReceived(number, value);
		}

		public void Clear()
		{
			values.Clear();
		}

		/// <summary>Known setting numbers, for the calibration panel.</summary>
		public const int StepsPerMmX = 100;
		public const int StepsPerMmY = 101;
		public const int StepsPerMmZ = 102;
		public const int HomingEnable = 22;
		public const int HomingDirInvert = 23;
		public const int HomingFeed = 24;
		public const int HomingSeek = 25;
	}
}
