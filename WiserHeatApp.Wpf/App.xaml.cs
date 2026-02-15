using System.Windows;

using WiserHeatApp.Wpf.Services;

namespace WiserHeatApp.Wpf;

public partial class App : Application
	{
	protected override void OnStartup (StartupEventArgs e)
		{
		base.OnStartup (e);
		AppState.Current.LoadSettings ();
		}
	}
