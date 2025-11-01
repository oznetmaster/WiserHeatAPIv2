using System;
using System.Linq;
using System.Threading;
using System.Windows.Controls;

using WiserHeatApp.Wpf.ViewModels;

namespace WiserHeatApp.Wpf.Views;

public partial class SchedulesPage : Page
	{
	private readonly SchedulesViewModel _vm = new ();
	public SchedulesPage ()
		{
		InitializeComponent ();
		Loaded += async (_, __) =>
		{
			await _vm.LoadAsync (CancellationToken.None);
			SchedulesList.ItemsSource = _vm.Items;
		};
		}
	}
