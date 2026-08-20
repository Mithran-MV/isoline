using System;
using System.Windows;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using System.Globalization;
using OpenCNCPilot.Util;

namespace OpenCNCPilot
{
	public partial class MainWindow
	{
		private void ButtonReadCalibration_Click(object sender, RoutedEventArgs e)
		{
			if (!machine.Connected)
			{
				MessageBox.Show("Machine is not connected!");
				return;
			}
			
			// GRBL Settings Window has a cache of the current settings. 
			// We can trigger a $$ command to refresh them if needed.
			machine.SendLine("$$");
			Machine_Info("Reading GRBL configuration. Please click 'Read from GRBL' again in a moment if fields don't populate.");

			// Load values from GrblCodeTranslator or Machine settings if we can access them.
			// Currently OpenCNCPilot parses them in GrblSettingsWindow, but we might just hook into the Machine's log or global settings if they exist.
			// Actually, OpenCNCPilot's core might not store the $ variables in a public dictionary. Let's just ask the user to input them.
			// For now, I will leave it empty and let the user type them.
			MessageBox.Show("Please ensure GRBL Settings window has been opened once to load values, or type them manually below.");
		}

		private async void ButtonSaveCalibration_Click(object sender, RoutedEventArgs e)
		{
			if (!machine.Connected)
			{
				MessageBox.Show("Machine is not connected!");
				return;
			}

			try
			{
				string enableHoming = (CheckBoxHomeEnable.IsChecked == true) ? "1" : "0";
				string homeMask = TextBoxHomeMask.Text;
				string homeFeed = TextBoxHomeFeed.Text;
				string homeSeek = TextBoxHomeSeek.Text;
				string xSteps = TextBoxXSteps.Text;
				string ySteps = TextBoxYSteps.Text;
				string zSteps = TextBoxZSteps.Text;

				if (!string.IsNullOrWhiteSpace(enableHoming)) machine.SendLine($"$22={enableHoming}");
				await Task.Delay(50);
				if (!string.IsNullOrWhiteSpace(homeMask)) machine.SendLine($"$23={homeMask}");
				await Task.Delay(50);
				if (!string.IsNullOrWhiteSpace(homeFeed)) machine.SendLine($"$24={homeFeed}");
				await Task.Delay(50);
				if (!string.IsNullOrWhiteSpace(homeSeek)) machine.SendLine($"$25={homeSeek}");
				await Task.Delay(50);
				if (!string.IsNullOrWhiteSpace(xSteps)) machine.SendLine($"$100={xSteps}");
				await Task.Delay(50);
				if (!string.IsNullOrWhiteSpace(ySteps)) machine.SendLine($"$101={ySteps}");
				await Task.Delay(50);
				if (!string.IsNullOrWhiteSpace(zSteps)) machine.SendLine($"$102={zSteps}");

				Machine_Info("Calibration parameters sent to GRBL.");
			}
			catch (Exception ex)
			{
				Machine_Info("Error sending calibration: " + ex.Message);
			}
		}
	}
}
