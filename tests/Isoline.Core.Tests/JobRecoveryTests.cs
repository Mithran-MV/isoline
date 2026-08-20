using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Isoline.Jobs;
using Isoline.Util;
using Xunit;

namespace Isoline.Tests
{
	public class JobRecoveryTests
	{
		private static readonly string[] Job =
		{
			"G21 G90",
			"G0 Z5",
			"G0 X10 Y10",
			"G1 Z-0.1 F60",
			"G1 X20 Y20 F300",
			"M30",
		};

		[Fact]
		public void RoundTripsThroughDisk()
		{
			string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

			JobRecoveryState state = new JobRecoveryState()
			{
				FilePath = "board.nc",
				FileHash = JobRecoveryState.HashLines(Job),
				TotalLines = Job.Length,
				CompletedLines = 4,
				SpindleSpeed = 12000,
				FeedRate = 300,
				SafeZ = 5,
			};

			try
			{
				state.Save(path);
				JobRecoveryState loaded = JobRecoveryState.Load(path);

				Assert.NotNull(loaded);
				Assert.Equal(4, loaded.CompletedLines);
				Assert.Equal(12000, loaded.SpindleSpeed);
				Assert.True(loaded.MatchesFile(Job));
			}
			finally
			{
				JobRecoveryState.Clear(path);
			}
		}

		[Fact]
		public void RefusesToResumeAgainstAnEditedFile()
		{
			JobRecoveryState state = new JobRecoveryState()
			{
				FileHash = JobRecoveryState.HashLines(Job),
			};

			List<string> edited = Job.ToList();
			edited[4] = "G1 X25 Y25 F300";

			Assert.True(state.MatchesFile(Job));
			Assert.False(state.MatchesFile(edited));
		}

		[Fact]
		public void MissingOrCorruptStateReadsAsNothingToResume()
		{
			string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

			Assert.Null(JobRecoveryState.Load(path));

			File.WriteAllText(path, "{ this is not json");

			try
			{
				Assert.Null(JobRecoveryState.Load(path));
			}
			finally
			{
				JobRecoveryState.Clear(path);
			}
		}

		[Fact]
		public void ResumePreambleRestoresStateBeforeItMoves()
		{
			JobRecoveryState state = new JobRecoveryState()
			{
				SpindleSpeed = 10000,
				FeedRate = 250,
				SafeZ = 5,
				Units = "G21",
				DistanceMode = "G90",
				Plane = "G17",
			};

			List<string> preamble = state.BuildResumePreamble(new Vector3(12.5, -3.25, -0.1));

			// modal groups first: a file left in G91 would read the return move as relative
			Assert.StartsWith("G21 G90 G17", preamble[0]);

			int lift = preamble.FindIndex(l => l.StartsWith("G0 Z", StringComparison.Ordinal));
			int travel = preamble.FindIndex(l => l.Contains("X12.5"));
			int plunge = preamble.FindIndex(l => l.StartsWith("G1 Z", StringComparison.Ordinal));

			Assert.True(lift < travel, "must lift to safe height before travelling in XY");
			Assert.True(travel < plunge, "must arrive above the resume point before plunging");

			// the spindle has to be running, and given time to spin up, before the plunge
			int spindle = preamble.FindIndex(l => l.Contains("M3"));
			Assert.InRange(spindle, travel, plunge);
			Assert.Contains(preamble, l => l.StartsWith("G4 P", StringComparison.Ordinal));

			// and the plunge is a controlled feed, not a rapid
			Assert.Contains("F250", preamble[plunge]);
		}

		[Fact]
		public void PreambleUsesARapidWhenNoFeedRateIsKnown()
		{
			JobRecoveryState state = new JobRecoveryState() { FeedRate = 0, SpindleSpeed = 0 };

			List<string> preamble = state.BuildResumePreamble(new Vector3(1, 2, -1));

			Assert.DoesNotContain(preamble, l => l.Contains("M3"));
			Assert.Contains(preamble, l => l.StartsWith("G0 Z-1", StringComparison.Ordinal));
		}

		[Fact]
		public void ProgressIsReportedAsAFraction()
		{
			JobRecoveryState state = new JobRecoveryState() { TotalLines = 200, CompletedLines = 50 };

			Assert.Equal(0.25, state.Progress, 9);
			Assert.Equal(0, new JobRecoveryState().Progress, 9);   // no divide by zero on an empty job
		}

		[Fact]
		public void SavingIsAtomicSoAnInterruptedWriteKeepsTheOldState()
		{
			// the temp file must not be left behind on a successful save
			string path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");

			try
			{
				new JobRecoveryState() { CompletedLines = 1 }.Save(path);
				new JobRecoveryState() { CompletedLines = 2 }.Save(path);

				Assert.False(File.Exists(path + ".tmp"));
				Assert.Equal(2, JobRecoveryState.Load(path).CompletedLines);
			}
			finally
			{
				JobRecoveryState.Clear(path);
			}
		}
	}
}
