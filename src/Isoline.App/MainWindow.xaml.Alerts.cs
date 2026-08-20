using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shell;
using Isoline.Machines;

namespace Isoline
{
	partial class MainWindow
	{
		private readonly JobProgress jobProgress = new JobProgress();

		#region Alert banner

		/// <summary>
		/// Shows a decoded alarm or error above the workspace. Alarms offer an Unlock
		/// button because that is what has to happen next in every case.
		/// </summary>
		public void ShowAlert(AlarmInfo info)
		{
			if (info == null)
				return;

			AlertTitle.Text = info.Title;
			AlertBody.Text = info.Remedy;

			Brush accent = StatusPresentation.Brush(info.IsAlarm ? "DangerBrush" : "WarningBrush");
			Brush text = StatusPresentation.Brush(info.IsAlarm ? "DangerTextBrush" : "WarningTextBrush");

			AlertAccent.Background = accent;
			AlertBanner.BorderBrush = accent;
			AlertTitle.Foreground = text;

			ButtonAlertAction.Visibility = info.IsAlarm ? Visibility.Visible : Visibility.Collapsed;
			AlertBanner.Visibility = Visibility.Visible;
		}

		/// <summary>Shows a plain informational message in the same place.</summary>
		public void ShowNotice(string title, string body)
		{
			AlertTitle.Text = title;
			AlertBody.Text = body;

			AlertAccent.Background = StatusPresentation.Brush("AccentBrush");
			AlertBanner.BorderBrush = StatusPresentation.Brush("AccentBrush");
			AlertTitle.Foreground = StatusPresentation.Brush("AccentTextBrush");

			ButtonAlertAction.Visibility = Visibility.Collapsed;
			AlertBanner.Visibility = Visibility.Visible;
		}

		private void ButtonAlertDismiss_Click(object sender, RoutedEventArgs e)
		{
			AlertBanner.Visibility = Visibility.Collapsed;
		}

		private void ButtonAlertAction_Click(object sender, RoutedEventArgs e)
		{
			if (resumeArmed)
			{
				ResumePendingJob();
				return;
			}

			// $X clears the alarm lock. Position is not trusted afterwards, which is what
			// the banner text has just told the operator.
			machine.SendLine("$X");
			AlertBanner.Visibility = Visibility.Collapsed;
		}

		#endregion

		#region State pill

		private void UpdateStatePill()
		{
			StatusStyle style = StatusPresentation.For(machine.Connected ? machine.Status : null);

			ButtonStatus.Text = style.Label;
			ButtonStatus.Foreground = StatusPresentation.Brush(style.TextKey);
			StatePillDot.Fill = StatusPresentation.Brush(style.TextKey);
			StatePillBorder.Background = StatusPresentation.Brush(style.FillKey);
			StatePillBorder.ToolTip = style.Tooltip;
		}

		#endregion

		#region Height map colour ramp

		/// <summary>
		/// Builds the height map material and the matching legend from a single colour ramp.
		/// </summary>
		private void ApplyHeightMapColours()
		{
			System.Windows.Media.Brush ramp = Visuals.Colormap.CreateBrush(
				Properties.Settings.Default.HeightMapOpacity, vertical: false);

			ModelHeightMap.Material = new System.Windows.Media.Media3D.DiffuseMaterial(ramp);
			HeightMapLegend.Background = Visuals.Colormap.CreateBrush(1.0, vertical: false);
		}

		#endregion

		#region Job progress

		private void UpdateJobProgress()
		{
			if (!jobProgress.Running && jobProgress.Total == 0)
			{
				JobProgressBar.Visibility = Visibility.Collapsed;
				TaskbarInfo.ProgressState = TaskbarItemProgressState.None;
				return;
			}

			JobProgressBar.Visibility = Visibility.Visible;

			double fraction = jobProgress.Fraction;

			BarJobProgress.Value = fraction;
			LabelJobPercent.Text = (fraction * 100).ToString("0") + "%";

			TimeSpan? remaining = jobProgress.Remaining;

			LabelJobTime.Text = remaining == null
				? JobProgress.Format(jobProgress.Elapsed) + " elapsed"
				: JobProgress.Format(jobProgress.Elapsed) + " elapsed  ·  " + JobProgress.Format(remaining) + " left";

			// mirror onto the taskbar button so a long job can be watched from another window
			TaskbarInfo.ProgressValue = fraction;
			TaskbarInfo.ProgressState = !jobProgress.Running
				? TaskbarItemProgressState.Paused
				: (machine.Status != null && machine.Status.StartsWith("Alarm", StringComparison.OrdinalIgnoreCase)
					? TaskbarItemProgressState.Error
					: TaskbarItemProgressState.Normal);
		}

		#endregion
	}
}
