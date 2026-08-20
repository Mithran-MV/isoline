using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using Isoline.Communication;
using Isoline.Machines;

namespace Isoline
{
	/// <summary>
	/// Works out a corrected steps/mm from a commanded move and a measured one.
	/// <para>
	/// corrected = current * (commanded / measured). If the machine was told to move 50 mm
	/// and only moved 49 mm, it is taking too few steps per millimetre, and the value has
	/// to go up by the same ratio.
	/// </para>
	/// </summary>
	public partial class CalibrationWizardWindow : Window
	{
		private readonly Machine machine;
		private readonly ControllerSettings settings;

		public char Axis { get; private set; } = 'X';
		public double OriginalValue { get; private set; }

		/// <summary>The corrected steps/mm, or null if the user cancelled.</summary>
		public double? CorrectedValue { get; private set; }

		public CalibrationWizardWindow(Machine machine, ControllerSettings settings)
		{
			InitializeComponent();

			this.machine = machine;
			this.settings = settings;

			UpdateCurrentValue();
		}

		private int SettingNumber
		{
			get
			{
				return Axis == 'X' ? ControllerSettings.StepsPerMmX
					: Axis == 'Y' ? ControllerSettings.StepsPerMmY
					: ControllerSettings.StepsPerMmZ;
			}
		}

		private void Axis_Checked(object sender, RoutedEventArgs e)
		{
			if (LabelCurrent == null)
				return;

			Axis = RadioX.IsChecked == true ? 'X' : RadioY.IsChecked == true ? 'Y' : 'Z';

			UpdateCurrentValue();
			UpdateResult();
		}

		private void UpdateCurrentValue()
		{
			double value;

			if (settings.TryGet(SettingNumber, out value))
			{
				OriginalValue = value;
				LabelCurrent.Text = string.Format(CultureInfo.InvariantCulture,
					"Current value: ${0} = {1:0.###} steps/mm", SettingNumber, value);
			}
			else
			{
				OriginalValue = 0;
				LabelCurrent.Text = string.Format(
					"Current value of ${0} is unknown. Close this, press \"Read from controller\", then come back.",
					SettingNumber);
			}
		}

		private static double Number(TextBox box, double fallback)
		{
			double value;

			return double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
				? value
				: fallback;
		}

		private void ButtonMove_Click(object sender, RoutedEventArgs e)
		{
			double distance = Number(TextCommanded, 0);
			double feed = Number(TextFeed, 500);

			if (distance == 0)
			{
				MessageBox.Show("Enter the distance to move.", "Test move",
					MessageBoxButton.OK, MessageBoxImage.Information);
				return;
			}

			MessageBoxResult confirm = MessageBox.Show(
				string.Format(CultureInfo.InvariantCulture,
					"Move {0} by {1:0.###} mm at {2:0} mm/min?\n\nThe move is relative to the current position. " +
					"Check that the axis has room to travel that far.", Axis, distance, feed),
				"Run test move", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

			if (confirm != MessageBoxResult.OK)
				return;

			// G91 for one move only, then straight back to absolute - leaving the machine in
			// relative mode is a good way to ruin the next job that assumes otherwise.
			machine.SendLine("G91");
			machine.SendLine(string.Format(CultureInfo.InvariantCulture,
				"G1 {0}{1:0.###} F{2:0.###}", Axis, distance, feed));
			machine.SendLine("G90");
		}

		private void TextMeasured_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateResult();
		}

		private void UpdateResult()
		{
			if (LabelResult == null)
				return;

			double commanded = Number(TextCommanded, 0);
			double measured = Number(TextMeasured, 0);

			CorrectedValue = null;
			ButtonApply.IsEnabled = false;

			if (OriginalValue <= 0)
			{
				LabelResult.Text = "The current steps/mm has to be read from the controller first.";
				return;
			}

			if (commanded == 0 || measured == 0)
			{
				LabelResult.Text = "Fill in the measured distance to see the corrected value.";
				return;
			}

			double corrected = OriginalValue * (commanded / measured);
			double errorPercent = (commanded - measured) / commanded * 100;

			if (corrected <= 0 || double.IsNaN(corrected) || double.IsInfinity(corrected))
			{
				LabelResult.Text = "That measurement does not produce a sensible value - check the sign.";
				return;
			}

			// A correction of more than a few percent usually means a mistyped measurement
			// or the wrong axis, not a genuinely miscalibrated machine.
			string caution = Math.Abs(errorPercent) > 20
				? "\n\nThat is a very large correction. Double-check the measurement and the axis before applying it."
				: "";

			LabelResult.Text = string.Format(CultureInfo.InvariantCulture,
				"Commanded {0:0.###} mm, measured {1:0.###} mm - out by {2:0.##}%.\n\n" +
				"${3} should be {4:0.###} steps/mm instead of {5:0.###}.{6}",
				commanded, measured, errorPercent, SettingNumber, corrected, OriginalValue, caution);

			CorrectedValue = corrected;
			ButtonApply.IsEnabled = true;
		}

		private void ButtonApply_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = true;
			Close();
		}

		private void ButtonCancel_Click(object sender, RoutedEventArgs e)
		{
			CorrectedValue = null;
			DialogResult = false;
			Close();
		}
	}
}
