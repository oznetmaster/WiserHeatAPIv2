// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Linq;

namespace WiserHeatApiV2
	{
#if HEATACTUATOR
	public class WiserHeatingActuator : WiserDevice
		{
		public WiserHeatingActuator (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (wiserRestController, data, deviceTypeData)
			{
			}

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("OccupiedHeatingSetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : Constants.TEMP_OFF);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp) : Constants.TEMP_OFF, "current");

		public int DeliveredPower => _deviceTypeData.TryGetValue ("CurrentSummationDelivered", out var power) ? Convert.ToInt32 (power) : 0;

		public int InstantaneousPower => _deviceTypeData.TryGetValue ("InstantaneousDemand", out var power) ? Convert.ToInt32 (power) : 0;

		public string OutputType => _deviceTypeData.TryGetValue ("OutputType", out var type) ? type.ToString () : Constants.TEXT_UNKNOWN;
		}

	public class WiserHeatingActuatorCollection
		{
		private readonly List<WiserHeatingActuator> _heatingActuators = new List<WiserHeatingActuator> ();

		public List<WiserHeatingActuator> All => _heatingActuators;

		public int Count => _heatingActuators.Count;

		public WiserHeatingActuator GetById (int id)
			{
			return _heatingActuators.FirstOrDefault (actuator => actuator.Id == id);
			}
		}
#endif
	}
