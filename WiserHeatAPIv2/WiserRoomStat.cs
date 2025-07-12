// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserRoomStat : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly IDictionary<string, object> _deviceTypeData;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserRoomStat (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);
			}

		private  Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false, CancellationToken cancellationToken = default)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERROOMSTAT, Id);

			return _wiserRestController.SendCommandAsync (url, cmd, cancellationToken: cancellationToken);
			}

		public WiserBattery Battery => new WiserBattery (_data);

		public int CurrentHumidity => _deviceTypeData.TryGetValue ("MeasuredHumidity", out var humidity) ? Convert.ToInt32 (humidity) : 0;

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current");

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value, CancellationToken cancellationToken = default)
			{
			if (await SendCommandAsync (new
				{
				DeviceLockEnabled = value
				}, true, cancellationToken).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value, CancellationToken cancellationToken = default)
			{
			if (await SendCommandAsync (new
				{
				Identify = value
				}, true, cancellationToken).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;
		}

	public class WiserRoomStatCollection
		{
		private readonly List<WiserRoomStat> _roomStats = new List<WiserRoomStat> ();

		public List<WiserRoomStat> All => _roomStats;

		public int Count => _roomStats.Count;

		public WiserRoomStat GetById (int id)
			{
			return _roomStats.FirstOrDefault (stat => stat.Id == id);
			}
		}
	}
