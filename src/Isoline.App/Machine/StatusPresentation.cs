using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;

namespace Isoline.Machines
{
	/// <summary>How a machine state should be drawn in the state pill.</summary>
	public class StatusStyle
	{
		public string Label { get; set; }
		public string FillKey { get; set; }
		public string TextKey { get; set; }

		/// <summary>The state needs the operator to do something before work can continue.</summary>
		public bool NeedsAttention { get; set; }

		/// <summary>Explains what the state means, shown as the pill's tooltip.</summary>
		public string Tooltip { get; set; }
	}

	/// <summary>
	/// Turns raw controller output into something an operator can act on.
	/// <para>
	/// Upstream painted the status label black when the machine was idle - invisible on a
	/// dark background - and dropped alarms into the console as "ALARM:3" among a hundred
	/// other lines. Both of those are the moments when the person at the machine most needs
	/// to know what is happening.
	/// </para>
	/// </summary>
	public static class StatusPresentation
	{
		private static readonly Dictionary<string, StatusStyle> Styles =
			new Dictionary<string, StatusStyle>(StringComparer.OrdinalIgnoreCase)
			{
				{ "Idle",    new StatusStyle { Label = "IDLE",    FillKey = "SurfaceOverlayBrush", TextKey = "TextBrush",        Tooltip = "Ready. The controller is waiting for a command." } },
				{ "Run",     new StatusStyle { Label = "RUN",     FillKey = "SuccessBrush",        TextKey = "TextOnAccentBrush", Tooltip = "Executing motion." } },
				{ "Jog",     new StatusStyle { Label = "JOG",     FillKey = "AccentBrush",         TextKey = "TextOnAccentBrush", Tooltip = "Jogging." } },
				{ "Hold",    new StatusStyle { Label = "HOLD",    FillKey = "WarningBrush",        TextKey = "TextOnAccentBrush", NeedsAttention = true, Tooltip = "Feed hold. Press Start to resume." } },
				{ "Door",    new StatusStyle { Label = "DOOR",    FillKey = "WarningBrush",        TextKey = "TextOnAccentBrush", NeedsAttention = true, Tooltip = "Safety door open. Close it, then press Start." } },
				{ "Alarm",   new StatusStyle { Label = "ALARM",   FillKey = "DangerBrush",         TextKey = "TextOnAccentBrush", NeedsAttention = true, Tooltip = "The controller has locked out. Clear the alarm before moving." } },
				{ "Check",   new StatusStyle { Label = "CHECK",   FillKey = "AccentBrush",         TextKey = "TextOnAccentBrush", Tooltip = "Check mode: G-code is parsed but no motion happens." } },
				{ "Home",    new StatusStyle { Label = "HOMING",  FillKey = "AccentBrush",         TextKey = "TextOnAccentBrush", Tooltip = "Homing cycle in progress." } },
				{ "Sleep",   new StatusStyle { Label = "SLEEP",   FillKey = "SurfaceOverlayBrush", TextKey = "TextMutedBrush",   NeedsAttention = true, Tooltip = "Controller asleep. A reset is needed to wake it." } },
				{ "Tool",    new StatusStyle { Label = "TOOL",    FillKey = "WarningBrush",        TextKey = "TextOnAccentBrush", NeedsAttention = true, Tooltip = "Waiting for a tool change." } },
			};

		private static readonly StatusStyle Disconnected = new StatusStyle
		{
			Label = "OFFLINE",
			FillKey = "SurfaceOverlayBrush",
			TextKey = "TextSubtleBrush",
			Tooltip = "Not connected to a controller.",
		};

		public static StatusStyle For(string status)
		{
			if (string.IsNullOrWhiteSpace(status))
				return Disconnected;

			// Grbl decorates some states with a substate, e.g. "Hold:0" or "Door:1"
			string key = status.Split(':')[0];

			StatusStyle style;

			if (Styles.TryGetValue(key, out style))
				return style;

			if (key.Equals("Disconnected", StringComparison.OrdinalIgnoreCase))
				return Disconnected;

			return new StatusStyle
			{
				Label = status.ToUpperInvariant(),
				FillKey = "SurfaceOverlayBrush",
				TextKey = "TextBrush",
				Tooltip = status,
			};
		}

		public static Brush Brush(string resourceKey)
		{
			object resource = Application.Current != null ? Application.Current.TryFindResource(resourceKey) : null;

			return resource as Brush ?? Brushes.Gray;
		}

		/// <summary>
		/// What to do about a given alarm. Keyed by Grbl alarm number; the text of the alarm
		/// itself already comes from the firmware code tables, this is the missing half.
		/// </summary>
		private static readonly Dictionary<int, string> AlarmRemedies = new Dictionary<int, string>
		{
			{ 1, "A limit switch tripped during a move. The machine position is no longer trusted - re-home before cutting." },
			{ 2, "The move would have left the soft-limit envelope. Check the work offset, then re-zero." },
			{ 3, "Reset while moving. Position is unknown; re-home." },
			{ 4, "The probe was already touching when the cycle started. Retract, then probe again." },
			{ 5, "The probe never made contact within the search distance. Check the probe clip and the Z start height." },
			{ 6, "Homing was reset before it finished. Try homing again." },
			{ 7, "Safety door opened during homing. Close it and re-home." },
			{ 8, "A limit switch was already active at the start of homing. Jog off the switch first." },
			{ 9, "The axis did not reach its limit switch in the search distance. Check wiring and the homing direction." },
			{ 10, "Dual-axis homing failed - the two motors did not agree. Check both switches." },
		};

		private static readonly Regex AlarmNumber = new Regex(@"ALARM:(\d+)", RegexOptions.IgnoreCase);
		private static readonly Regex ErrorNumber = new Regex(@"error:(\d+)", RegexOptions.IgnoreCase);

		/// <summary>
		/// Splits a controller message into a headline and a suggested remedy.
		/// Returns null when the message is not an alarm or error.
		/// </summary>
		public static AlarmInfo Decode(string message)
		{
			if (string.IsNullOrWhiteSpace(message))
				return null;

			Match alarm = AlarmNumber.Match(message);

			if (alarm.Success)
			{
				int number = int.Parse(alarm.Groups[1].Value);
				string remedy;

				return new AlarmInfo
				{
					IsAlarm = true,
					Title = "Alarm " + number + ": " + Firmware.GrblCodeTranslator.GetErrorMessage(number, true),
					Remedy = AlarmRemedies.TryGetValue(number, out remedy)
						? remedy
						: "Clear the alarm with Unlock once you know why it happened.",
				};
			}

			Match error = ErrorNumber.Match(message);

			if (error.Success)
			{
				int number = int.Parse(error.Groups[1].Value);

				return new AlarmInfo
				{
					IsAlarm = false,
					Title = "Error " + number + ": " + Firmware.GrblCodeTranslator.GetErrorMessage(number),
					Remedy = "The controller rejected that line. The job has been stopped so nothing runs out of order.",
				};
			}

			return null;
		}
	}

	public class AlarmInfo
	{
		public bool IsAlarm { get; set; }
		public string Title { get; set; }
		public string Remedy { get; set; }
	}
}
