using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Clipper2Lib;
using Isoline.Gerber;
using Isoline.Toolpaths;
using Microsoft.Win32;

namespace Isoline
{
	/// <summary>
	/// Reads a Gerber copper layer and turns it into an isolation program.
	/// <para>
	/// The preview panel recomputes the offset contours as the parameters change, so the
	/// consequence of a 0.2 mm cutter versus a 0.4 mm one - traces that survive, or traces
	/// that get swallowed - is visible before anything is cut.
	/// </para>
	/// </summary>
	public partial class GerberImportWindow : Window
	{
		private GerberDocument document;
		private string sourcePath;

		/// <summary>The generated program, or null if the user cancelled.</summary>
		public List<string> GeneratedGCode { get; private set; }

		/// <summary>Name to show in the title bar once the toolpath is loaded.</summary>
		public string SourceName { get; private set; }

		public GerberImportWindow()
		{
			InitializeComponent();
			LoadDefaults();
		}

		private void LoadDefaults()
		{
			var s = Properties.Settings.Default;

			CheckVBit.IsChecked = s.IsolationUseVBit;
			TextVAngle.Text = Text(s.IsolationVBitAngle);
			TextVTip.Text = Text(s.IsolationVBitTip);
			TextDiameter.Text = Text(s.IsolationToolDiameter);
			TextDepth.Text = Text(s.IsolationCutDepth);
			TextPasses.Text = s.IsolationPasses.ToString(CultureInfo.InvariantCulture);
			TextStepover.Text = Text(s.IsolationStepover);
			TextFeed.Text = Text(s.IsolationFeedRate);
			TextPlunge.Text = Text(s.IsolationPlungeRate);
			TextSafe.Text = Text(s.IsolationSafeHeight);
			TextSpindle.Text = Text(s.IsolationSpindleSpeed);
			CheckOptimise.IsChecked = s.IsolationOptimiseTravel;
		}

		private void SaveDefaults(IsolationOptions options)
		{
			var s = Properties.Settings.Default;

			s.IsolationUseVBit = CheckVBit.IsChecked == true;
			s.IsolationVBitAngle = Number(TextVAngle, 30);
			s.IsolationVBitTip = Number(TextVTip, 0.1);
			s.IsolationToolDiameter = Number(TextDiameter, 0.4);
			s.IsolationCutDepth = options.CutDepth;
			s.IsolationPasses = options.Passes;
			s.IsolationStepover = options.Stepover;
			s.IsolationFeedRate = options.FeedRate;
			s.IsolationPlungeRate = options.PlungeRate;
			s.IsolationSafeHeight = options.SafeHeight;
			s.IsolationSpindleSpeed = options.SpindleSpeed;
			s.IsolationOptimiseTravel = options.OptimiseTravel;
			s.Save();
		}

		private static string Text(double value)
		{
			return value.ToString("0.###", CultureInfo.InvariantCulture);
		}

		private static double Number(TextBox box, double fallback)
		{
			double value;

			return box != null && double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
				? value
				: fallback;
		}

		private void ButtonBrowse_Click(object sender, RoutedEventArgs e)
		{
			OpenFileDialog dialog = new OpenFileDialog()
			{
				Filter = "Gerber files|*.gbr;*.ger;*.gbl;*.gtl;*.art;*.pho;*.gerber|All files|*.*",
				Title = "Choose a copper layer",
			};

			if (dialog.ShowDialog() != true)
				return;

			sourcePath = dialog.FileName;
			TextBoxFile.Text = sourcePath;

			try
			{
				document = GerberParser.ParseFile(sourcePath);
			}
			catch (Exception ex)
			{
				document = null;
				MessageBox.Show("Could not read that file:\n\n" + ex.Message, "Gerber",
					MessageBoxButton.OK, MessageBoxImage.Warning);
			}

			UpdatePreview();
		}

		private void Parameter_Changed(object sender, RoutedEventArgs e)
		{
			if (PanelVBit == null || PanelStraight == null)
				return;

			bool vbit = CheckVBit.IsChecked == true;

			PanelVBit.Visibility = vbit ? Visibility.Visible : Visibility.Collapsed;
			PanelStraight.Visibility = vbit ? Visibility.Collapsed : Visibility.Visible;

			UpdatePreview();
		}

		private void Parameter_Changed(object sender, TextChangedEventArgs e)
		{
			UpdatePreview();
		}

		/// <summary>
		/// Effective cut width: for a V-bit this depends on how deep it is going, which is
		/// exactly the parameter people get wrong when they treat it like an end mill.
		/// </summary>
		private double EffectiveToolDiameter()
		{
			if (CheckVBit.IsChecked != true)
				return Number(TextDiameter, 0.4);

			return IsolationOptions.VBitWidth(
				Number(TextVAngle, 30),
				Math.Abs(Number(TextDepth, 0.1)),
				Number(TextVTip, 0));
		}

		private IsolationOptions BuildOptions()
		{
			return new IsolationOptions()
			{
				ToolDiameter = EffectiveToolDiameter(),
				Passes = Math.Max(1, (int)Number(TextPasses, 1)),
				Stepover = Number(TextStepover, 0.8),
				CutDepth = -Math.Abs(Number(TextDepth, 0.1)),
				SafeHeight = Math.Abs(Number(TextSafe, 2)),
				FeedRate = Number(TextFeed, 300),
				PlungeRate = Number(TextPlunge, 100),
				SpindleSpeed = Math.Max(0, Number(TextSpindle, 0)),
				OptimiseTravel = CheckOptimise.IsChecked == true,
			};
		}

		private void UpdatePreview()
		{
			if (LabelCutWidth == null)
				return;

			double width = EffectiveToolDiameter();

			LabelCutWidth.Text = CheckVBit.IsChecked == true
				? string.Format(CultureInfo.InvariantCulture,
					"{0:0.###} mm at {1:0.###} mm deep. Going deeper widens the cut - and the gap it leaves between traces.",
					width, Math.Abs(Number(TextDepth, 0.1)))
				: string.Format(CultureInfo.InvariantCulture, "{0:0.###} mm", width);

			if (document == null)
			{
				ButtonGenerate.IsEnabled = false;
				LabelSummary.Text = "Choose a Gerber file to see what will be cut.";
				ListWarnings.ItemsSource = null;
				LabelNoWarnings.Visibility = Visibility.Visible;
				return;
			}

			ListWarnings.ItemsSource = document.Warnings;
			LabelNoWarnings.Visibility = document.Warnings.Count == 0 ? Visibility.Visible : Visibility.Collapsed;

			if (document.IsEmpty)
			{
				ButtonGenerate.IsEnabled = false;
				LabelSummary.Text = "No copper was found in that file. Copper layers usually end in .gbr, .gtl or .gbl - " +
					"a drill or outline file will not work here.";
				return;
			}

			try
			{
				IsolationOptions options = BuildOptions();
				List<PathsD> passes = IsolationToolpathGenerator.GenerateContours(document.Copper, options);

				int contours = passes.Sum(p => p.Count);
				int points = passes.Sum(p => p.Sum(c => c.Count));

				LabelSummary.Text = string.Format(CultureInfo.InvariantCulture,
					"Board is {0:0.##} x {1:0.##} mm, from ({2:0.##}, {3:0.##}) to ({4:0.##}, {5:0.##}).\n\n" +
					"{6} contours over {7} pass(es), {8} points.\n\n" +
					"Cutting {9:0.###} mm deep at {10:0} mm/min.",
					document.Size.X, document.Size.Y,
					document.Min.X, document.Min.Y, document.Max.X, document.Max.Y,
					contours, passes.Count, points,
					Math.Abs(options.CutDepth), options.FeedRate);

				ButtonGenerate.IsEnabled = contours > 0;
			}
			catch (Exception ex)
			{
				ButtonGenerate.IsEnabled = false;
				LabelSummary.Text = ex.Message;
			}
		}

		private void ButtonGenerate_Click(object sender, RoutedEventArgs e)
		{
			if (document == null)
				return;

			try
			{
				IsolationOptions options = BuildOptions();

				GeneratedGCode = IsolationToolpathGenerator.Generate(document.Copper, options);
				SourceName = System.IO.Path.GetFileName(sourcePath);

				SaveDefaults(options);

				DialogResult = true;
				Close();
			}
			catch (Exception ex)
			{
				MessageBox.Show("Could not generate the toolpath:\n\n" + ex.Message, "Isolation",
					MessageBoxButton.OK, MessageBoxImage.Warning);
			}
		}

		private void ButtonCancel_Click(object sender, RoutedEventArgs e)
		{
			DialogResult = false;
			Close();
		}
	}
}
