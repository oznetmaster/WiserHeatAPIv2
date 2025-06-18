// Copyright © 2025 Nivloc Enterprises Ltd.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using log4net;
using log4net.Config;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using WiserHeatApiV2;

using WiserHeatingAPI;

namespace WiserHeatAPIv2Test
	{
	internal class WiserHeatAPIv2Test
		{
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (WiserHeatAPIv2Test));

		public static string wiserkey;
		public static string wiserip;

		private static WiserAPI wapi;

		static async Task Main (string[] args)
			{
			// Load log4net configuration
			XmlConfigurator.Configure (new FileInfo ("log4net.config"));

			var data = File.ReadAllLines ("wiserkeys.params");
			foreach (var lines in data)
				{
				var line = lines.Split ('=');
				if (line[0] == "wiserkey")
					wiserkey = line[1];
				if (line[0] == "wiserhubip")
					wiserip = line[1];
				}

			Console.WriteLine ($" Wiser Hub IP = {wiserip}, WiserKey = {wiserkey}");


			wapi = new WiserAPI (wiserip, wiserkey);

			Console.WriteLine ("-------------------------------");
			Console.WriteLine ("Running tests on Version {0}", wapi.System.ActiveSystemVersion);
			Console.WriteLine ("-------------------------------");
			Console.WriteLine ("Model # {0}", wapi.System.Model);
			Console.WriteLine ($"Hub Date/Time: {wapi.System.HubTime}");

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Wiser Hub Capabilities");
			Console.WriteLine ("--------------------------------");

			var capabilities = wapi.System.Capabilities;
			var capabilityProperties = capabilities.GetType ().GetProperties ();

			foreach (var property in capabilityProperties)
				{
				if (property.PropertyType == typeof (bool) && (bool)property.GetValue (capabilities))
					{
					Console.WriteLine ($"{property.Name}");
					}
				}

			// Display some states
			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Some States");
			Console.WriteLine ("--------------------------------");
			// Heating State
			Console.WriteLine ("Hot water status {0} ", wapi.Hotwater.IsHeating ? "Heating" : "Idle");
			// Assumes at least one roomstat
			Console.WriteLine ("Roomstat humidity {0}%", wapi.Rooms.All[3].CurrentHumidity);

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("List of Devices");
			Console.WriteLine ("--------------------------------");

			foreach (var device in wapi.Devices.All)
				{
				var deviceId = device.Id;
				var room = wapi.Rooms.GetByDeviceId (deviceId);
				Console.WriteLine ("Device : Id {0} Room {1} Type {2}, SignalStrength {3}", deviceId, room.Name, device.ProductType, device.Signal.DisplayedSignalStrength);
				}

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Listing all Rooms");
			Console.WriteLine ("--------------------------------");
			foreach (var roomTest in wapi.Rooms.All)
				{
				var smartValves = roomTest.SmartvalveIds;
				if (smartValves is null || smartValves.Count == 0)
					Console.WriteLine ("Room ({1}) {0} has no smartValves", roomTest.Name, roomTest.Id);
				else
					{
					Console.WriteLine (
						 "Room ({3}) {0}, setpoint={1}C, current temp={2}C",
							  roomTest.Name,
							  roomTest.CurrentTargetTemperature,
							  roomTest.CurrentTemperature,
							  roomTest.Id
						 );
					Console.WriteLine ("\tSmartvalves in this room:");
					foreach (var smartValveId in smartValves)
						{
						var smartValve = wapi.Devices.Smartvalves.All.FirstOrDefault (x => x.Id == smartValveId);
						if (smartValve != null)
							{
							Console.WriteLine ("\t\tSmartvalve ({0}) {1}, setpoint={2}C, current temp={3}C, battery={4}%",
								  smartValve.Name,
								  smartValve.Id,
								  smartValve.CurrentTargetTemperature,
								  smartValve.CurrentTemperature,
								  smartValve.Battery.Percent
							 );
							}
						else
							{
							Console.WriteLine ("Smartvalve ({0}) not found", smartValveId);
							}
						}
					}
				if (roomTest.RoomstatId != null)
					{
					var roomStat = wapi.Devices.Roomstats.All.FirstOrDefault (x => x.Id == roomTest.RoomstatId);
					Console.WriteLine ("\tRoomstat ({0}) {1}, setpoint={2}C, current temp={3}C, current humidity={4}% battery={5}%",
						  roomStat.Name,
						  roomStat.Id,
						  roomStat.CurrentTargetTemperature,
						  roomStat.CurrentTemperature,
						  roomStat.CurrentHumidity,
						  roomStat.Battery.Percent);
					}

				var smartPlugs = wapi.Devices.Smartplugs.All.Where (x => x.RoomId == roomTest.Id).ToList ();
				if (smartPlugs.Count > 0)
					{
					Console.WriteLine ("\tSmartplugs in this room:");
					foreach (var smartPlug in smartPlugs)
						{
						Console.WriteLine ("\t\tSmartplug ({0}) {1}, state={2}, scheduled state={3}",
							  smartPlug.Name,
							  smartPlug.Id,
							  smartPlug.IsOn ? "On" : "Off",
							  smartPlug.ScheduledState);
						}
					}
				else
					{
					Console.WriteLine ("\tNo Smartplugs in this room");
					}
				}

				{
				var roomToTest = 5;
				var room = wapi.Rooms.GetById (roomToTest);
				var temp = room.CurrentTargetTemperature;
				Console.WriteLine ($"Room {room.Name} setpoint is {room.CurrentTargetTemperature}");
				Console.WriteLine ($"Room {room.Name} IsOverride is {room.IsOverride}, IsBoost is {room.IsBoost}");
				await room.CancelBoostAsync ();
				await room.CancelOverridesAsync ();
				await room.SetTargetTemperatureForDurationOfScheduleAsync (14);
				await wapi.ReadHubDataAsync ();
				//room = wapi.Rooms.GetById (roomToTest);
				Console.WriteLine ($"Room {room.Name} setpoint is now {room.CurrentTargetTemperature}, scheduled setpoint is {room.ScheduledTargetTemperature}, override setpoint is {room.OverrideTargetTemperature}");
				Console.WriteLine ($"Room {room.Name} IsOverride is {room.IsOverride}, IsBoost is {room.IsBoost}");
				await room.CancelOverridesAsync ();
				await wapi.ReadHubDataAsync ();
				//room = wapi.Rooms.GetById (roomToTest);
				Console.WriteLine ($"Room {room.Name} setpoint is now reset to {room.CurrentTargetTemperature}");
				Console.WriteLine ($"Room {room.Name} IsOverride is {room.IsOverride}, IsBoost is {room.IsBoost}");
				}

			// Display some states
			var state = wapi.Devices.Smartplugs.All[0].IsOn;
			await wapi.Devices.Smartplugs.All[0].TurnOffAsync ();
			Thread.Sleep (1);
			await wapi.Devices.Smartplugs.All[0].TurnOnAsync ();
			Thread.Sleep (1);
			if (!state)
				await wapi.Devices.Smartplugs.All[0].TurnOffAsync ();

			int scheduleRoomTest = 5;
			var schedule = wapi.Schedules.GetByRoomId (scheduleRoomTest);
			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Schedule for Room {0} [{1}] {2} Type = {3}", scheduleRoomTest, wapi.Rooms.GetById (scheduleRoomTest).Name, schedule.Name, schedule.ScheduleType);
			Console.WriteLine ("--------------------------------");

			Console.WriteLine ($"Schedule.Next: DateTime {schedule.Next.DateTime} Day {schedule.Next.Day} Time {schedule.Next.Time} Setting {schedule.Next.Setting}");

			// Assume 'schedule' is an instance of WiserSchedule
			var scheduleData = schedule.ScheduleData;

			foreach (var day in scheduleData.Keys)
				{
				Console.WriteLine ($"Day: {day}");
				var slots = scheduleData[day] as Dictionary<string, object>;
				var times = slots["Time"] as List<object>;
				var settings = slots["DegreesC"] as List<object>;
				for (int i = 0; i < times.Count; i++)
					{
					Console.WriteLine ($"\tTime: {times[i]}, Setting: {WiserTemperatureFunctions.FromWiserTemp(settings[i])}");
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
