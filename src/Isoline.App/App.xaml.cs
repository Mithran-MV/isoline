using Isoline.Properties;
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows;

namespace Isoline
{
	/// <summary>
	/// Interaction logic for App.xaml
	/// </summary>
	public partial class App : Application
	{
		// command line args
		public static string[] Args;

		public const int WM_COPYDATA = 0x004A;

		[DllImport("user32", EntryPoint = "SendMessageA")]
		private static extern int SendMessage(IntPtr Hwnd, int wMsg, IntPtr wParam, IntPtr lParam);

		[StructLayout(LayoutKind.Sequential)]
		public struct COPYDATASTRUCT
		{
			public IntPtr dwData;    // Any value the sender chooses.  Perhaps its main window handle?
			public int cbData;       // The count of bytes in the message.
			public IntPtr lpData;    // The address of the message.
		}

		void SendMessage(IntPtr hWnd, byte[] array, int startIndex, int length)
		{
			IntPtr ptr = Marshal.AllocHGlobal(IntPtr.Size * 3 + length);
			Marshal.WriteIntPtr(ptr, 0, IntPtr.Zero);
			Marshal.WriteIntPtr(ptr, IntPtr.Size, (IntPtr)length);
			IntPtr dataPtr = new IntPtr(ptr.ToInt64() + IntPtr.Size * 3);
			Marshal.WriteIntPtr(ptr, IntPtr.Size * 2, dataPtr);
			Marshal.Copy(array, startIndex, dataPtr, length);
			int result = SendMessage(hWnd, WM_COPYDATA, IntPtr.Zero, ptr);
			Marshal.FreeHGlobal(ptr);
		}


		private void Application_Startup(object sender, StartupEventArgs e)
		{
			// check if already running
			Process _currentProcess = Process.GetCurrentProcess();
			Process _other = null;
			foreach (Process p in Process.GetProcessesByName(_currentProcess.ProcessName))
			{
				if (p.Id == _currentProcess.Id)
					continue;
				_other = p;
				break;
			}

			if (_other != null)
			{
				if (e.Args.Length > 0)
				{
					byte[] data = Encoding.Unicode.GetBytes(e.Args[0]);
					SendMessage(_other.MainWindowHandle, data, 0, data.Length);
				}
				else
				{
					MessageBox.Show("Isoline is already running.");
				}
				Shutdown();
			}

			Args = e.Args;

			// upgrade settings after a new version was installed
			if (Settings.Default.SettingsUpdateRequired)
			{
				Settings.Default.Upgrade();
				Settings.Default.SettingsUpdateRequired = false;
				Settings.Default.Save();
			}

			ApplySettingsToCore();

			Theme.ThemeManager.Apply(Theme.ThemeManager.Parse(Settings.Default.Theme));
		}

		/// <summary>
		/// Pushes the user's settings into Isoline.Core.
		/// <para>
		/// Core deliberately knows nothing about the application settings object - that is
		/// what makes it testable - so the application hands it the handful of values it
		/// needs at start-up, and again whenever the user changes one.
		/// </para>
		/// </summary>
		public static void ApplySettingsToCore()
		{
			GCode.GCodeOutputOptions.Current = new GCode.GCodeOutputOptions()
			{
				IncludeProgramEnd = Settings.Default.GCodeIncludeMEnd,
				IncludeSpindle = Settings.Default.GCodeIncludeSpindle,
				IncludeDwell = Settings.Default.GCodeIncludeDwell,
			};

			GCode.GCodeParserOptions.Current = new GCode.GCodeParserOptions()
			{
				IgnoreAdditionalAxes = Settings.Default.IgnoreAdditionalAxes,
			};

			Visuals.ToolpathVisuals.ViewportArcSplit = Settings.Default.ViewportArcSplit;

			// the firmware code tables live next to the executable
			Firmware.GrblCodeTranslator.ResourceDirectory =
				System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Resources");
			Firmware.GrblCodeTranslator.Reload(Settings.Default.FirmwareType);
		}
	}
}
