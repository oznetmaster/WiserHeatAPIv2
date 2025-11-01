using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using WiserHeatApiV2;

using WiserHeatApp.Wpf.Services;
using WiserHeatApp.Wpf.ViewModels.Base;

namespace WiserHeatApp.Wpf.ViewModels;

public class ScheduleItemViewModel : ObservableObject
	{
	public string RoomName { get; set; } = string.Empty;
	public string ScheduleName { get; set; } = string.Empty;
	public string ScheduleType { get; set; } = string.Empty;
	public string NextSummary { get; set; } = string.Empty;
	}

public class SchedulesViewModel : ObservableObject
	{
	public ObservableCollection<ScheduleItemViewModel> Items { get; } = [];
	public Task LoadAsync (CancellationToken ct = default)
		{
		WiserAPI? api = AppState.Current.Api;
		if (api?.Rooms == null || api.Schedules == null)
			return Task.CompletedTask;
		Items.Clear ();
		foreach (WiserRoom room in api.Rooms.All)
			{
			var item = new ScheduleItemViewModel { RoomName = room.Name };
			WiserHeatingSchedule? sched = api.Schedules.GetByRoomId (room.Id);
			if (sched != null)
				{
				item.ScheduleName = sched.Name ?? string.Empty;
				item.ScheduleType = sched.ScheduleType;
				WiserScheduleNext? next = sched.Next;
				if (next != null)
					{
					item.NextSummary = $"{next.DateTime} {next.Day} {next.Time} -> {next.Setting}";
					}
				}

			Items.Add (item);
			}

		return Task.CompletedTask;
		}
	}