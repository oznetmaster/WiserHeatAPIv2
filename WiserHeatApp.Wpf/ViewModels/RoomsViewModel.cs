using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using WiserHeatApiV2;

using WiserHeatApp.Wpf.Services;
using WiserHeatApp.Wpf.ViewModels.Base;

namespace WiserHeatApp.Wpf.ViewModels;

public class RoomItemViewModel (int id) : ObservableObject
	{
	public int Id
		{
		get;
		} = id;
	public string Name
		{
		get; set => SetProperty (ref field, value);
		} = string.Empty;
	public double CurrentTemp
		{
		get; set => SetProperty (ref field, value);
		}
	public double TargetTemp
		{
		get; set => SetProperty (ref field, value);
		}
	public bool IsHeating
		{
		get; set => SetProperty (ref field, value);
		}
	public bool HasBoost
		{
		get; set => SetProperty (ref field, value);
		}
	public bool HasOverride
		{
		get; set => SetProperty (ref field, value);
		}
	public bool UseSchedule
		{
		get; set => SetProperty (ref field, value);
		}

	// Defaults for quick actions
	public double BoostIncrementC { get; set; } = 1.0;
	public int BoostMinutes { get; set; } = 30;
	public int OverrideMinutes { get; set; } = 120;

	public Task RefreshAsync ()
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null)
			return Task.CompletedTask;
		WiserRoom? r = api.Rooms.GetById (Id);
		if (r == null)
			return Task.CompletedTask;
		Name = r.Name;
		CurrentTemp = r.CurrentTemperature;
		TargetTemp = r.CurrentTargetTemperature;
		IsHeating = r.IsHeating;
		HasBoost = r.IsBoost;
		HasOverride = r.IsOverride;
		// Mode: treat "Auto" as schedule, anything else as manual
		UseSchedule = string.Equals (r.Mode, "Auto", System.StringComparison.OrdinalIgnoreCase);
		return Task.CompletedTask;
		}

	public async Task SetUseScheduleAsync (bool value)
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null)
			return;
		WiserRoom? r = api.Rooms.GetById (Id);
		if (r == null) 
			return;
		var mode = value ? "Auto" : "Manual";
		if (!string.Equals (r.Mode, mode, System.StringComparison.OrdinalIgnoreCase))
			{
			_ = await r.SetModeAsync (mode).ConfigureAwait (false);
			_ = await api.ReadHubDataAsync ().ConfigureAwait (false);
			await RefreshAsync ().ConfigureAwait (false);
			}
		}

	public async Task SetTempAsync (double temp)
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null)
			return;
		WiserRoom? r = api.Rooms.GetById (Id);
		if (r == null)
			return;
		if (UseSchedule)
			{
			// override for current schedule slot
			_ = await r.SetTargetTemperatureForDurationOfScheduleAsync (temp).ConfigureAwait (false);
			}
		else
			{
			// change actual manual target temperature
			_ = await r.SetManualTemperatureAsync (temp).ConfigureAwait (false);
			}

		_ = await api.ReadHubDataAsync ().ConfigureAwait (false);
		await RefreshAsync ().ConfigureAwait (false);
		}
	public async Task CancelOverrideAsync ()
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null)
			return;
		WiserRoom? r = api.Rooms.GetById (Id);
		if (r == null)
			return;
		_ = await r.CancelOverridesAsync ().ConfigureAwait (false);
		_ = await api.ReadHubDataAsync ().ConfigureAwait (false);
		await RefreshAsync ().ConfigureAwait (false);
		}
	public async Task CancelBoostAsync ()
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null)
			return;
		WiserRoom? r = api.Rooms.GetById (Id);
		if (r == null)
			return;
		_ = await r.CancelBoostAsync ().ConfigureAwait (false);
		_ = await api.ReadHubDataAsync ().ConfigureAwait (false);
		await RefreshAsync ().ConfigureAwait (false);
		}
	public async Task BoostAsync ()
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null)
			return;
		WiserRoom? r = api.Rooms.GetById (Id);
		if (r == null)
			return;
		_ = await r.BoostAsync (BoostIncrementC, BoostMinutes).ConfigureAwait (false);
		_ = await api.ReadHubDataAsync ().ConfigureAwait (false);
		await RefreshAsync ().ConfigureAwait (false);
		}
	}

public class RoomsViewModel : ObservableObject
	{
	public ObservableCollection<RoomItemViewModel> Rooms { get; } = [];
	public bool IsBusy
		{
		get; set => SetProperty (ref field, value);
		}

	public async Task LoadAsync (CancellationToken ct = default)
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null)
			{
			Rooms.Clear ();
			return;
			}

		IsBusy = true;
		try
			{
			Rooms.Clear ();
			foreach (WiserRoom r in api.Rooms.All)
				{
				var vm = new RoomItemViewModel (r.Id);
				await vm.RefreshAsync ().ConfigureAwait (false);
				Application.Current.Dispatcher.Invoke (() => Rooms.Add (vm));
				}
			}
		finally
			{
			IsBusy = false;
			}
		}
	}