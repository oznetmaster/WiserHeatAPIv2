// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	public class WiserHeatingChannel
		{
		private readonly IDictionary<string, object> _data;

		public WiserHeatingChannel (IDictionary<string, object> data)
			{
			_data = data;
			}

		public string DemandOnOffOutput => _data.TryGetValue ("DemandOnOffOutput", out var output) ? output.ToString () : Constants.TextUnknown;

		public string HeatingRelayStatus => _data.TryGetValue ("HeatingRelayState", out var state) ? state.ToString () : Constants.TextUnknown;

		public int Id => _data.TryGetValue ("id", out var id) ? Convert.ToInt32 (id, CultureInfo.InvariantCulture) : 0;

		public bool IsSmartValvePreventingDemand => _data.TryGetValue ("IsSmartValvePreventingDemand", out var preventing) && Convert.ToBoolean (preventing, CultureInfo.InvariantCulture);

		public string Name => _data.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TextUnknown;

		public int PercentageDemand => _data.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand, CultureInfo.InvariantCulture) : 0;

		public List<int> RoomIds => _data.TryGetValue ("RoomIds", out var roomIds) && roomIds is List<object> roomIdsList
			 ? roomIdsList.Select (id => Convert.ToInt32 (id, CultureInfo.InvariantCulture)).ToList ()
			 : new List<int> ();
		}

	public class WiserHeatingChannels
		{
		private readonly List<WiserHeatingChannel> _heatingChannels = new List<WiserHeatingChannel> ();
		private WiserRooms _rooms;

		public WiserHeatingChannels (List<Dictionary<string, object>> heatingChannelData, WiserRooms rooms)
			{
			_rooms = rooms;
			foreach (var channel in heatingChannelData)
				{
				_heatingChannels.Add (new WiserHeatingChannel (channel));
				}
			}

		public void Update (List<Dictionary<string, object>> heatingChannelData, WiserRooms rooms)
			{
			_rooms = rooms;
			_heatingChannels.Clear ();
			foreach (var channel in heatingChannelData)
				{
				_heatingChannels.Add (new WiserHeatingChannel (channel));
				}
			}

		public List<WiserHeatingChannel> All => _heatingChannels;
		public int Count => _heatingChannels.Count;
		public WiserHeatingChannel GetById (int id)
			{
			return _heatingChannels.FirstOrDefault (channel => channel.Id == id);
			}
		public WiserHeatingChannel GetByRoomId (int id)
			{
			return _heatingChannels.FirstOrDefault (channel => channel.RoomIds.Contains (id));
			}
		public WiserHeatingChannel? GetByRoomName (string roomName)
			{
			var room = _rooms.GetByName (roomName);
			return room != null ? GetByRoomId (room.Id) : null;
			}
		}
	}
