using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Isoline.GCode;
using Isoline.Util;

namespace Isoline.Jobs
{
	/// <summary>
	/// A snapshot of a running job, written to disk as it progresses so that a job
	/// interrupted by an alarm, a lost USB connection or a power cut can be picked up again
	/// instead of being restarted from line 1.
	/// <para>
	/// Restarting a half-finished isolation job is not just slow - the second pass re-cuts
	/// air over the finished area and any tiny difference in work offset shows up as a
	/// doubled trace edge. Resuming is the difference between losing a minute and losing
	/// the board.
	/// </para>
	/// </summary>
	public class JobRecoveryState
	{
		/// <summary>Full path of the G-code file that was running.</summary>
		public string FilePath { get; set; }

		/// <summary>SHA-256 of the file contents, so a resume against an edited file is refused.</summary>
		public string FileHash { get; set; }

		/// <summary>Number of lines in the job.</summary>
		public int TotalLines { get; set; }

		/// <summary>Index (0 based) of the last line the controller acknowledged with "ok".</summary>
		public int CompletedLines { get; set; }

		/// <summary>Work coordinate offset in effect when the job was interrupted.</summary>
		public double WorkOffsetX { get; set; }
		public double WorkOffsetY { get; set; }
		public double WorkOffsetZ { get; set; }

		/// <summary>Modal state to restore before resuming.</summary>
		public string DistanceMode { get; set; } = "G90";
		public string Units { get; set; } = "G21";
		public string Plane { get; set; } = "G17";

		/// <summary>Last commanded spindle speed, 0 when the spindle was off.</summary>
		public double SpindleSpeed { get; set; }

		/// <summary>Last commanded feed rate; used for the plunge back into the cut.</summary>
		public double FeedRate { get; set; }

		/// <summary>Height to travel at while returning to the resume point.</summary>
		public double SafeZ { get; set; } = 5;

		public DateTime SavedUtc { get; set; } = DateTime.UtcNow;

		[JsonIgnore]
		public double Progress
		{
			get { return TotalLines == 0 ? 0 : (double)CompletedLines / TotalLines; }
		}

		private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions()
		{
			WriteIndented = true,
		};

		public void Save(string path)
		{
			SavedUtc = DateTime.UtcNow;

			string directory = Path.GetDirectoryName(path);

			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			// Write to a temporary file and move it into place: a power cut halfway through
			// writing the recovery file must not destroy the previous, still-valid one.
			string temp = path + ".tmp";
			File.WriteAllText(temp, JsonSerializer.Serialize(this, SerializerOptions));

			if (File.Exists(path))
				File.Delete(path);

			File.Move(temp, path);
		}

		public static JobRecoveryState Load(string path)
		{
			if (!File.Exists(path))
				return null;

			try
			{
				return JsonSerializer.Deserialize<JobRecoveryState>(File.ReadAllText(path));
			}
			catch (JsonException)
			{
				return null;    // corrupt state file: treat it as "no job to resume"
			}
		}

		public static void Clear(string path)
		{
			if (File.Exists(path))
				File.Delete(path);
		}

		public static string HashFile(string path)
		{
			using (SHA256 sha = SHA256.Create())
			using (FileStream stream = File.OpenRead(path))
				return Convert.ToHexString(sha.ComputeHash(stream));
		}

		public static string HashLines(IEnumerable<string> lines)
		{
			using (SHA256 sha = SHA256.Create())
				return Convert.ToHexString(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", lines))));
		}

		/// <summary>
		/// True when this state still describes the file it was recorded against.
		/// </summary>
		public bool MatchesFile(IEnumerable<string> lines)
		{
			return FileHash != null && FileHash == HashLines(lines);
		}

		/// <summary>
		/// Builds the preamble that re-establishes machine state before streaming resumes.
		/// <para>
		/// The order matters and is deliberately conservative: restore the modal groups
		/// first (a file that was running in G91 would otherwise interpret the return move
		/// as relative), lift to a safe height, only then travel in XY, start the spindle
		/// and finally plunge. Nothing here moves in more than one axis at a time while the
		/// tool is anywhere near the work.
		/// </para>
		/// </summary>
		/// <param name="resumePoint">Where the interrupted line was going to leave the tool.</param>
		public List<string> BuildResumePreamble(Vector3 resumePoint)
		{
			CultureInfo ci = CultureInfo.InvariantCulture;
			List<string> lines = new List<string>();

			lines.Add(string.Format(ci, "{0} {1} {2} G91.1", Units, DistanceMode, Plane));
			lines.Add(string.Format(ci, "G0 Z{0:0.###}", SafeZ));
			lines.Add(string.Format(ci, "G0 X{0:0.###} Y{1:0.###}", resumePoint.X, resumePoint.Y));

			if (SpindleSpeed > 0)
			{
				lines.Add(string.Format(ci, "S{0:0.###} M3", SpindleSpeed));
				lines.Add("G4 P2");     // let the spindle come up to speed before plunging
			}

			if (FeedRate > 0)
				lines.Add(string.Format(ci, "G1 Z{0:0.###} F{1:0.###}", resumePoint.Z, FeedRate));
			else
				lines.Add(string.Format(ci, "G0 Z{0:0.###}", resumePoint.Z));

			return lines;
		}
	}
}
