using System;
using System.Linq;
using System.Windows;

namespace Isoline.Theme
{
	/// <summary>The themes the application ships with.</summary>
	public enum AppTheme
	{
		Dark,
		Light,

		/// <summary>Follow the Windows app-mode setting.</summary>
		System,
	}

	/// <summary>
	/// Swaps the colour token dictionary at runtime.
	/// <para>
	/// Only the tokens are swapped - control templates reference them with DynamicResource,
	/// so nothing else has to be rebuilt and the change is instant.
	/// </para>
	/// </summary>
	public static class ThemeManager
	{
		private const string DarkTokens = "Theme/Tokens.Dark.xaml";
		private const string LightTokens = "Theme/Tokens.Light.xaml";

		public static AppTheme Current { get; private set; } = AppTheme.Dark;

		public static event Action<AppTheme> ThemeChanged;

		public static AppTheme Parse(string name)
		{
			AppTheme theme;

			return Enum.TryParse(name, true, out theme) ? theme : AppTheme.Dark;
		}

		public static void Apply(AppTheme theme)
		{
			Application application = Application.Current;

			if (application == null)
				return;

			AppTheme effective = theme == AppTheme.System
				? (IsSystemInDarkMode() ? AppTheme.Dark : AppTheme.Light)
				: theme;

			Uri source = new Uri(effective == AppTheme.Dark ? DarkTokens : LightTokens, UriKind.Relative);

			ResourceDictionary tokens = new ResourceDictionary() { Source = source };

			var dictionaries = application.Resources.MergedDictionaries;

			// The token dictionary is always merged first so control styles can override it;
			// find whichever palette is loaded and replace it in place.
			ResourceDictionary existing = dictionaries.FirstOrDefault(d =>
				d.Source != null &&
				(d.Source.OriginalString.EndsWith("Tokens.Dark.xaml", StringComparison.OrdinalIgnoreCase) ||
				 d.Source.OriginalString.EndsWith("Tokens.Light.xaml", StringComparison.OrdinalIgnoreCase)));

			if (existing != null)
			{
				int index = dictionaries.IndexOf(existing);
				dictionaries[index] = tokens;
			}
			else
			{
				dictionaries.Insert(0, tokens);
			}

			Current = theme;

			if (ThemeChanged != null)
				ThemeChanged(effective);
		}

		/// <summary>
		/// Reads the Windows "app mode" preference. Returns dark if the value is missing,
		/// which matches what the application looked like before it could switch at all.
		/// </summary>
		public static bool IsSystemInDarkMode()
		{
			try
			{
				object value = Microsoft.Win32.Registry.GetValue(
					@"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
					"AppsUseLightTheme", null);

				return value is int light && light == 0;
			}
			catch
			{
				return true;
			}
		}
	}
}
