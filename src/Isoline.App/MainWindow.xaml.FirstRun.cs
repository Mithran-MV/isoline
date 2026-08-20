using System;
using System.Windows;

namespace Isoline
{
	partial class MainWindow
	{
		/// <summary>
		/// Runs the connection wizard on the very first launch, then offers to connect.
		/// </summary>
		private void ShowFirstRunSetup()
		{
			FirstRunWindow window = new FirstRunWindow() { Owner = this };

			window.ShowDialog();

			if (!window.ShouldConnect)
				return;

			try
			{
				machine.Connect();
			}
			catch (Exception ex)
			{
				ShowNotice("Could not connect", ex.Message +
					"  Check the cable and the port, then press Connect.");
			}
		}
	}
}
