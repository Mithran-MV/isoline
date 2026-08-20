using System;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;

namespace Isoline.Util
{
	/// <summary>
	/// Asks GitHub whether a newer release exists. Entirely best-effort: an update check
	/// must never delay start-up or interrupt a running job, so every failure is swallowed.
	/// </summary>
	public static class UpdateCheck
	{
		private const string ReleasesApi = "https://api.github.com/repos/Mithran-MV/isoline/releases/latest";
		private const string ReleasesPage = "https://github.com/Mithran-MV/isoline/releases";

		private static readonly HttpClient Client = CreateClient();

		private static HttpClient CreateClient()
		{
			HttpClient client = new HttpClient() { Timeout = TimeSpan.FromSeconds(10) };

			// GitHub rejects requests without a user agent
			client.DefaultRequestHeaders.Add("User-Agent", "Isoline");
			client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");

			return client;
		}

		public static async void CheckForUpdate()
		{
			try
			{
				string json = await Client.GetStringAsync(ReleasesApi).ConfigureAwait(true);

				using (JsonDocument document = JsonDocument.Parse(json))
				{
					JsonElement tagElement;

					if (!document.RootElement.TryGetProperty("tag_name", out tagElement))
						return;

					string tag = tagElement.GetString();

					if (string.IsNullOrEmpty(tag))
						return;

					Version latest;

					if (!Version.TryParse(tag.TrimStart('v', 'V'), out latest))
						return;

					Version current = System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version;

					if (current == null || current >= latest)
						return;

					JsonElement urlElement;
					string url = document.RootElement.TryGetProperty("html_url", out urlElement)
						? urlElement.GetString()
						: ReleasesPage;

					MessageBoxResult answer = MessageBox.Show(
						$"Isoline {latest} is available (you have {current}).\nOpen the release page?",
						"Update available", MessageBoxButton.YesNo, MessageBoxImage.Information);

					if (answer == MessageBoxResult.Yes)
						OpenInBrowser(url ?? ReleasesPage);
				}
			}
			catch (Exception ex)
			{
				// non-critical by design
				Console.WriteLine("update check failed: " + ex.Message);
			}
		}

		public static void OpenInBrowser(string url)
		{
			try
			{
				// .NET Core onwards will not launch a URL without UseShellExecute
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url)
				{
					UseShellExecute = true,
				});
			}
			catch (Exception ex)
			{
				Console.WriteLine("could not open browser: " + ex.Message);
			}
		}
	}
}
