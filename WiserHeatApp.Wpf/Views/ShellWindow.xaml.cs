using System.Windows;
using System.Windows.Controls;

using WiserHeatApp.Wpf.Services;

namespace WiserHeatApp.Wpf.Views;

public partial class ShellWindow : Window
	{
	public ShellWindow ()
		{
		InitializeComponent ();

		// Restore previous window placement
		WindowPlacement.Restore(this);

		HomeBtn.Click += (_, __) => MainFrame.Navigate (new SettingsPage ());
		RoomsBtn.Click += (_, __) => MainFrame.Navigate (new RoomsPage ());
		SchedulesBtn.Click += (_, __) => MainFrame.Navigate (new SchedulesPage ());
		SettingsBtn.Click += (_, __) => MainFrame.Navigate (new SettingsPage ());

		// If already connected (prefill), go to Rooms by default
		Loaded += (_, __) =>
			_ = AppState.Current.IsConnected ? MainFrame.Navigate (new RoomsPage ()) : MainFrame.Navigate (new SettingsPage ());

		// Save bounds when closing
		Closing += (_, __) => WindowPlacement.Save(this);
		}
	}
