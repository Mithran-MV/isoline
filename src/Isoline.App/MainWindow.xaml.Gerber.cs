using System;
using System.Windows;
using Isoline.Communication;

namespace Isoline
{
	partial class MainWindow
	{
		/// <summary>
		/// Opens the Gerber importer and loads whatever it produces as the current job.
		/// </summary>
		private void ButtonImportGerber_Click(object sender, RoutedEventArgs e)
		{
			if (machine.Mode == Machine.OperatingMode.SendFile)
				return;

			GerberImportWindow window = new GerberImportWindow() { Owner = this };

			if (window.ShowDialog() != true || window.GeneratedGCode == null)
				return;

			try
			{
				machine.SetFile(window.GeneratedGCode.ToArray());
				CurrentFileName = window.SourceName + " (isolation)";
				HeightMapApplied = false;

				ShowNotice("Isolation toolpath ready",
					"Probe the board and apply the height map before cutting - that is the whole point of " +
					"engraving with a V-bit, and it takes a minute.");
			}
			catch (Exception ex)
			{
				MessageBox.Show(ex.Message, "Could not load the generated toolpath",
					MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}
	}
}
