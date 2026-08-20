using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Isoline.Communication;
using Isoline.Firmware;

namespace Isoline
{
	/// <summary>
	/// The first-run connection wizard. Writes straight into the application settings and
	/// reports whether the user asked to connect immediately.
	/// </summary>
	public partial class FirstRunWindow : Window
	{
		/// <summary>True when the user pressed "Save and connect" rather than "Skip".</summary>
		public bool ShouldConnect { get; private set; }

		public FirstRunWindow()
		{
			InitializeComponent();

			ComboFirmware.ItemsSource = FirmwareProfile.All.Select(p => p.Name).ToList();
			ComboFirmware.SelectedItem = Properties.Settings.Default.FirmwareType;

			if (ComboFirmware.SelectedItem == null)
				ComboFirmware.SelectedIndex = 0;

			ComboFirmware.SelectionChanged += (s, e) => UpdateFirmwareHint();

			ComboConnection.ItemsSource = Enum.GetValues(typeof(ConnectionType)).Cast<ConnectionType>().ToList();
			ComboConnection.SelectedItem = Properties.Settings.Default.ConnectionType;

			ComboBaud.ItemsSource = new[] { 9600, 19200, 38400, 57600, 115200, 230400, 250000 };
			ComboBaud.SelectedItem = Properties.Settings.Default.SerialPortBaud;

			if (ComboBaud.SelectedItem == null)
				ComboBaud.SelectedItem = 115200;

			TextBoxHost.Text = Properties.Settings.Default.EthernetIP;
			TextBoxPort.Text = Properties.Settings.Default.EthernetPort.ToString();

			RefreshPorts();
			UpdateFirmwareHint();
		}

		private void UpdateFirmwareHint()
		{
			FirmwareProfile profile = FirmwareProfile.ForName(ComboFirmware.SelectedItem as string);

			LabelFirmwareHint.Text = string.Format(
				"{0} byte receive buffer{1}.",
				profile.BufferSize,
				profile.PrefersNetwork ? ", usually reached over WiFi" : "");
		}

		private void RefreshPorts()
		{
			List<string> ports = SerialPort.GetPortNames().OrderBy(p => p).ToList();

			ComboPort.ItemsSource = ports;

			string saved = Properties.Settings.Default.SerialPortName;

			ComboPort.SelectedItem = ports.Contains(saved)
				? saved
				: ports.FirstOrDefault();
		}

		private void ButtonRefresh_Click(object sender, RoutedEventArgs e)
		{
			RefreshPorts();
		}

		private void ComboConnection_SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (PanelSerial == null || PanelNetwork == null)
				return;

			bool serial = (ComboConnection.SelectedItem as ConnectionType?) == ConnectionType.Serial;

			PanelSerial.Visibility = serial ? Visibility.Visible : Visibility.Collapsed;
			PanelNetwork.Visibility = serial ? Visibility.Collapsed : Visibility.Visible;
		}

		private void ButtonSkip_Click(object sender, RoutedEventArgs e)
		{
			Properties.Settings.Default.FirstRunComplete = true;
			Properties.Settings.Default.Save();

			ShouldConnect = false;
			Close();
		}

		private void ButtonSave_Click(object sender, RoutedEventArgs e)
		{
			var settings = Properties.Settings.Default;

			settings.FirmwareType = ComboFirmware.SelectedItem as string ?? "Grbl";
			settings.ConnectionType = (ComboConnection.SelectedItem as ConnectionType?) ?? ConnectionType.Serial;

			if (settings.ConnectionType == ConnectionType.Serial)
			{
				if (ComboPort.SelectedItem == null)
				{
					MessageBox.Show(
						"No serial port is selected.\n\nPlug the controller in, press Refresh, and pick the port it appears as.",
						"Choose a port", MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				settings.SerialPortName = ComboPort.SelectedItem as string;
				settings.SerialPortBaud = (int)ComboBaud.SelectedItem;
			}
			else
			{
				int port;

				if (!int.TryParse(TextBoxPort.Text, out port))
				{
					MessageBox.Show("The port has to be a number.", "Check the address",
						MessageBoxButton.OK, MessageBoxImage.Information);
					return;
				}

				settings.EthernetIP = TextBoxHost.Text;
				settings.EthernetPort = port;
			}

			settings.FirstRunComplete = true;
			settings.Save();

			GrblCodeTranslator.Reload(settings.FirmwareType);

			ShouldConnect = true;
			Close();
		}
	}
}
