using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace WiserHeatApiV2
	{
	public class WiserSmartValve : WiserDevice
		{
		private readonly WiserRestController _wiserRestController;
		private readonly Dictionary<string, object> _deviceTypeData;
		private bool _deviceLockEnabled;
		private bool _identifyActive;

		public WiserSmartValve (WiserRestController wiserRestController, Dictionary<string, object> data, Dictionary<string, object> deviceTypeData)
			 : base (data)
			{
			_wiserRestController = wiserRestController;
			_deviceTypeData = deviceTypeData;
			_deviceLockEnabled = data.TryGetValue ("DeviceLockEnabled", out var lockEnabled) && Convert.ToBoolean (lockEnabled);
			_identifyActive = data.TryGetValue ("IdentifyActive", out var identify) && Convert.ToBoolean (identify);
			}

		private async Task<bool> SendCommandAsync (object cmd, bool deviceLevel = false)
			{
			string url = deviceLevel
				 ? string.Format (RestConstants.WISERDEVICE, Id)
				 : string.Format (RestConstants.WISERSMARTVALVE, Id);

			bool result = await _wiserRestController.SendCommandAsync (url, cmd).ConfigureAwait (false);
			return result;
			}

		public WiserBattery Battery => new WiserBattery (_data);

		public bool DeviceLockEnabled => _deviceLockEnabled;
		public async Task<bool> SetDeviceLockEnabledAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				DeviceLockEnabled = value
				}, true).ConfigureAwait (false))
				{
				_deviceLockEnabled = value;
				return true;
				}
			return false;
			}

		public double CurrentTargetTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("SetPoint", out var setPoint) ? Convert.ToInt32 (setPoint) : 0);

		public double CurrentTemperature => WiserTemperatureFunctions.FromWiserTemp (
			 _deviceTypeData.TryGetValue ("MeasuredTemperature", out var temp) ? Convert.ToInt32 (temp) : 0, "current");

		public bool Identify => _identifyActive;
		public async Task<bool> SetIdentifyAsync (bool value)
			{
			if (await SendCommandAsync (new
				{
				Identify = value
				}, true).ConfigureAwait (false))
				{
				_identifyActive = value;
				return true;
				}
			return false;
			}

		public string MountingOrientation => _deviceTypeData.TryGetValue ("MountingOrientation", out var orientation) ? orientation.ToString () : null;

		public int PercentageDemand => _deviceTypeData.TryGetValue ("PercentageDemand", out var demand) ? Convert.ToInt32 (demand) : 0;

		public int RoomId => _deviceTypeData.TryGetValue ("RoomId", out var roomId) ? Convert.ToInt32 (roomId) : 0;
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
