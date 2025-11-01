using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Controls;

using WiserHeatApp.Wpf.Services;
using WiserHeatApp.Wpf.ViewModels;

namespace WiserHeatApp.Wpf.Views;

public partial class SettingsPage : Page
	{
	private readonly SettingsViewModel _vm = new ();
	public SettingsPage ()
		{
		InitializeComponent ();
		DataContext = _vm;

		// Prefill from wiserkeys.params if present in output directory
		TryPrefillFromParams ();

		DiscoverBtn.Click += async (_, __) =>
		{
			DiscoverBtn.IsEnabled = false;
			try
				{
				await _vm.DiscoverAsync (CancellationToken.None);
				HubsList.ItemsSource = _vm.Discovered;
				}
			finally
				{
				DiscoverBtn.IsEnabled = true;
				}
		};
		HubsList.SelectionChanged += (_, __) =>
		{
			dynamic? item = HubsList.SelectedItem;
			if (item != null)
				IpBox.Text = item.IpAddress.ToString ();
		};
		ConnectBtn.Click += async (_, __) =>
		{
			_vm.HubIp = IpBox.Text;
			_vm.Secret = SecretBox.Password;
			try
				{
				// Establish connection via AppState so other pages can access Api
				var ok = await AppState.Current.ConnectAsync (_vm.HubIp, _vm.Secret, CancellationToken.None);
				if (ok)
					{
					AppState.Current.StartPolling (System.TimeSpan.FromSeconds (10));
					_ = MessageBox.Show ("Connected");
					}
				else
					{
					_ = MessageBox.Show ("Failed to connect");
					}
				}
			catch (System.Exception ex)
				{
				_ = MessageBox.Show ($"Error: {ex.Message}");
				}
		};
		}

	private void TryPrefillFromParams ()
		{
		try
			{
			var path = Path.Combine (AppContext.BaseDirectory, "wiserkeys.params");
			if (!File.Exists (path))
				return;
			string? ip = null;
			string? key = null;
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
					ip = value;
				else if (name.Equals ("wiserkey", StringComparison.OrdinalIgnoreCase))
					key = value;
				}

			if (!string.IsNullOrWhiteSpace (key))
				{
				SecretBox.Password = key!;
				_vm.Secret = key!;
				}

			if (!string.IsNullOrWhiteSpace (ip) && !ip!.Equals ("discover", StringComparison.OrdinalIgnoreCase))
				{
				IpBox.Text = ip!;
				_vm.HubIp = ip!;
				}
			}
		catch { /* ignore prefill errors */ }
		}
	}
