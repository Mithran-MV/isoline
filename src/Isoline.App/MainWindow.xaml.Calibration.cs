using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Isoline.Machines;
using Isoline.Util;

namespace Isoline
{
	public partial class MainWindow
	{
		/// <summary>Cache of the controller's $n settings, kept in step with what it reports.</summary>
		public ControllerSettings ControllerSettings { get; } = new ControllerSettings();

		/// <summary>
		/// Asks the controller for its settings. The boxes fill themselves when the reply
		/// arrives - the previous version told the user to press the button again in a
		/// moment and then to type the values in by hand.
		/// </summary>
		private void ButtonReadCalibration_Click(object sender, RoutedEventArgs e)
		{
			if (!RequireConnection())
				return;

			machine.SendLine("$$");
			Machine_Info("Reading controller settings...");
		}

		/// <summary>Populates the calibration boxes from the cache.</summary>
		private void ControllerSettings_Refreshed()
		{
			Dispatcher.Invoke(() =>
			{
				SetBox(TextBoxXSteps, ControllerSettings.StepsPerMmX);
				SetBox(TextBoxYSteps, ControllerSettings.StepsPerMmY);
				SetBox(TextBoxZSteps, ControllerSettings.StepsPerMmZ);
				SetBox(TextBoxHomeMask, ControllerSettings.HomingDirInvert);
				SetBox(TextBoxHomeFeed, ControllerSettings.HomingFeed);
				SetBox(TextBoxHomeSeek, ControllerSettings.HomingSeek);

				double homing;

				if (ControllerSettings.TryGet(ControllerSettings.HomingEnable, out homing))
					CheckBoxHomeEnable.IsChecked = homing != 0;

				Machine_Info($"Read {ControllerSettings.Count} settings from the controller.");
			});
		}

		private void SetBox(TextBox box, int setting)
		{
			double value;

			if (ControllerSettings.TryGet(setting, out value))
				box.Text = value.ToString("0.###", CultureInfo.InvariantCulture);
		}

		/// <summary>
		/// Writes the calibration back. Every value is validated first: sending a malformed
		/// or wildly wrong steps/mm is how an axis ends up driving into its end stop.
		/// </summary>
		private async void ButtonSaveCalibration_Click(object sender, RoutedEventArgs e)
		{
			if (!RequireConnection())
				return;

			List<string> commands = new List<string>();

			commands.Add($"${ControllerSettings.HomingEnable}={(CheckBoxHomeEnable.IsChecked == true ? 1 : 0)}");

			if (!AddSetting(commands, ControllerSettings.HomingDirInvert, TextBoxHomeMask, 0, 7, "homing direction mask"))
				return;
			if (!AddSetting(commands, ControllerSettings.HomingFeed, TextBoxHomeFeed, 1, 10000, "homing feed"))
				return;
			if (!AddSetting(commands, ControllerSettings.HomingSeek, TextBoxHomeSeek, 1, 20000, "homing seek rate"))
				return;
			if (!AddSetting(commands, ControllerSettings.StepsPerMmX, TextBoxXSteps, 1, 100000, "X steps/mm"))
				return;
			if (!AddSetting(commands, ControllerSettings.StepsPerMmY, TextBoxYSteps, 1, 100000, "Y steps/mm"))
				return;
			if (!AddSetting(commands, ControllerSettings.StepsPerMmZ, TextBoxZSteps, 1, 100000, "Z steps/mm"))
				return;

			MessageBoxResult confirm = MessageBox.Show(
				"About to write these settings to the controller:\n\n" + string.Join("\n", commands) +
				"\n\nThey take effect immediately and persist across power cycles.",
				"Write calibration", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

			if (confirm != MessageBoxResult.OK)
				return;

			foreach (string command in commands)
			{
				machine.SendLine(command);

				// Grbl writes each setting to EEPROM before acknowledging it; crowding the
				// buffer here is how settings get silently dropped.
				await Task.Delay(Math.Max(50, Properties.Settings.Default.SettingsSendDelay));
			}

			machine.SendLine("$$");      // read back, so the boxes show what actually stuck
			Machine_Info("Calibration written.");
		}

		private bool AddSetting(List<string> commands, int number, TextBox box, double min, double max, string description)
		{
			if (string.IsNullOrWhiteSpace(box.Text))
				return true;    // leave that setting alone

			double value;

			if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value))
			{
				MessageBox.Show($"\"{box.Text}\" is not a number ({description}).",
					"Check the value", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			if (value < min || value > max)
			{
				MessageBox.Show($"{description} of {value} is outside the sensible range {min} to {max}.",
					"Check the value", MessageBoxButton.OK, MessageBoxImage.Warning);
				return false;
			}

			commands.Add($"${number}={value.ToString("0.###", CultureInfo.InvariantCulture)}");
			return true;
		}

		/// <summary>
		/// Opens the steps/mm wizard: command a known move, measure what the machine
		/// actually did, and let it work out the correction.
		/// </summary>
		private void ButtonCalibrationWizard_Click(object sender, RoutedEventArgs e)
		{
			if (!RequireConnection())
				return;

			CalibrationWizardWindow wizard = new CalibrationWizardWindow(machine, ControllerSettings) { Owner = this };

			wizard.ShowDialog();

			if (wizard.CorrectedValue == null)
				return;

			TextBox target = wizard.Axis == 'X' ? TextBoxXSteps
				: wizard.Axis == 'Y' ? TextBoxYSteps
				: TextBoxZSteps;

			target.Text = wizard.CorrectedValue.Value.ToString("0.###", CultureInfo.InvariantCulture);

			ShowNotice("Calibration worked out",
				$"{wizard.Axis} steps/mm should be {wizard.CorrectedValue.Value:0.###} " +
				$"(was {wizard.OriginalValue:0.###}). Press \"Save to controller\" to write it.");
		}

		private bool RequireConnection()
		{
			if (machine.Connected)
				return true;

			MessageBox.Show("Connect to the machine first.", "Not connected",
				MessageBoxButton.OK, MessageBoxImage.Information);

			return false;
		}
	}
}
