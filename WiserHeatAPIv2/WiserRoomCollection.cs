namespace WiserHeatApiV2
	{
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading.Tasks;
	using System;
	using System.Globalization;
	using System.IO;

	public class WiserRoomCollection
		{
		private readonly WiserRestController _wiserRestController;
		private readonly List<WiserRoom> _rooms = new List<WiserRoom> ();

		public WiserRoomCollection (WiserRestController wiserRestController, List<Dictionary<string, object>> roomData,
														  WiserScheduleCollection schedules, WiserDeviceCollection devices)
			{
			_wiserRestController = wiserRestController;
			// Add room objects
			foreach (var room in roomData)
				{
				var schedule = schedules.GetByType (WiserScheduleTypeEnum.Heating)
					  .FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0));
				var roomDevices = devices.GetByRoomId (room.TryGetValue ("id", out var roomId) ? Convert.ToInt32 (roomId) : 0);
				_rooms.Add (new WiserRoom (
					  wiserRestController,
					  room,
					  schedule,
					  roomDevices
				));
				}
			}

		public void Update (List<Dictionary<string, object>> roomData, WiserScheduleCollection schedules, WiserDeviceCollection devices)
			{
			// For simplicity, just rebuild the collection
			// (You can optimize this if needed)
			// This assumes you have a Build method or similar
			// Build(roomData, schedules, devices);

			// Remove rooms that are not in the new data
			var newRoomIds = new HashSet<int> (roomData.Select (r => r.TryGetValue ("id", out var id) ? Convert.ToInt32 (id) : 0));
			_rooms.RemoveAll (room => !newRoomIds.Contains (room.Id));

			// Update existing rooms or add new ones
			foreach (var room in roomData)
				{
				var schedule = schedules.GetByType (WiserScheduleTypeEnum.Heating)
					.FirstOrDefault (s => s.Id == (room.TryGetValue ("ScheduleId", out var id) ? Convert.ToInt32 (id) : 0));
				var idroom = room.TryGetValue ("id", out var roomId) ? Convert.ToInt32 (roomId) : 0;
				var roomDevices = devices.GetByRoomId (idroom);
				var existingRoom = _rooms.FirstOrDefault (r => r.Id == idroom);
				if (existingRoom != null)
					existingRoom.Update (room, schedule, roomDevices);
				else
					{
					_rooms.Add (new WiserRoom (
						_wiserRestController,
						room,
						schedule,
						roomDevices
					));
					}
				}
			}

		public List<WiserRoom> All => _rooms;
		public int Count => _rooms.Count;
		public async Task<bool> AddAsync (string name)
			{
			return await _wiserRestController.SendCommandAsync (RestConstants.WISERROOM, new
				{
				name = name
				}, WiserRestActionEnum.POST).ConfigureAwait (false);
			}
		public WiserRoom GetById (int id)
			{
			return _rooms.FirstOrDefault (room => room.Id == id);
			}
		public WiserRoom GetByName (string name)
			{
			return _rooms.FirstOrDefault (room => room.Name.Equals (name, StringComparison.OrdinalIgnoreCase));
			}
		public WiserRoom GetByScheduleId (int scheduleId)
			{
			return _rooms.FirstOrDefault (room => room.ScheduleId == scheduleId);
			}
		public WiserRoom GetByDeviceId (int deviceId)
			{
			foreach (var room in _rooms)
				{
				foreach (var device in room.Devices)
					{
					if (device.Id == deviceId)
						return room;
					}
				}
			return null;
			}
		}
	}
