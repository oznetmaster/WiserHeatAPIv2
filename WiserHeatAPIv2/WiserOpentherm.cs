// Copyright © 2025 Nivloc Enterprises Ltd.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

// WiserHeatApiV2.cs
using System;
using System.Collections.Generic;

using WiserHeatApiV2;

namespace WiserHeatApiV2
	{
#if OPENTHERM
	public class WiserOpentherm
		{
		internal readonly IDictionary<string, object> _data;
		private readonly string _enabledStatus;

		public WiserOpentherm (IDictionary<string, object> data, string enabledStatus)
			{
			_data = data;
			_enabledStatus = enabledStatus;
			}

		public double ChFlowActiveLowerSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("chFlowActiveLowerSetpoint", out var value) ? value : null, "current");

		public double ChFlowActiveUpperSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("chFlowActiveUpperSetpoint", out var value) ? value : null, "current");

		public bool Ch1FlowEnabled => _data.TryGetValue ("ch1FlowEnable", out var value) && Convert.ToBoolean (value);

		public double Ch1FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ch1FlowSetpoint", out var value) ? value : null, "current");

		public bool Ch2FlowEnabled => _data.TryGetValue ("ch2FlowEnable", out var value) && Convert.ToBoolean (value);

		public double Ch2FlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ch2FlowSetpoint", out var value) ? value : null, "current");

		public string ConnectionStatus => _enabledStatus;

		public bool Enabled => _data.TryGetValue ("Enabled", out var value) && Convert.ToBoolean (value);

		public bool HwEnabled => _data.TryGetValue ("dhwEnable", out var value) && Convert.ToBoolean (value);

		public double HwFlowSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("dhwFlowSetpoint", out var value) ? value : null, "current");

		public string OperatingMode => _data.TryGetValue ("operatingMode", out var value) ? value.ToString () : null;

		public WiserOpenThermOperationalData OperationalData
			{
			get
				{
				if (_data.TryGetValue ("operationalData", out var data) && data is Dictionary<string, object> dataDict)
					return new WiserOpenThermOperationalData (dataDict);
				return new WiserOpenThermOperationalData (new Dictionary<string, object> ());
				}
			}

		public WiserOpenThermBoilerParameters BoilerParameters
			{
			get
				{
				if (_data.TryGetValue ("preDefinedRemoteBoilerParameters", out var data) && data is Dictionary<string, object> dataDict)
					return new WiserOpenThermBoilerParameters (dataDict);
				return new WiserOpenThermBoilerParameters (new Dictionary<string, object> ());
				}
			}

		public double RoomSetpoint => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("roomSetpoint", out var value) ? value : null, "current");

		public double RoomTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("roomTemperature", out var value) ? value : null, "current");

		public int? TrackedRoomId => _data.TryGetValue ("TrackedRoomId", out var value) ? (int?)Convert.ToInt32 (value) : null;
		}

	public class WiserOpenThermBoilerParameters
		{
		private readonly Dictionary<string, object> _data;

		public WiserOpenThermBoilerParameters (Dictionary<string, object> data)
			{
			_data = data;
			}

		public bool? HwSetpointTransferEnable => _data.TryGetValue ("dhwSetpointTransferEnable", out var value) ? (bool?)Convert.ToBoolean (value) : null;

		public bool? ChSetpointTransferEnable => _data.TryGetValue ("maxChSetpointTransferEnable", out var value) ? (bool?)Convert.ToBoolean (value) : null;

		public bool? HwSetpointReadWrite => _data.TryGetValue ("dhwSetpointReadWrite", out var value) ? (bool?)Convert.ToBoolean (value) : null;

		public bool? ChSetpointReadWrite => _data.TryGetValue ("maxChSetpointReadWrite", out var value) ? (bool?)Convert.ToBoolean (value) : null;
		}

	public class WiserOpenThermOperationalData
		{
		private readonly Dictionary<string, object> _data;

		public WiserOpenThermOperationalData (Dictionary<string, object> data)
			{
			_data = data;
			}

		public double ChPressureBar => _data.TryGetValue ("ChPressureBar", out var value) ? Convert.ToDouble (value) / 10 : 0;

		public double ChFlowTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("Ch1FlowTemperature", out var value) ? value : (int?)null, "current");

		public double ChReturnTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("ChReturnTemperature", out var value) ? value : (int?)null, "current");

		public double HwTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _data.TryGetValue ("Dhw1Temperature", out var value) ? value : (int?)null, "current");

		public int? RelativeModulationLevel => _data.TryGetValue ("RelativeModulationLevel", out var value) ? (int?)Convert.ToInt32 (value) : null;

		public int? SlaveStatus => _data.TryGetValue ("SlaveStatus", out var value) ? (int?)Convert.ToInt32 (value) : null;
		}
#endif
	}

// -----

