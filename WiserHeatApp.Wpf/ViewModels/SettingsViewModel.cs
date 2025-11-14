using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using WiserHeatApiV2;

using WiserHeatApp.Wpf.ViewModels.Base;

namespace WiserHeatApp.Wpf.ViewModels;

public class SettingsViewModel : ObservableObject
	{
	public string HubIp
		{
		get; set => SetProperty (ref field, value);
		} = string.Empty;
	public string Secret
		{
		get; set => SetProperty (ref field, value);
		} = string.Empty;
	public bool IsBusy
		{
		get; set => SetProperty (ref field, value);
		}

	public ObservableCollection<WiserDiscoveredHub> Discovered { get; } = [];
	public WiserAPI? Api
		{
		get; private set => SetProperty (ref field, value);
		}

	public async Task DiscoverAsync (CancellationToken ct = default)
		{
		IsBusy = true;
		Discovered.Clear ();
		try
			{
			List<WiserDiscoveredHub> hubs = await WiserDiscovery.DiscoverHubAsync (30, 5, ct).ConfigureAwait (false);
			foreach (WiserDiscoveredHub hub in hubs)
				{
				App.Current.Dispatcher.Invoke (() => Discovered.Add (hub));
				}
			}
		finally
			{
			IsBusy = false;
			}
		}

	public async Task<bool> ConnectAsync (CancellationToken ct = default)
		{
		if (string.IsNullOrWhiteSpace (HubIp) || string.IsNullOrWhiteSpace (Secret))
			return false;
		Api = new WiserAPI (HubIp, Secret);
		await Api.InitializeAsync (ct).ConfigureAwait (false);
		return true;
		}
	}