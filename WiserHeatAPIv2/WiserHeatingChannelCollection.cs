// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2
	{
	using System.Collections.Generic;
	using System.Linq;

	public class WiserHeatingChannelCollection
		{
		private readonly List<WiserHeatingChannel> _heatingChannels = new List<WiserHeatingChannel> ();
		private WiserRoomCollection _rooms;

		public WiserHeatingChannelCollection (List<Dictionary<string, object>> heatingChannelData, WiserRoomCollection rooms)
			{
			_rooms = rooms;
			foreach (var channel in heatingChannelData)
				{
				_heatingChannels.Add (new WiserHeatingChannel (channel));
				}
			}

		public void Update (List<Dictionary<string, object>> heatingChannelData, WiserRoomCollection rooms)
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
		public WiserHeatingChannel GetByRoomName (string roomName)
			{
			var room = _rooms.GetByName (roomName);
			return room != null ? GetByRoomId (room.Id) : null;
			}
		}
	}
