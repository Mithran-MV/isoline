using System;

namespace Isoline.Machines
{
	/// <summary>
	/// Tracks how far a job has got and how long it has left.
	/// <para>
	/// The estimate comes from the observed line rate rather than from the G-code's own
	/// feed rates, because the real rate depends on the controller's buffer, the serial
	/// link and every override the operator touches while it runs. It is smoothed with an
	/// exponential moving average so the figure does not swing wildly between a rapid and
	/// a slow plunge.
	/// </para>
	/// </summary>
	public class JobProgress
	{
		private DateTime _started;
		private DateTime _lastSample;
		private int _lastPosition;
		private double _linesPerSecond;
		private TimeSpan _accumulated = TimeSpan.Zero;

		/// <summary>Weight given to the newest measurement. Lower is smoother.</summary>
		private const double Smoothing = 0.15;

		public int Total { get; private set; }
		public int Position { get; private set; }
		public bool Running { get; private set; }

		public double Fraction
		{
			get { return Total <= 0 ? 0 : Math.Min(1.0, (double)Position / Total); }
		}

		public TimeSpan Elapsed
		{
			get { return Running ? _accumulated + (DateTime.UtcNow - _started) : _accumulated; }
		}

		/// <summary>Estimated time left, or null while there is not enough data yet.</summary>
		public TimeSpan? Remaining
		{
			get
			{
				if (!Running || _linesPerSecond <= 1e-6 || Total <= 0)
					return null;

				int left = Total - Position;

				if (left <= 0)
					return TimeSpan.Zero;

				double seconds = left / _linesPerSecond;

				// Anything beyond a day means the rate estimate has not settled; showing
				// "3 days" would be worse than showing nothing.
				return seconds > 86400 ? (TimeSpan?)null : TimeSpan.FromSeconds(seconds);
			}
		}

		public void Start(int totalLines, int startPosition = 0)
		{
			Total = totalLines;
			Position = startPosition;
			_lastPosition = startPosition;
			_started = DateTime.UtcNow;
			_lastSample = _started;
			_linesPerSecond = 0;
			_accumulated = TimeSpan.Zero;
			Running = true;
		}

		public void Resume()
		{
			if (Running)
				return;

			_started = DateTime.UtcNow;
			_lastSample = _started;
			Running = true;
		}

		public void Pause()
		{
			if (!Running)
				return;

			_accumulated += DateTime.UtcNow - _started;
			Running = false;
		}

		public void Stop()
		{
			Pause();
			_linesPerSecond = 0;
		}

		/// <summary>Records a new file position and updates the rate estimate.</summary>
		public void Update(int position)
		{
			DateTime now = DateTime.UtcNow;
			double seconds = (now - _lastSample).TotalSeconds;

			Position = position;

			// Sample at most a few times a second: over very short intervals the line count
			// is dominated by whatever happened to be in the controller's buffer.
			if (seconds < 0.5)
				return;

			int advanced = position - _lastPosition;

			if (advanced > 0)
			{
				double rate = advanced / seconds;

				_linesPerSecond = _linesPerSecond <= 1e-6
					? rate
					: _linesPerSecond + Smoothing * (rate - _linesPerSecond);
			}

			_lastPosition = position;
			_lastSample = now;
		}

		public static string Format(TimeSpan? span)
		{
			if (span == null)
				return "--:--";

			TimeSpan value = span.Value;

			return value.TotalHours >= 1
				? string.Format("{0}:{1:00}:{2:00}", (int)value.TotalHours, value.Minutes, value.Seconds)
				: string.Format("{0:00}:{1:00}", value.Minutes, value.Seconds);
		}
	}
}
