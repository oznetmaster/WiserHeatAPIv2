// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	public class WiserHeatingChannel (IDictionary<string, object> data)
		{
		public string DemandOnOffOutput => data.TryGetValue ("DemandOnOffOutput", out var output) ? output.ToString () : Constants.TextUnknown;

		public string HeatingRelayStatus => data.TryGetValue ("HeatingRelayState", out var state) ? state.ToString () : Constants.TextUnknown;

		public int Id => data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public bool IsSmartValvePreventingDemand => data.TryGetValue ("IsSmartValvePreventingDemand", out var preventing) && Convert.ToBoolean (preventing, CultureInfo.InvariantCulture);

		public string Name => data.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

		public int PercentageDemand => data.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand, CultureInfo.InvariantCulture) : 0;

		public List<int> RoomIds => data.TryGetValue ("RoomIds", out var roomIds) && roomIds is List<object> roomIdsList
			 ? [.. roomIdsList.Select (id => Convert.ToInt32 (id, CultureInfo.InvariantCulture))]
			 : new List<int> ();
		}

	public class WiserHeatingChannels
		{
		private WiserRooms _rooms;

		public WiserHeatingChannels (List<Dictionary<string, object>> heatingChannelData, WiserRooms rooms)
			{
			_rooms = rooms;
			foreach (Dictionary<string, object> channel in heatingChannelData)
				{
				All.Add (new WiserHeatingChannel (channel));
				}
			}

		public void Update (List<Dictionary<string, object>> heatingChannelData, WiserRooms rooms)
			{
			_rooms = rooms;
			All.Clear ();
			foreach (Dictionary<string, object> channel in heatingChannelData)
				{
				All.Add (new WiserHeatingChannel (channel));
				}
			}

		public List<WiserHeatingChannel> All { get; } = [];
		public int Count => All.Count;
		public WiserHeatingChannel GetById (int id) => All.FirstOrDefault (channel => channel.Id == id);
		public WiserHeatingChannel GetByRoomId (int id) => All.FirstOrDefault (channel => channel.RoomIds.Contains (id));
		public WiserHeatingChannel? GetByRoomName (string roomName) =>
			_rooms.GetByName (roomName) is WiserRoom room ? GetByRoomId (room.Id) : null;
		}
	}
