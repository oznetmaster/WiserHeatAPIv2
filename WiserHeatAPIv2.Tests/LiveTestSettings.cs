// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO;

using Newtonsoft.Json;

namespace WiserHeatAPIv2.Tests;

internal sealed class LiveTestSettings
	{
	public bool Enabled { get; set; }
	public string HubHost { get; set; } = "";
	public string Secret { get; set; } = "";
	public string ControlRoomName { get; set; } = "";
	public int TimeoutSeconds { get; set; } = 60;
	}

internal static class LiveTestSupport
	{
	internal static string SettingsPath (string? directory) => Path.Combine (
		string.IsNullOrWhiteSpace (directory)
			? Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.LocalApplicationData), "WiserHeatAPIv2")
			: directory!, "LiveTestSettings.json");

	internal static LiveTestSettings LoadForRun () => LoadForRun (
		SettingsPath (TestContext.Parameters.Get ("TestDataDirectory", "")),
		TestContext.Parameters.Get ("EnableLiveTests", ""));

	internal static LiveTestSettings LoadForRun (string path, string enableOverride)
		{
		bool? enabled = ParseOverride (enableOverride);
		if (enabled == false)
			Assert.Ignore ("Wiser live tests are disabled for this run.");
		LiveTestSettings? settings = null;
		if (File.Exists (path))
			{
			try
				{
				settings = JsonConvert.DeserializeObject<LiveTestSettings> (File.ReadAllText (path));
				}
			catch (JsonException)
				{
				// Never include credential-bearing input or parser diagnostics in test results.
				throw new InvalidDataException ("LiveTestSettings.json must contain a valid settings object.");
				}
			}
		if (!(enabled ?? settings?.Enabled ?? false))
			Assert.Ignore ("Wiser live tests are disabled. Set enabled=true in private LiveTestSettings.json or supply EnableLiveTests=true.");
		if (settings == null)
			throw new InvalidDataException ("Enabled live tests require LiveTestSettings.json in the private local folder or TestDataDirectory.");
		Validate (settings);
		return settings;
		}

	internal static bool? ParseOverride (string? value)
		{
		if (string.IsNullOrWhiteSpace (value))
			return null;
		if (bool.TryParse (value, out bool enabled))
			return enabled;
		throw new InvalidDataException ("EnableLiveTests must be true or false.");
		}

	internal static void Validate (LiveTestSettings settings)
		{
		if (string.IsNullOrWhiteSpace (settings.HubHost) || string.IsNullOrWhiteSpace (settings.Secret))
			throw new InvalidDataException ("LiveTestSettings.json requires hubHost and secret.");
		if (settings.HubHost.Any (char.IsWhiteSpace) || settings.HubHost.Any (character => character is '/' or '?' or '#' or '@')
			|| !Uri.TryCreate ("http://" + settings.HubHost, UriKind.Absolute, out var uri)
			|| uri.HostNameType == UriHostNameType.Unknown)
			throw new InvalidDataException ("hubHost must be a hostname or IP address, optionally with a port; do not include a URL scheme or path.");
		if (settings.TimeoutSeconds < 1 || settings.TimeoutSeconds > 120)
			throw new InvalidDataException ("timeoutSeconds must be between 1 and 120.");
		}
	}