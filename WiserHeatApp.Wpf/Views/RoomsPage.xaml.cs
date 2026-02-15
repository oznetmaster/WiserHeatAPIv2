using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

using WiserHeatApp.Wpf.Services;

using WiserHeatApp.Wpf.ViewModels;

namespace WiserHeatApp.Wpf.Views;

public partial class RoomsPage : Page
	{
	private readonly RoomsViewModel _vm = new ();
	private readonly DispatcherTimer _refreshTimer;
	private bool _isLoaded;
	public RoomsPage ()
		{
		InitializeComponent ();
		DataContext = _vm;

		_refreshTimer = new DispatcherTimer
			{
			Interval = AppState.Current.RefreshInterval
			};
		_refreshTimer.Tick += async (_, __) => await RefreshNowAsync ();
		Loaded += async (_, __) => await RefreshNowAsync ();
		Loaded += (_, __) => AppState.Current.RefreshIntervalChanged += OnRefreshIntervalChanged;
		Unloaded += (_, __) =>
			{
			AppState.Current.RefreshIntervalChanged -= OnRefreshIntervalChanged;
			_refreshTimer.Stop ();
			};
		}

	private void OnRefreshIntervalChanged (object? sender, EventArgs e)
		{
		_refreshTimer.Interval = AppState.Current.RefreshInterval;
		}

	private async Task RefreshNowAsync ()
		{
		try
			{
			if (!_isLoaded)
				{
				await _vm.LoadAsync (CancellationToken.None);
				_isLoaded = true;
				}

			await _vm.RefreshAsync (CancellationToken.None);
			if (!_refreshTimer.IsEnabled)
				{
				_refreshTimer.Start ();
				}
			}
		catch (Exception ex)
			{
			_ = MessageBox.Show (ex.Message);
			}
		}

	private async void IncTemp (object sender, RoutedEventArgs e)
		{
		if (sender is Button { DataContext: RoomItemViewModel r })
			{
			await r.SetTempAsync (Math.Min (r.TargetTemp +0.5,30)).ConfigureAwait (false);
			}
		}
	private async void DecTemp (object sender, RoutedEventArgs e)
		{
		if (sender is Button { DataContext: RoomItemViewModel r })
			{
			await r.SetTempAsync (Math.Max (r.TargetTemp -0.5,5)).ConfigureAwait (false);
			}
		}
	private async void CancelOverride (object sender, RoutedEventArgs e)
		{
		if (sender is Button { DataContext: RoomItemViewModel r })
			{
			await r.CancelOverrideAsync ().ConfigureAwait (false);
			}
		}
	private async void CancelBoost (object sender, RoutedEventArgs e)
		{
		if (sender is Button { DataContext: RoomItemViewModel r })
			{
			await r.CancelBoostAsync ().ConfigureAwait (false);
			}
		}
	private async void Boost (object sender, RoutedEventArgs e)
		{
		if (sender is Button { DataContext: RoomItemViewModel r })
			{
			await r.BoostAsync ().ConfigureAwait (false);
			}
		}
	private async void ToggleUseSchedule (object sender, RoutedEventArgs e)
		{
		if (sender is CheckBox { DataContext: RoomItemViewModel r })
			{
			await r.SetUseScheduleAsync (r.UseSchedule).ConfigureAwait (false);
			}
		}

	private async void RefreshNow (object sender, RoutedEventArgs e)
		{
		await RefreshNowAsync ();
		}
	}
