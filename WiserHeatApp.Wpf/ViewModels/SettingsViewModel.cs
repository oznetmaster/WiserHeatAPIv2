using System.Collections.ObjectModel;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

using WiserHeatApiV2;

using WiserHeatApp.Wpf.ViewModels.Base;

namespace WiserHeatApp.Wpf.ViewModels;

public class SettingsViewModel : ObservableObject
	{
	private string _hubIp = string.Empty;
	public string HubIp
		{
		get => _hubIp; set => SetProperty (ref _hubIp, value);
		}

	private string _secret = string.Empty;
	public string Secret
		{
		get => _secret; set => SetProperty (ref _secret, value);
		}

	private bool _isBusy;
	public bool IsBusy
		{
		get => _isBusy; set => SetProperty (ref _isBusy, value);
		}

	public ObservableCollection<WiserDiscoveredHub> Discovered { get; } = [];

	private WiserAPI? _api;
	public WiserAPI? Api
		{
		get => _api; private set => SetProperty (ref _api, value);
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