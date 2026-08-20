using OpenCNCPilot.Communication;
using OpenCNCPilot.Util;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace OpenCNCPilot
{
	partial class MainWindow
	{
		private List<string> ManualCommands = new List<string>();   //pos 0 is the last command sent, pos1+ are older
		private int ManualCommandIndex = -1;
		private bool ManualExpressionSuccess = true;

		void ManualSend()
		{
			if (machine.Mode != Machine.OperatingMode.Manual)
				return;

			string tosend;

			if (Properties.Settings.Default.ManualUseExpressions)
			{
				if (ManualExpressionSuccess)
					tosend = TextBoxPreview.Text;
				else
				{
					Machine_NonFatalException("Expression did not evaluate");
					return;
				}
			}
			else
				tosend = TextBoxManual.Text;

			machine.SendLine(tosend);

			ManualCommands.Insert(0, tosend);
			ManualCommandIndex = -1;

			TextBoxManual.Text = "";
		}

		private void UpdateExpressionPreview()
		{
			if (Properties.Settings.Default.ManualUseExpressions)
				TextBoxPreview.Text = machine.Calculator.Evaluate(TextBoxManual.Text, out ManualExpressionSuccess);

			TextBoxPreview.Background = ManualExpressionSuccess ? Brushes.LightYellow : Brushes.Red;
		}

		private void TextBoxManual_TextChanged(object sender, TextChangedEventArgs e)
		{
			UpdateExpressionPreview();
		}

		private void CheckBoxUseExpressions_Changed(object sender, RoutedEventArgs e)
		{
			TextBoxPreview.Visibility = Properties.Settings.Default.ManualUseExpressions ? Visibility.Visible : Visibility.Collapsed;
			UpdateExpressionPreview();
		}

		private void ButtonManualSend_Click(object sender, RoutedEventArgs e)
		{
			ManualSend();
		}

		private void TextBoxManual_PreviewKeyDown(object sender, KeyEventArgs e)
		{
			if (e.Key == Key.Enter)
			{
				e.Handled = true;
				ManualSend();
			}
			else if (e.Key == Key.Down)
			{
				e.Handled = true;

				if (ManualCommandIndex == 0)
				{
					TextBoxManual.Text = "";
					ManualCommandIndex = -1;
				}
				else if (ManualCommandIndex > 0)
				{
					ManualCommandIndex--;
					TextBoxManual.Text = ManualCommands[ManualCommandIndex];
					TextBoxManual.SelectionStart = TextBoxManual.Text.Length;
				}
			}
			else if (e.Key == Key.Up)
			{
				e.Handled = true;

				if (ManualCommands.Count > ManualCommandIndex + 1)
				{
					ManualCommandIndex++;
					TextBoxManual.Text = ManualCommands[ManualCommandIndex];
					TextBoxManual.SelectionStart = TextBoxManual.Text.Length;
				}
			}
		}

		private void ButtonManualSetG10Zero_Click(object sender, RoutedEventArgs e)
		{
			if (machine.Mode != Machine.OperatingMode.Manual)
				return;

			TextBoxManual.Text = $"G10 L2 P0 X(MX) Y(MY) Z(MZ-TLO)";
			CheckBoxUseExpressions.IsChecked = true;
		}

		private void ButtonManualSetG92Zero_Click(object sender, RoutedEventArgs e)
		{
			if (machine.Mode != Machine.OperatingMode.Manual)
				return;

			TextBoxManual.Text = "G92 X0 Y0 Z0";
		}

		private void ButtonManualResetG10_Click(object sender, RoutedEventArgs e)
		{
			if (machine.Mode != Machine.OperatingMode.Manual)
				return;

			TextBoxManual.Text = "G10 L2 P0 X0 Y0 Z0";
		}

		private void CheckBoxEnableJog_Checked(object sender, RoutedEventArgs e)
		{
			if (machine.Mode != Machine.OperatingMode.Manual)
			{
				CheckBoxEnableJog.IsChecked = false;
				return;
			}
		}

		private void CheckBoxEnableJog_Unchecked(object sender, RoutedEventArgs e)
		{
			if (!machine.Connected)
				return;
			machine.JogCancel();
		}

		private void Jogging_KeyDown(object sender, KeyEventArgs e)
		{
			if (!machine.Connected)
				return;

			if (e.Key == Key.Escape)
			{
				if (Properties.Settings.Default.EnableEscapeSoftReset)
					machine.SoftReset();
				else
					machine.JogCancel();
			}

			if (!CheckBoxEnableJog.IsChecked.Value)
				return;

			e.Handled = e.Key != Key.Tab;

			if (e.IsRepeat)
				return;

			if (machine.BufferState > 0 || machine.Status != "Idle")
				return;

			string direction = null;

			if (Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift))
			{
				if (e.Key == Key.Up)
					direction = "Z";
				else if (e.Key == Key.Down)
					direction = "Z-";
			}
			else
			{
				if (e.Key == Key.Right)
					direction = "X";
				else if (e.Key == Key.Left)
					direction = "X-";
				else if (e.Key == Key.Up)
					direction = "Y";
				else if (e.Key == Key.Down)
					direction = "Y-";
				else if (e.Key == Key.PageUp)
					direction = "Z";
				else if (e.Key == Key.PageDown)
					direction = "Z-";
			}

			double feed = Properties.Settings.Default.JogFeed;
			double distance = Properties.Settings.Default.JogDistance;

			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				feed = Properties.Settings.Default.JogFeedCtrl;
				distance = Properties.Settings.Default.JogDistanceCtrl;
			}

			if (direction != null)
			{
				machine.SendLine(string.Format(Constants.DecimalOutputFormat, "$J=G91F{0:0.#}{1}{2:0.###}", feed, direction, distance));
			}
		}

		private void ButtonJog_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
		{
			if (!machine.Connected)
			{
				MessageBox.Show("Machine is not connected!");
				return;
			}

			if (machine.Status == "Alarm")
			{
				MessageBox.Show("Machine is in Alarm state! You must Home ($H) or Unlock ($X) first.");
				return;
			}

			if (machine.BufferState > 0 || machine.Status != "Idle")
			{
				// Only show message box if it's not already jogging, to avoid spam
				if (machine.Status != "Jog")
					MessageBox.Show("Machine must be Idle to jog. Current status: " + machine.Status);
				return;
			}

			string direction = (sender as Button)?.Tag as string;
			if (direction == null) return;

			double feed = Properties.Settings.Default.JogFeed;
			
			// Use a very large distance for continuous joystick-style jogging
			double distance = 10000.0;

			if (Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
			{
				feed = Properties.Settings.Default.JogFeedCtrl;
			}

			string distStr = distance.ToString("0.###", Constants.DecimalOutputFormat);
			string axisCommand = "";

			if (direction == "X-Y") axisCommand = $"X-{distStr} Y{distStr}";
			else if (direction == "XY") axisCommand = $"X{distStr} Y{distStr}";
			else if (direction == "X-Y-") axisCommand = $"X-{distStr} Y-{distStr}";
			else if (direction == "XY-") axisCommand = $"X{distStr} Y-{distStr}";
			else if (direction == "X-") axisCommand = $"X-{distStr}";
			else if (direction == "X") axisCommand = $"X{distStr}";
			else if (direction == "Y-") axisCommand = $"Y-{distStr}";
			else if (direction == "Y") axisCommand = $"Y{distStr}";
			else if (direction == "Z-") axisCommand = $"Z-{distStr}";
			else if (direction == "Z") axisCommand = $"Z{distStr}";

			machine.SendLine(string.Format(Constants.DecimalOutputFormat, "$J=G91 {0} F{1:0.#}", axisCommand, feed));
		}

		private void ButtonJog_MouseUp(object sender, System.Windows.Input.MouseEventArgs e)
		{
			if (!machine.Connected) return;
			machine.JogCancel();
		}

		private void ButtonJog_Click(object sender, RoutedEventArgs e)
		{
			string direction = (sender as Button)?.Tag as string;
			if (direction == "STOP")
			{
				machine.JogCancel();
			}
		}

		private void ButtonPresetStep_Click(object sender, RoutedEventArgs e)
		{
			if (double.TryParse((sender as Button)?.Content?.ToString(), out double val))
			{
				Properties.Settings.Default.JogDistance = val;
				Properties.Settings.Default.Save();
			}
		}

		private void ButtonPresetFeed_Click(object sender, RoutedEventArgs e)
		{
			if (double.TryParse((sender as Button)?.Content?.ToString(), out double val))
			{
				Properties.Settings.Default.JogFeed = val;
				Properties.Settings.Default.Save();
			}
		}

		private void Jogging_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
		{
			machine.JogCancel();
		}

		private void Jogging_KeyUp(object sender, KeyEventArgs e)
		{
			machine.JogCancel();
		}

		private void Window_PreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
		{
			if (machine.Connected && e.Key == Key.Escape && Properties.Settings.Default.EnableEscapeSoftReset)
				machine.SoftReset();
		}
	}
}
