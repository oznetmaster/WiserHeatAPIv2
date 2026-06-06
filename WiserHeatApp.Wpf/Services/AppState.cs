using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

using WiserHeatApiV2;

namespace WiserHeatApp.Wpf.Services;

public class AppState
	{
	public static AppState Current { get; } = new ();
	private const string SETTINGS_FILE_NAME = "settings.json";

	private AppState ()
		{
		}

	public WiserAPI? Api
		{
		get; private set;
		}
	public bool IsConnected => Api != null;
	private TimeSpan _refreshInterval = TimeSpan.FromMinutes (1);
	public TimeSpan RefreshInterval
		{
		get => _refreshInterval;
		set
			{
			if (value <= TimeSpan.Zero || value == _refreshInterval)
				return;
			_refreshInterval = value;
			SaveSettings ();
			RefreshIntervalChanged?.Invoke (this, EventArgs.Empty);
			}
		}

	public event EventHandler? RefreshIntervalChanged;

	public void LoadSettings ()
		{
		try
			{
			var path = GetSettingsPath ();
			if (!File.Exists (path))
				return;
			var json = File.ReadAllText (path);
			AppSettings? settings = JsonSerializer.Deserialize<AppSettings> (json);
			if (settings?.RefreshIntervalSeconds > 0)
				{
				_refreshInterval = TimeSpan.FromSeconds (settings.RefreshIntervalSeconds);
				}
			else if (settings?.RefreshIntervalMinutes > 0)
				{
				_refreshInterval = TimeSpan.FromMinutes (settings.RefreshIntervalMinutes);
				}
			}
		catch { }
		}

	public void SaveSettings ()
		{
		try
			{
			var path = GetSettingsPath ();
			Directory.CreateDirectory (Path.GetDirectoryName (path)!);
			var settings = new AppSettings
				{
				RefreshIntervalSeconds = RefreshInterval.TotalSeconds
				};
			var json = JsonSerializer.Serialize (settings, new JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText (path, json);
			}
		catch { }
		}

	private static string GetSettingsPath ()
		{
		var folder = Path.Combine (Environment.GetFolderPath (Environment.SpecialFolder.ApplicationData), "WiserHeatApp");
		return Path.Combine (folder, SETTINGS_FILE_NAME);
		}

	private sealed class AppSettings
		{
		public double RefreshIntervalSeconds { get; set; } = 60;
		public double RefreshIntervalMinutes { get; set; }
		}

	private CancellationTokenSource? _pollCts;

	public async Task<bool> ConnectAsync (string host, string secret, CancellationToken ct = default)
		{
		Api = new WiserAPI (host, secret);
		await Api.InitializeAsync (ct).ConfigureAwait (false);
		return true;
		}

	public async Task<bool> TryAutoConnectAsync (CancellationToken ct = default)
		{
		if (IsConnected)
			return true;
		if (!TryReadParams (out var host, out var secret))
			return false;
		try
			{
			var ok = await ConnectAsync (host, secret, ct).ConfigureAwait (false);
			if (ok)
				{
				StartPolling (TimeSpan.FromSeconds (10));
				return true;
				}
			}
		catch { }

		return false;
		}

	public void StartPolling (TimeSpan interval)
		{
		StopPolling ();
		if (Api == null)
			return;
		_pollCts = new CancellationTokenSource ();
		_ = Task.Run (async () =>
		{
			while (!_pollCts.IsCancellationRequested)
				{
				try
					{
					_ = await Api!.ReadHubDataAsync (_pollCts.Token).ConfigureAwait (false);
					}
				catch { /* ignore poll errors */ }

				await Task.Delay (interval, _pollCts.Token).ConfigureAwait (false);
				}
		});
		}

	public void StopPolling ()
		{
		try
			{
			_pollCts?.Cancel ();
			}
		catch { }

		_pollCts = null;
		}

	private static bool TryReadParams (out string host, out string secret)
		{
		host = string.Empty;
		secret = string.Empty;
		try
			{
			var path = Path.Combine (AppContext.BaseDirectory, "wiserkeys.params");
			if (!File.Exists (path))
				return false;
			foreach (var raw in File.ReadAllLines (path))
				{
				var line = raw?.Trim ();
				if (string.IsNullOrEmpty (line) || line.StartsWith ('#'))
					continue;
				var idx = line.IndexOf ('=');
				if (idx <= 0)
					continue;
				var name = line[..idx].Trim ();
				var value = line[(idx + 1)..].Trim ();
				if (name.Equals ("wiserhubip", StringComparison.OrdinalIgnoreCase))
					host = value;
				else if (name.Equals ("wiserkey", StringComparison.OrdinalIgnoreCase))
					secret = value;
				}
			}
		catch
			{
			return false;
			}

		return !string.IsNullOrWhiteSpace (host) && !string.IsNullOrWhiteSpace (secret) && !host.Equals ("discover", StringComparison.OrdinalIgnoreCase);
		}
	}