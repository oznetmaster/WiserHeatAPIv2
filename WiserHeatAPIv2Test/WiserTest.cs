using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Bson;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using System.Net;
using System.Web;
using System.IO;
using log4net;
using log4net.Config;
using System.Collections.Specialized;
using System.Text;
using System.Text.RegularExpressions;
using WiserHub;
using System.Threading;

namespace WiserTest
	{
	public static class Program
		{
		public static ILog _LOGGER = log4net.LogManager.GetLogger (typeof (Program));

		static void Main ()
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

			var wh = new WiserAPI.wiserHub (IPAddress.Parse (wiserip), wiserkey);

			Console.WriteLine ("-------------------------------");
			Console.WriteLine ("Running tests");
			Console.WriteLine ("-------------------------------");
			Console.WriteLine ("Model # {0}", wh.getWiserHubName ());

			// Display some states
			// Heating State
			Console.WriteLine ("Hot water status {0} ", wh.getHotwaterRelayStatus ());
			// Assumes at least one roomstat
			Console.WriteLine ("Roomstat humidity {0}", wh.getRoomStatData (-1)["MeasuredHumidity"]);


			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("List of Devices");
			Console.WriteLine ("--------------------------------");

			foreach (var device in wh.getDevices ())
				{
				var deviceId = device["id"].Value<int> ();
				Console.WriteLine ("Device : Id {0} Room {1} Type {2}, SignalStrength {3}", deviceId, wh.getDeviceRoomName (deviceId), device["ProductType"], device["DisplayedSignalStrength"]);
				}

			//
			// Assume there room 1 :-), otherwise what are you heating?
			//
			int scheduleRoomTest = 3;

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Schedule for Room {0} [{1}] {2}", scheduleRoomTest, wh.getRoom (scheduleRoomTest)["Name"], wh.getRoomSchedule (scheduleRoomTest));
			Console.WriteLine ("--------------------------------");

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

			// List all Rooms

			findValve = 0;
			roomName = null;

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Listing all Rooms");
			Console.WriteLine ("--------------------------------");
			foreach (var roomTest in wh.getRooms ())
				{
				var smartValves = roomTest["SmartValveIds"];
				if (smartValves is null)
					Console.WriteLine ("Room ({1}) {0} has no smartValves", roomTest["Name"], roomTest["id"]);
				else
					Console.WriteLine (
						 "Room ({3}) {0}, setpoint={1}C, current temp={2}C",
							  roomTest["Name"],
							  roomTest["CurrentSetPoint"].Value<int> () / 10.0,
							  roomTest["CalculatedTemperature"].Value<int> () / 10.0,
							  roomTest["id"].Value<int> ()
						 );
				}

			Console.WriteLine ("--------------------------------");
			Console.WriteLine ("Listing all smartplugs");
			Console.WriteLine ("--------------------------------");

			// Find and set smartPlug on off
			if (wh.getSmartPlugs () != null)
				foreach (var smartPlug in wh.getSmartPlugs ())
					{
					var smartPlugId = smartPlug["id"].Value<int> ();
					Console.WriteLine (
						 "Smartplug ID {0} Name {1} OutputState is {2} Mode is {3}",
							  smartPlug["id"],
							  smartPlug["Name"],
							  wh.getSmartPlugState (smartPlugId),
							  wh.getSmartPlugMode (smartPlugId)
						 );
					Console.WriteLine ("Bouncing Plug {0} ", smartPlugId);
					var originalPlugState = wh.getSmartPlugState (smartPlugId);
					if (originalPlugState == "On")
						{
						wh.setSmartPlugState (smartPlugId, "Off");
						Thread.Sleep (1000);
						}
					else
						{
						wh.setSmartPlugState (smartPlugId, "On");
						Thread.Sleep (1000);
						}
					// Set back to original state
					wh.setSmartPlugState (smartPlugId, originalPlugState);
					}


			var temp = wh.getRoomStatData (3)["SetPoint"].Value<int> ();
			Console.WriteLine ("Room 3 setpoint is {0}", temp / 10.0);
			wh.setRoomTemperature (3, 14);
			wh.refreshData ();
			var newtemp = wh.getRoomStatData (3)["SetPoint"].Value<int> ();
			Console.WriteLine ("Room 3 setpoint is now {0}", newtemp / 10.0);
			wh.setRoomTemperature (3, temp / 10.0f);
			wh.refreshData ();
			newtemp = wh.getRoomStatData (3)["SetPoint"].Value<int> ();
			Console.WriteLine ("Room 3 setpoint is now reset to {0}", newtemp / 10.0);

			// Display some states
			//Heating State
			//_LOGGER.Logger (logging.DEBUG);
			//json.dump (room1schedule, f);
			//f.close ();
			/*
			wh.setRoomSchedule (scheduleRoomTest, data);
			wh.setSmartPlugState (smartPlug.get ("id"), "Off");
			time.sleep (1);
			wh.setSmartPlugState (smartPlug.get ("id"), "On");
			time.sleep (1);
			wh.setSmartPlugState (smartPlugId, originalPlugState);
			*/

			Console.WriteLine ();
			Console.WriteLine ("*********************** Done **********************");
			Console.ReadKey ();
			}

		//public static object data = f.read ().split ("\n");

		public static string wiserkey = "";

		public static string wiserip = "";

		//public static object line = lines.split ("=");

		//public static object wiserkey = line[1];

		//public static object wiserip = line[1];

		//public static WiserHub.WiserAPI wh = wiserHub.wiserHub (wiserip, wiserkey);


		//public static object data = json.load (f);

		public static object findValve = 0;

		public static object roomName = null;

		// public static object smartValves = scheduleRoomTest.get ("SmartValveIds");

		// public static object smartPlugId = smartPlug.get ("id");

		//public static object originalPlugState = wh.getSmartPlugState (smartPlugId);
		}
	}
