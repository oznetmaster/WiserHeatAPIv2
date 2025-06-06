// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using log4net;

using Newtonsoft.Json;

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

using WiserHeatApiV2;

namespace WiserHeatingAPI
	{
	/// <summary>
	/// Wiser API Version 2
	/// 
	/// angelosantagata@gmail.com
	/// msparker@sky.com
	/// 
	/// https://github.com/asantaga/wiserheatingapi
	/// 
	/// This API allows you to get information from and control your wiserhub.
	/// </summary>
	// TODO: Keep objects and update instead of recreating on hub update
	// TODO: Update entity values after commend issued to get current values
	public class WiserAPI
		{
		// Update the logger initialization to ensure the `AddConsole` method is recognized.
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserAPI));

		private const string VERSION = "1.0.0"; // Assuming a version, replace with actual version

		// Connection variables
		private WiserConnection _wiserApiConnection;
		private WiserRestController _wiserRestController;

		// Hub Data
		private Dictionary<string, object> _domainData = new Dictionary<string, object> ();
		private Dictionary<string, object> _networkData = new Dictionary<string, object> ();
		private Dictionary<string, object> _scheduleData = new Dictionary<string, object> ();
		private Dictionary<string, object> _openthermData = new Dictionary<string, object> ();

		// Data stores for exposed properties
		private WiserDeviceCollection _devices;
		private WiserHotwater _hotwater;
		private WiserHeatingChannelCollection _heatingChannels;
		private WiserMomentCollection _moments;
		private WiserRoomCollection _rooms;
		private WiserScheduleCollection _schedules;
		private WiserSystem _system;

		/// <summary>
		/// Main api class to access all entities and attributes of wiser system
		/// </summary>
		public WiserAPI (string host, string secret, WiserUnitsEnum units = WiserUnitsEnum.Metric)
			{
			var logger = ((log4net.Repository.Hierarchy.Logger)((log4net.Core.LogImpl)_LOGGER).Logger);
#if DEBUG
			logger.Level = log4net.Core.Level.Debug;
#else
			logger.Level = log4net.Core.Level.Error;
#endif

			// Connection variables
			_wiserApiConnection = new WiserConnection () { Host = host, Secret = secret, Units = units };
			_wiserRestController = null;

			// Log initialization info
			_LOGGER.InfoFormat ($"WiserHub API v{VERSION} Initialised - Host: {host}, Units: {_wiserApiConnection.Units.ToString ().ToTitleCase ()}");

			// Read hub data if hub IP and secret exist
			if (!string.IsNullOrEmpty (_wiserApiConnection.Host) && !string.IsNullOrEmpty (_wiserApiConnection.Secret))
				{
				_wiserRestController = new WiserRestController (_wiserApiConnection);
				ReadHubDataAsync ().Wait ();
				}
			else
				throw new WiserHubConnectionException ("Missing or incomplete connection information");
			}

		/// <summary>
		/// Read all data from hub and populate objects
		/// </summary>
		public async Task<bool> ReadHubDataAsync ()
			{
			try
				{
				// Read data from hub
				var newDomainData = await _wiserRestController.GetHubDataAsync (string.Format (RestConstants.WISERHUBDOMAIN, _wiserApiConnection.Host)).ConfigureAwait (false);
				var newNetworkData = await _wiserRestController.GetHubDataAsync (string.Format (RestConstants.WISERHUBNETWORK, _wiserApiConnection.Host)).ConfigureAwait (false);
				var newScheduleData = await _wiserRestController.GetHubDataAsync (string.Format (RestConstants.WISERHUBSCHEDULES, _wiserApiConnection.Host)).ConfigureAwait (false);
				var newOpenthermData = await _wiserRestController.GetHubDataAsync (string.Format (RestConstants.WISERHUBOPENTHERM, _wiserApiConnection.Host), false).ConfigureAwait (false);

				// Update internal data
				_domainData = newDomainData;
				_networkData = newNetworkData;
				_scheduleData = newScheduleData;
				_openthermData = newOpenthermData;
				}
			catch (Exception ex)
				{
				_LOGGER.Error (ex);
				return false;
				}

			if (_domainData.Count > 0 && _networkData.Count > 0 && _scheduleData.Count > 0)
				{
				if (!(_domainData.TryGetValue ("Device", out var deviceDataObj) && deviceDataObj is List<Dictionary<string, object>> deviceData))
					deviceData = new List<Dictionary<string, object>> ();

				// Keep objects and update instead of recreating on hub update
				if (_system != null)
					_system.Update (_domainData, _networkData, deviceData, _openthermData);
				else
					_system = new WiserSystem (_wiserRestController, _domainData, _networkData, deviceData, _openthermData);

				if (_schedules != null)
					_schedules.Update (_scheduleData, _system.SunriseTimes, _system.SunsetTimes);
				else
					_schedules = new WiserScheduleCollection (_wiserRestController, _scheduleData, _system.SunriseTimes, _system.SunsetTimes);

				if (_devices != null)
					_devices.Update (_domainData, _schedules);
				else
					_devices = new WiserDeviceCollection (_wiserRestController, _domainData, _schedules);

				if (!(_domainData.TryGetValue ("Room", out var roomDataObj) && roomDataObj is List<Dictionary<string, object>> roomData))
					roomData = new List<Dictionary<string, object>> ();

				if (_rooms != null)
					_rooms.Update (roomData, _schedules, _devices);
				else
					_rooms = new WiserRoomCollection (_wiserRestController, roomData, _schedules, _devices);

				if (_domainData.TryGetValue ("HotWater", out var hotWaterDataObj) && hotWaterDataObj is List<Dictionary<string, object>> hotWaterData)
					{
					if (hotWaterData.Count > 0)
						{
						int scheduleId = hotWaterData[0].ContainsKey ("ScheduleId") ? Convert.ToInt32 (hotWaterData[0]["ScheduleId"]) : 0;
						var schedule = _schedules.GetById (WiserScheduleTypeEnum.OnOff, scheduleId);
						if (_hotwater != null)
							_hotwater.Update (hotWaterData[0], schedule);
						else
							_hotwater = new WiserHotwater (_wiserRestController, hotWaterData[0], schedule);
						}
					}

				if (_domainData.TryGetValue ("HeatingChannel", out var heatingChannelDataObj) && heatingChannelDataObj is List<Dictionary<string, object>> heatingChannelData)
					{
					if (_heatingChannels != null)
						_heatingChannels.Update (heatingChannelData, _rooms);
					else
						_heatingChannels = new WiserHeatingChannelCollection (heatingChannelData, _rooms);
					}

				if (_domainData.TryGetValue ("Moment", out var momentDataObj) && momentDataObj is List<Dictionary<string, object>> momentData)
					{
					if (_moments != null)
						_moments.Update (momentData);
					else
						_moments = new WiserMomentCollection (_wiserRestController, momentData);
					}

				// If gets here with no exceptions then success and return true
				return true;
				}

			_LOGGER.DebugFormat ($"No update: _domainData: {_domainData.Count}, _networkData: {_networkData.Count}, _scheduleData: {_scheduleData.Count}");
			return false;
			}

		// API properties
		/// <summary>
		/// List of device entities attached to the Wiser Hub
		/// </summary>
		public WiserDeviceCollection Devices => _devices;

		/// <summary>
		/// List of heating channel entities on the Wiser Hub
		/// </summary>
		public WiserHeatingChannelCollection HeatingChannels => _heatingChannels;

		/// <summary>
		/// List of hot water entities on the Wiser Hub
		/// </summary>
		public WiserHotwater Hotwater => _hotwater;

		/// <summary>
		/// List of moment entities on the Wiser Hub
		/// </summary>
		public WiserMomentCollection Moments => _moments;

		/// <summary>
		/// List of room entities configured on the Wiser Hub
		/// </summary>
		public WiserRoomCollection Rooms => _rooms;

		/// <summary>
		/// List of schedules
		/// </summary>
		public WiserScheduleCollection Schedules => _schedules;

		/// <summary>
		/// Entity of the Wiser Hub
		/// </summary>
		public WiserSystem System => _system;

		/// <summary>
		/// Get or set units for temperature
		/// </summary>
		public WiserUnitsEnum Units
			{
			get => _wiserApiConnection.Units;
			set => _wiserApiConnection.Units = value;
			}

		/// <summary>
		/// API Version
		/// </summary>
		public string Version => VERSION;

		/// <summary>
		/// Raw hub data
		/// </summary>
		public Dictionary<string, object> RawHubData => new Dictionary<string, object>
			{
			["Domain"] = _domainData,
			["Network"] = _networkData,
			["Schedule"] = _scheduleData,
			["OpenTherm"] = _openthermData
			};

		/// <summary>
		/// Output raw hub data to json file
		/// </summary>
		public async Task<bool> OutputRawHubData (string dataClass, string filename, string filePath)
			{
			// Get correct endpoint
			string endpoint = null;
			if (dataClass.ToLower () == "domain")
				endpoint = RestConstants.WISERHUBDOMAIN;
			else if (dataClass.ToLower () == "network")
				endpoint = RestConstants.WISERHUBNETWORK;
			else if (dataClass == "schedules")
				endpoint = RestConstants.WISERHUBSCHEDULES;

			// Get raw json data
			if (!string.IsNullOrEmpty (endpoint))
				{
				var data = (Dictionary<string, object>)await _wiserRestController.GetHubDataAsync (endpoint).ConfigureAwait (false);
				try
					{
					if (data != null && data.Count > 0)
						{
						// Write out to file
						LogResponseToFile (data, filename, false, Path.Combine (filePath));
						return true;
						}
					}
				catch (Exception ex)
					{
					_LOGGER.Error (ex.Message);
					return false;
					}
				}
			return false;
			}

		private void LogResponseToFile (Dictionary<string, object> data, string filename, bool append, string filePath)
			{
			string fullPath = Path.Combine (filePath, filename);
			string jsonData = JsonConvert.SerializeObject (data, new JsonSerializerSettings { Formatting = Newtonsoft.Json.Formatting.Indented });
			File.WriteAllText (fullPath, jsonData);
			}
		}

	public static class StringExtensions
		{
		public static string ToTitleCase (this string input)
			{
			if (string.IsNullOrEmpty (input))
				return input;

			return char.ToUpper (input[0]) + input.Substring (1).ToLower ();
			}
		}

	// Constants
	public static class WiserConstants
		{
		public const double DEFAULT_AWAY_MODE_TEMP = 16.0;
		public const double DEFAULT_DEGRADED_TEMP = 18.0;
		public const int MAX_BOOST_INCREASE = 5;
		public const double TEMP_ERROR = -1;
		public const double TEMP_HW_ON = -20;
		public const double TEMP_HW_OFF = -20.5;
		public const double TEMP_MINIMUM = 5;
		public const double TEMP_MAXIMUM = 30;
		public const double TEMP_OFF = -20;
		public const string WISERHUBDOMAIN = "domain";
		public const string WISERHUBNETWORK = "network";
		public const string WISERHUBSCHEDULES = "schedules";
		public const string WISERHUBOPENTHERM = "opentherm";
		}

	// Exceptions
	public class WiserHubConnectionException : Exception
		{
		public WiserHubConnectionException (string message) : base (message) { }
		}
	}
