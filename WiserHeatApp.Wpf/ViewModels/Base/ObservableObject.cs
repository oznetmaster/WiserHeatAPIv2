using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WiserHeatApp.Wpf.ViewModels.Base;

public abstract class ObservableObject : INotifyPropertyChanged
	{
	public event PropertyChangedEventHandler? PropertyChanged;
	protected void SetProperty<T> (ref T backing, T value, [CallerMemberName] string? name = null)
		{
		if (!Equals (backing, value))
			{
			backing = value;
			PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
			}
		}
	protected void Raise ([CallerMemberName] string? name = null) =>
	PropertyChanged?.Invoke (this, new PropertyChangedEventArgs (name));
	}