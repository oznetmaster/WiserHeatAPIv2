// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Reflection;

using log4net;
using log4net.Config;

using WiserHeatApiV2;

namespace WiserHeatAPIv2Test
	{
	internal class WiserHeatAPIv2Test
		{
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserHeatAPIv2Test));

		public static string wiserkey = string.Empty;
		public static string wiserip = string.Empty;

		private static WiserAPI? _wapi;

		static async Task Main ()
			{
			// Load log4net configuration
			_ = XmlConfigurator.Configure (new FileInfo ("log4net.config"));

			Console.WriteLine ("Wiser Heat API v2 Test");
			Console.WriteLine ("-------------------------------");
			Console.WriteLine ("This test requires a file named wiserkeys.params in the current directory.");
			Console.WriteLine ("This file should contain the Wiser key and Wiser hub IP address in the following format:");
			Console.WriteLine ("wiserkey=your_wiser_key_here");
			Console.WriteLine ("wiserhubip=your_wiser_hub_ip_here or discover");
			Console.WriteLine ("-------------------------------");
			if (!File.Exists ("wiserkeys.params"))
				{
				Console.WriteLine ("wiserkeys.params file not found.");
				Console.WriteLine ("Please create a file named wiserkeys.params with the following format:");
				Console.WriteLine ("wiserkey=your_wiser_key_here");
				Console.WriteLine ("wiserhubip=your_wiser_hub_ip_here or discover");
				return;
				}

			// Read the Wiser key and Wiser hub IP from the file
			var data = File.ReadAllLines ("wiserkeys.params");
			foreach (var lines in data)
				{
				var line = lines.Split ('=');
				if (line[0] == "wiserkey")
					wiserkey = line[1];
				if (line[0] == "wiserhubip")
					wiserip = line[1];
				}

			if (string.IsNullOrEmpty (wiserkey) || string.IsNullOrEmpty (wiserip))
				{
				Console.WriteLine ("wiserkeys.params file is missing or does not contain the required keys.");
				Console.WriteLine ("Please create a file named wiserkeys.params with the following format:");
				Console.WriteLine ("wiserkey=your_wiser_key_here");
				Console.WriteLine ("wiserhubip=your_wiser_hub_ip_here or discover");
				return;
				}

			Console.WriteLine ($" Wiser Hub IP = {wiserip}, WiserKey = {wiserkey}");

			if (string.Equals (wiserip, "discover", StringComparison.OrdinalIgnoreCase))
				{
				Console.WriteLine ("Discovering Wiser Hub IP address...");
				// Discover the Wiser Hub IP address
				List<WiserDiscoveredHub> discoveredHubs = await WiserDiscovery.DiscoverHubAsync (60, CancellationToken.None).ConfigureAwait (false);
				if (discoveredHubs != null)
					{
					if (discoveredHubs.Count == 1)
						{
						wiserip = discoveredHubs.First ().IpAddress.ToString ();
						Console.WriteLine ($"Discovered Wiser Hub IP: {wiserip}");
						}
					else if (discoveredHubs.Count > 1)
						{
						Console.WriteLine ("Multiple Wiser Hubs discovered. Please specify the IP address in the wiserkeys.params file.");
						foreach (WiserDiscoveredHub hub in discoveredHubs)
							{
							Console.WriteLine ($"Discovered Wiser Hub IP: {hub.IpAddress}");
							}

						return;
						}
					else
						{
						Console.WriteLine ("Failed to discover Wiser Hub IP address.");
						return;
						}
					}
				}

			_wapi = new WiserAPI (wiserip, wiserkey);
			if (_wapi != null)
				{
				Console.WriteLine ("WiserAPI created successfully");
				}
			else
				{
				Console.WriteLine ("Failed to create WiserAPI instance");
				return;
				}
			// Read the hub data
			await _wapi.InitializeAsync (CancellationToken.None);

			Console.WriteLine ("-------------------------------");
			Console.WriteLine ($"Running tests on Version {_wapi.System?.ActiveSystemVersion}");
			Console.WriteLine ("-------------------------------");
			Console.WriteLine ($"Model # {_wapi.System?.Model}");
			Console.WriteLine ($"Hub Date/Time: {_wapi.System?.HubTime}");
			Console.WriteLine ($"Hub ZigBee Channel: {_wapi.System?.Zigbee?.NetworkChannel}");

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Wiser Hub Capabilities");
			Console.WriteLine ("--------------------------------");

			WiserHubCapabilitiesInfo? capabilities = _wapi.System?.Capabilities;
			System.Reflection.PropertyInfo[]? capabilityProperties = capabilities?.GetType ().GetProperties ();

			if (capabilities == null || capabilityProperties == null || capabilityProperties.Length == 0)
				{
				Console.WriteLine ("No capabilities found.");
				}
			else
				{
				foreach (PropertyInfo property in capabilityProperties)
					{
					if (property.PropertyType == typeof (bool) && (bool)property.GetValue (capabilities))
						{
						Console.WriteLine ($"{property.Name}");
						}
					}
				}

			// Display some states
			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Some States");
			Console.WriteLine ("--------------------------------");
			// Heating State
			Console.WriteLine ($"Hot water status {(_wapi.Hotwater != null ? (_wapi.Hotwater.IsHeating ? "Heating" : "Idle") : "Unknown")}");
			// Assumes at least one roomstat
			Console.WriteLine ($"Roomstat humidity {_wapi.Rooms!.All[3].CurrentHumidity}%");

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("List of Devices");
			Console.WriteLine ("--------------------------------");

			if (_wapi.Devices != null && _wapi.Devices.All.Count > 0)
				{
				foreach (WiserDevice device in _wapi.Devices.All)
					{
					var deviceId = device.Id;
					WiserRoom? room = _wapi.Rooms.GetByDeviceId (deviceId);
					Console.WriteLine ($"Device : Id {deviceId} Room {room?.Name} Type {device.ProductType}, SignalStrength {device.Signal.DisplayedSignalStrength}");
					}
				}

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Listing all Rooms");
			Console.WriteLine ("--------------------------------");
			foreach (WiserRoom roomTest in _wapi.Rooms.All)
				{
				List<int>? smartValves = roomTest.SmartvalveIds;
				if (smartValves is null || smartValves.Count == 0)
					{
					Console.WriteLine ($"Room ({roomTest.Id}) {roomTest.Name} has no smartValves");
					}
				else
					{
					Console.WriteLine (
						 $"Room ({roomTest.Id}) {roomTest.Name}, setpoint={roomTest.CurrentTargetTemperature}C, current temp={roomTest.CurrentTemperature}C"
					);
					Console.WriteLine ("\tSmartvalves in this room:");
					foreach (var smartValveId in smartValves)
						{
						WiserSmartValve smartValve = _wapi.Devices!.Smartvalves.All.FirstOrDefault (x => x.Id == smartValveId);
						if (smartValve != null)
							{
							Console.WriteLine ($"\t\tSmartvalve ({smartValve.Name}) {smartValve.Id}, setpoint={smartValve.CurrentTargetTemperature}C, current temp={smartValve.CurrentTemperature}C, battery={smartValve.Battery.Percent}% {smartValve.Battery.Level}");
							}
						else
							{
							Console.WriteLine ($"Smartvalve ({smartValveId}) not found");
							}
						}
					}

				if (roomTest.RoomstatId != null)
					{
					WiserRoomStat roomStat = _wapi.Devices!.Roomstats.All.FirstOrDefault (x => x.Id == roomTest.RoomstatId);
					Console.WriteLine ($"\tRoomstat ({roomStat.Name}) {roomStat.Id}, setpoint={roomStat.CurrentTargetTemperature}C, current temp={roomStat.CurrentTemperature}C, current humidity={roomStat.CurrentHumidity}% battery={roomStat.Battery.Percent}% {roomStat.Battery.Level}");
					}

				var smartPlugs = _wapi.Devices!.Smartplugs.All.Where (x => x.RoomId == roomTest.Id).ToList ();
				if (smartPlugs.Count > 0)
					{
					Console.WriteLine ("\tSmartplugs in this room:");
					foreach (WiserSmartPlug smartPlug in smartPlugs)
						{
						Console.WriteLine ($"\t\tSmartplug ({smartPlug.Name}) {smartPlug.Id}, state={(smartPlug.IsOn ? "On" : "Off")}, scheduled state={smartPlug.ScheduledState}");
						}
					}
				else
					{
					Console.WriteLine ("\tNo Smartplugs in this room");
					}

				var lowBattery = (roomTest.RoomstatId.HasValue && _wapi.Devices.Roomstats.GetById (roomTest.RoomstatId.Value)?.Battery.Level == "Low")
					|| (roomTest.SmartvalveIds?.Count > 0 && roomTest.SmartvalveIds.Any (svid => _wapi.Devices.Smartvalves.GetById (svid).Battery.Level == "Low"));
				if (lowBattery)
					{
					Console.WriteLine ($"\tLow battery warning in room: {roomTest.Name}");
					}
				}

				{
				var roomToTest = 5;
				WiserRoom room = _wapi.Rooms.GetById (roomToTest);
				Console.WriteLine ($"Room {room.Name} setpoint is {room.CurrentTargetTemperature}");
				Console.WriteLine ($"Room {room.Name} IsOverride is {room.IsOverride}, IsBoost is {room.IsBoost}");
				_ = await room.CancelBoostAsync ();
				_ = await room.CancelOverridesAsync ();
				_ = await room.SetTargetTemperatureForDurationOfScheduleAsync (14);
				_ = await _wapi.ReadHubDataAsync ();
				//room = wapi.Rooms.GetById (roomToTest);
				Console.WriteLine ($"Room {room.Name} setpoint is now {room.CurrentTargetTemperature}, scheduled setpoint is {room.ScheduledTargetTemperature}, override setpoint is {room.OverrideTargetTemperature}");
				Console.WriteLine ($"Room {room.Name} IsOverride is {room.IsOverride}, IsBoost is {room.IsBoost}");
				_ = await room.CancelOverridesAsync ();
				_ = await _wapi.ReadHubDataAsync ();
				//room = wapi.Rooms.GetById (roomToTest);
				Console.WriteLine ($"Room {room.Name} setpoint is now reset to {room.CurrentTargetTemperature}");
				Console.WriteLine ($"Room {room.Name} IsOverride is {room.IsOverride}, IsBoost is {room.IsBoost}");
				}

			// Display some states
			var state = _wapi.Devices!.Smartplugs.All[0].IsOn;
			_ = await _wapi.Devices.Smartplugs.All[0].TurnOffAsync ();
			Thread.Sleep (1);
			_ = await _wapi.Devices.Smartplugs.All[0].TurnOnAsync ();
			Thread.Sleep (1);
			if (!state)
				_ = await _wapi.Devices.Smartplugs.All[0].TurnOffAsync ();

			var scheduleRoomTest = 5;
			WiserHeatingSchedule? schedule = _wapi.Schedules!.GetByRoomId (scheduleRoomTest);
			if (schedule == null)
				{
				Console.WriteLine ($"No schedule found for Room {scheduleRoomTest}");
				}
			else
				{
				Console.WriteLine ("--------------------------------");
				Console.WriteLine ($"Schedule for Room {scheduleRoomTest} [{_wapi.Rooms.GetById (scheduleRoomTest).Name}] {schedule.Name} Type = {schedule.ScheduleType}");
				Console.WriteLine ("--------------------------------");

				Console.WriteLine ($"Schedule.Next: DateTime {schedule.Next?.DateTime} Day {schedule.Next?.Day} Time {schedule.Next?.Time} Setting {schedule.Next?.Setting}");

				// Assume 'schedule' is an instance of WiserSchedule
				IDictionary<string, object> scheduleData = schedule.ScheduleData;

				foreach (var day in scheduleData.Keys.OrderBy (k => Enum.Parse (typeof (DayOfWeek), k)))
					{
					Console.WriteLine ($"Day: {day}");
					if (scheduleData[day] is not Dictionary<string, object> slots)
						{
						Console.WriteLine ($"\tNo slots found for {day}");
						continue;
						}

					if (slots["Time"] is not List<object> times || slots["DegreesC"] is not List<object> settings)
						{
						Console.WriteLine ($"\tNo times or settings found for {day}");
						continue;
						}

					for (var i = 0; i < times.Count; i++)
						{
						Console.WriteLine ($"\tTime: {times[i].ToWiserTime ()}, Setting: {WiserTemperatureFunctions.FromWiserTemp (settings[i]):f1}");
						}
					}
				//Console.WriteLine ($"Next schedule change: {next.Time}, Level: {next.Setting}");
				/*
				// Query Schedule for Room1
				// Big assumption there is always a room 1 :-)
				//
				using (var s = new StreamWriter ($"./room{scheduleRoomTest}schedule.json"))
					{
					var room3schedule = wh.getRoomSchedule (scheduleRoomTest);
					var writer = new JsonTextWriter (s);
					room3schedule.WriteTo (writer);
					}
				Console.WriteLine ($"File room{scheduleRoomTest}schedule.json created ");
				// Load schedule file and set schedule
				Console.WriteLine ("--------------------------------");
				Console.WriteLine ("Set room schedule for Room {0}", scheduleRoomTest);
				using (var s = new StreamReader ($"./room{scheduleRoomTest}schedule.json"))
					{
					var reader = new JsonTextReader (s);
					var jdata = JObject.Load (reader);
					//wh.setRoomSchedule (scheduleRoomTest, jdata);
					}
				Console.WriteLine ("Schedule for room {0} loaded indirectly from file", scheduleRoomTest);

				Console.WriteLine ("--------------------------------");
				*/
				}
			}
		}
	}
