// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WiserHeatApiV2
	{
	public class WiserRoomStat : WiserDevice
		{

		public WiserRoomStat (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData)
			 : base (wiserRestController, data, deviceTypeData)
			{
			}

		public WiserBattery Battery => new WiserBattery (_data);

		public int CurrentHumidity => _deviceTypeData.TryGetValue ("MeasuredHumidity", out var humidity) ? Convert.ToInt32 (humidity) : 0;

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current");
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
