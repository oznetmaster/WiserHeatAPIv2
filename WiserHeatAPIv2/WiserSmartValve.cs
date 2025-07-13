// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WiserHeatApiV2
	{
	public class WiserSmartValve : WiserDevice
		{

		public WiserSmartValve (WiserRestController wiserRestController, IDictionary<string, object> data, IDictionary<string, object> deviceTypeData)
			 : base (wiserRestController, data, deviceTypeData)
			{
			}

		public WiserBattery Battery => new WiserBattery (_data);

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? setPoint : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? temp : 0, "current");

		public string MountingOrientation => _deviceTypeData.TryGetValue ("MountingOrientation", out var orientation) ? orientation.ToString () : null;

		public int PercentageDemand => _deviceTypeData.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand) : 0;

		}

	public class WiserSmartValveCollection
		{
		private readonly List<WiserSmartValve> _smartValves = new List<WiserSmartValve> ();

		public List<WiserSmartValve> All => _smartValves;

		public int Count => _smartValves.Count;

		public WiserSmartValve GetById (int id)
			{
			return _smartValves.FirstOrDefault (valve => valve.Id == id);
			}
		}
	}
