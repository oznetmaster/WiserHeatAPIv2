using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

using WiserHeatApp.Wpf.ViewModels;

namespace WiserHeatApp.Wpf.Views;

public partial class RoomsPage : Page
	{
	private readonly RoomsViewModel _vm = new ();
	public RoomsPage ()
		{
		InitializeComponent ();
		DataContext = _vm;
		Loaded += async (_, __) =>
		{
			try
				{
				await _vm.LoadAsync (CancellationToken.None);
				}
			catch (Exception ex)
				{
				_ = MessageBox.Show (ex.Message);
				}
		};
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
	}
