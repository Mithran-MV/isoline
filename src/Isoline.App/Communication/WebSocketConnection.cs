using System;
using System.IO;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace Isoline.Communication
{
	/// <summary>
	/// Presents a WebSocket as a <see cref="Stream"/>, so the sender's worker loop can talk
	/// to FluidNC over WiFi with exactly the same code it uses for a serial port.
	/// <para>
	/// FluidNC's console is a WebSocket carrying the ordinary Grbl line protocol. Frames do
	/// not line up with lines - one frame may hold several lines, or half of one - so
	/// received data is buffered and handed out byte by byte, which is what the caller's
	/// line-oriented reader expects.
	/// </para>
	/// </summary>
	public class WebSocketConnection : Stream
	{
		private readonly ClientWebSocket socket = new ClientWebSocket();
		private readonly Uri uri;
		private readonly CancellationTokenSource cancellation = new CancellationTokenSource();

		private byte[] pending = new byte[0];
		private int pendingOffset;

		public WebSocketConnection(string url)
		{
			uri = new Uri(url);

			// FluidNC negotiates this sub-protocol; without it the handshake is refused.
			socket.Options.AddSubProtocol("arduino");
			socket.Options.KeepAliveInterval = TimeSpan.FromSeconds(20);
		}

		public void Connect(int timeoutMilliseconds = 8000)
		{
			using (CancellationTokenSource timeout = new CancellationTokenSource(timeoutMilliseconds))
			{
				try
				{
					socket.ConnectAsync(uri, timeout.Token).GetAwaiter().GetResult();
				}
				catch (OperationCanceledException)
				{
					throw new TimeoutException($"no response from {uri} within {timeoutMilliseconds} ms");
				}
			}
		}

		public override bool CanRead { get { return true; } }
		public override bool CanWrite { get { return true; } }
		public override bool CanSeek { get { return false; } }

		public override int ReadTimeout { get; set; } = Timeout.Infinite;
		public override int WriteTimeout { get; set; } = Timeout.Infinite;

		public override int Read(byte[] buffer, int offset, int count)
		{
			if (pendingOffset >= pending.Length)
			{
				if (!ReceiveFrame())
					return 0;
			}

			int available = pending.Length - pendingOffset;
			int taken = Math.Min(available, count);

			Array.Copy(pending, pendingOffset, buffer, offset, taken);
			pendingOffset += taken;

			return taken;
		}

		/// <summary>Pulls one complete message into the pending buffer.</summary>
		private bool ReceiveFrame()
		{
			byte[] chunk = new byte[4096];
			MemoryStream message = new MemoryStream();

			while (true)
			{
				WebSocketReceiveResult result;

				try
				{
					result = socket.ReceiveAsync(new ArraySegment<byte>(chunk), cancellation.Token)
						.GetAwaiter().GetResult();
				}
				catch (OperationCanceledException)
				{
					return false;
				}

				if (result.MessageType == WebSocketMessageType.Close)
					return false;

				message.Write(chunk, 0, result.Count);

				if (result.EndOfMessage)
					break;
			}

			pending = message.ToArray();
			pendingOffset = 0;

			return pending.Length > 0;
		}

		public override void Write(byte[] buffer, int offset, int count)
		{
			socket.SendAsync(
				new ArraySegment<byte>(buffer, offset, count),
				WebSocketMessageType.Binary, true, cancellation.Token)
				.GetAwaiter().GetResult();
		}

		public override void Flush()
		{
		}

		public override void Close()
		{
			cancellation.Cancel();

			try
			{
				if (socket.State == WebSocketState.Open)
				{
					socket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "closing", CancellationToken.None)
						.GetAwaiter().GetResult();
				}
			}
			catch
			{
				// the far end going away first is the normal case, not an error
			}

			base.Close();
		}

		protected override void Dispose(bool disposing)
		{
			if (disposing)
			{
				cancellation.Dispose();
				socket.Dispose();
			}

			base.Dispose(disposing);
		}

		public override long Length { get { throw new NotSupportedException(); } }

		public override long Position
		{
			get { throw new NotSupportedException(); }
			set { throw new NotSupportedException(); }
		}

		public override long Seek(long offset, SeekOrigin origin)
		{
			throw new NotSupportedException();
		}

		public override void SetLength(long value)
		{
			throw new NotSupportedException();
		}
	}
}
