using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using Isoline.Communication;
using Isoline.GCode;
using Isoline.GCode.GCodeCommands;
using Isoline.Jobs;
using Isoline.Util;

namespace Isoline
{
	partial class MainWindow
	{
		/// <summary>
		/// Where the running job's position is kept between sessions. Next to the executable
		/// rather than in the user profile, so it travels with a portable install.
		/// </summary>
		private static readonly string RecoveryFile = Path.Combine(
			AppDomain.CurrentDomain.BaseDirectory, "recovery.json");

		private JobRecoveryState recoveryState;
		private DateTime lastRecoveryWrite = DateTime.MinValue;

		/// <summary>
		/// Records the current position, at most once a second. Writing on every acknowledged
		/// line would put a file write in the middle of the streaming loop.
		/// </summary>
		private void SaveRecoveryPoint()
		{
			if (!Properties.Settings.Default.JobRecoveryEnabled)
				return;

			if (machine.Mode != Machine.OperatingMode.SendFile || recoveryState == null)
				return;

			if ((DateTime.UtcNow - lastRecoveryWrite).TotalSeconds < 1)
				return;

			lastRecoveryWrite = DateTime.UtcNow;

			recoveryState.CompletedLines = machine.FilePosition;
			recoveryState.WorkOffsetX = machine.WorkOffset.X;
			recoveryState.WorkOffsetY = machine.WorkOffset.Y;
			recoveryState.WorkOffsetZ = machine.WorkOffset.Z;
			recoveryState.SafeZ = Properties.Settings.Default.ProbeSafeHeight;

			try
			{
				recoveryState.Save(RecoveryFile);
			}
			catch (IOException)
			{
				// a failed recovery write must never interrupt the job
			}
		}

		/// <summary>Called when a job starts, to lay down the state a resume would need.</summary>
		private void BeginRecoveryTracking()
		{
			if (!Properties.Settings.Default.JobRecoveryEnabled || machine.File.Count == 0)
				return;

			recoveryState = new JobRecoveryState()
			{
				FilePath = CurrentFileName,
				FileHash = JobRecoveryState.HashLines(machine.File),
				TotalLines = machine.File.Count,
				CompletedLines = machine.FilePosition,
				SafeZ = Properties.Settings.Default.ProbeSafeHeight,
				FeedRate = LastCommandedFeed(machine.FilePosition),
				SpindleSpeed = LastCommandedSpindle(machine.FilePosition),
			};

			lastRecoveryWrite = DateTime.MinValue;
			SaveRecoveryPoint();
		}

		private void EndRecoveryTracking()
		{
			recoveryState = null;

			try
			{
				JobRecoveryState.Clear(RecoveryFile);
			}
			catch (IOException) { }
		}

		/// <summary>
		/// On start-up, offer to pick up an interrupted job. The offer is only made when the
		/// file on disk still hashes to what was running - resuming into a file that has been
		/// re-exported since would put the tool in the wrong place.
		/// </summary>
		private void OfferRecovery()
		{
			if (!Properties.Settings.Default.JobRecoveryEnabled)
				return;

			JobRecoveryState state = JobRecoveryState.Load(RecoveryFile);

			if (state == null || state.CompletedLines <= 0 || state.CompletedLines >= state.TotalLines)
				return;

			pendingRecovery = state;

			ShowNotice(
				"An interrupted job was found",
				$"\"{state.FilePath}\" stopped at line {state.CompletedLines} of {state.TotalLines} " +
				$"({state.Progress * 100:0}%). Load the same file and press Resume to carry on from there.");

			ButtonAlertAction.Content = "Resume";
			ButtonAlertAction.Visibility = Visibility.Visible;
			resumeArmed = true;
		}

		private JobRecoveryState pendingRecovery;
		private bool resumeArmed;

		/// <summary>
		/// Restarts an interrupted job: restore modal state, lift, travel, spin up, plunge,
		/// and only then continue streaming from the line after the last acknowledged one.
		/// </summary>
		private void ResumePendingJob()
		{
			resumeArmed = false;

			if (pendingRecovery == null)
				return;

			if (machine.File.Count == 0)
			{
				MessageBox.Show("Load the job's G-code file first, then press Resume again.",
					"Nothing to resume", MessageBoxButton.OK, MessageBoxImage.Information);
				resumeArmed = true;
				return;
			}

			if (!pendingRecovery.MatchesFile(machine.File))
			{
				MessageBox.Show(
					"The loaded file is not the one that was interrupted - it has been edited or re-exported.\n\n" +
					"Resuming into a different file would put the tool in the wrong place, so the resume has been cancelled.",
					"File does not match", MessageBoxButton.OK, MessageBoxImage.Warning);

				pendingRecovery = null;
				AlertBanner.Visibility = Visibility.Collapsed;
				return;
			}

			if (machine.Mode != Machine.OperatingMode.Manual)
			{
				MessageBox.Show("Connect and make sure the machine is idle before resuming.",
					"Not ready", MessageBoxButton.OK, MessageBoxImage.Information);
				resumeArmed = true;
				return;
			}

			int resumeLine = Math.Max(0, pendingRecovery.CompletedLines - 1);
			Vector3 resumePoint = PositionAtLine(resumeLine);

			MessageBoxResult confirm = MessageBox.Show(
				$"Resume at line {resumeLine + 1} of {machine.File.Count}?\n\n" +
				$"The tool will lift to Z{pendingRecovery.SafeZ:0.###}, travel to " +
				$"X{resumePoint.X:0.###} Y{resumePoint.Y:0.###}, then plunge to Z{resumePoint.Z:0.###}.\n\n" +
				"Check that the work offset is still the one the job started with.",
				"Resume job", MessageBoxButton.OKCancel, MessageBoxImage.Warning);

			if (confirm != MessageBoxResult.OK)
			{
				resumeArmed = true;
				return;
			}

			foreach (string line in pendingRecovery.BuildResumePreamble(resumePoint))
				machine.SendLine(line);

			machine.FileGoto(resumeLine);

			pendingRecovery = null;
			AlertBanner.Visibility = Visibility.Collapsed;

			machine.FileStart();
		}

		/// <summary>
		/// Where the tool would be after the given line, by replaying the parsed toolpath.
		/// </summary>
		private Vector3 PositionAtLine(int line)
		{
			Vector3 position = new Vector3(0, 0, 0);

			if (ToolPath == null)
				return position;

			foreach (Command command in ToolPath.Toolpath)
			{
				Motion motion = command as Motion;

				if (motion == null || motion.LineNumber > line + 1)
					continue;

				position = motion.End;
			}

			return position;
		}

		private double LastCommandedFeed(int line)
		{
			double feed = 0;

			if (ToolPath == null)
				return feed;

			foreach (Command command in ToolPath.Toolpath)
			{
				Motion motion = command as Motion;

				if (motion != null && motion.LineNumber <= line + 1 && motion.Feed > 0)
					feed = motion.Feed;
			}

			return feed;
		}

		private double LastCommandedSpindle(int line)
		{
			double speed = 0;

			if (ToolPath == null)
				return speed;

			foreach (Command command in ToolPath.Toolpath)
			{
				Spindle spindle = command as Spindle;

				if (spindle != null && spindle.LineNumber <= line + 1)
					speed = spindle.Speed;
			}

			return speed;
		}
	}
}
