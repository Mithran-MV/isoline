using System;
using System.Collections.Generic;

namespace Isoline.Firmware
{
	/// <summary>
	/// The Grbl-compatible firmwares Isoline knows how to talk to.
	/// </summary>
	public enum FirmwareFlavor
	{
		/// <summary>Classic Grbl 1.1 on an AVR. The baseline every other flavour extends.</summary>
		Grbl = 0,

		/// <summary>grblHAL - Grbl 1.1 protocol on 32 bit MCUs, adds more axes and a larger RX buffer.</summary>
		GrblHal = 1,

		/// <summary>FluidNC - ESP32 firmware, usually reached over WiFi rather than USB.</summary>
		FluidNC = 2,

		/// <summary>uCNC - separate error/alarm code tables.</summary>
		uCNC = 3,
	}

	/// <summary>
	/// Per-firmware quirks that the sender has to account for.
	/// <para>
	/// Upstream assumed classic Grbl everywhere and hard-coded its 127 byte serial buffer.
	/// That throttles grblHAL and FluidNC needlessly (they have much larger buffers) and it
	/// is the reason long jobs of very short segments stutter on 32 bit controllers.
	/// </para>
	/// </summary>
	public class FirmwareProfile
	{
		public FirmwareFlavor Flavor { get; private set; }

		/// <summary>Display name, also the key used to pick the error/alarm code tables.</summary>
		public string Name { get; private set; }

		/// <summary>Size of the controller's serial receive buffer, in bytes.</summary>
		public int BufferSize { get; private set; }

		/// <summary>Firmware understands the <c>$J=</c> jog command and 0x85 jog cancel.</summary>
		public bool SupportsJogging { get; private set; }

		/// <summary>Firmware reports real-time feed and spindle speed in its status line.</summary>
		public bool ReportsRealtimeFeed { get; private set; }

		/// <summary>Firmware is normally reached over the network rather than a serial port.</summary>
		public bool PrefersNetwork { get; private set; }

		/// <summary>Firmware exposes a WebSocket endpoint (FluidNC's telnet-over-websocket).</summary>
		public bool SupportsWebSocket { get; private set; }

		private static readonly Dictionary<FirmwareFlavor, FirmwareProfile> Profiles =
			new Dictionary<FirmwareFlavor, FirmwareProfile>()
			{
				{
					FirmwareFlavor.Grbl, new FirmwareProfile
					{
						Flavor = FirmwareFlavor.Grbl, Name = "Grbl",
						BufferSize = 127, SupportsJogging = true,
						ReportsRealtimeFeed = true, PrefersNetwork = false, SupportsWebSocket = false,
					}
				},
				{
					FirmwareFlavor.GrblHal, new FirmwareProfile
					{
						Flavor = FirmwareFlavor.GrblHal, Name = "grblHAL",
						BufferSize = 1024, SupportsJogging = true,
						ReportsRealtimeFeed = true, PrefersNetwork = false, SupportsWebSocket = false,
					}
				},
				{
					FirmwareFlavor.FluidNC, new FirmwareProfile
					{
						Flavor = FirmwareFlavor.FluidNC, Name = "FluidNC",
						BufferSize = 256, SupportsJogging = true,
						ReportsRealtimeFeed = true, PrefersNetwork = true, SupportsWebSocket = true,
					}
				},
				{
					FirmwareFlavor.uCNC, new FirmwareProfile
					{
						Flavor = FirmwareFlavor.uCNC, Name = "uCNC",
						BufferSize = 127, SupportsJogging = true,
						ReportsRealtimeFeed = true, PrefersNetwork = false, SupportsWebSocket = false,
					}
				},
			};

		public static FirmwareProfile For(FirmwareFlavor flavor)
		{
			FirmwareProfile profile;

			return Profiles.TryGetValue(flavor, out profile)
				? profile
				: Profiles[FirmwareFlavor.Grbl];
		}

		/// <summary>
		/// Resolves a profile from the name stored in the application settings. Unknown
		/// names fall back to classic Grbl, which is the safest assumption (smallest buffer).
		/// </summary>
		public static FirmwareProfile ForName(string name)
		{
			foreach (FirmwareProfile profile in Profiles.Values)
			{
				if (string.Equals(profile.Name, name, StringComparison.OrdinalIgnoreCase))
					return profile;
			}

			return Profiles[FirmwareFlavor.Grbl];
		}

		public static IEnumerable<FirmwareProfile> All { get { return Profiles.Values; } }

		public override string ToString()
		{
			return Name;
		}
	}
}
