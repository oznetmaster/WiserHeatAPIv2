// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.IO;

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class ScheduleCommandResultTests
	{
	[Test]
	public async Task Commands_ReportHubOutcomeWithoutRepeatingTheWrite (
		[Values ("AssignHeating", "UnassignHeating", "AssignOnOff", "UnassignOnOff", "AssignLevel", "UnassignLevel", "Copy", "Delete", "Set", "Json", "Editor")]
		string operation,
		[Values ("Accepted", "Rejected", "Disconnected")] string outcome)
		{
		using var hub = new ScriptedHub ();
		using var controller = new WiserRestController (new WiserConnection ("hub.example", "test-secret"), hub);
		var data = ScriptedHub.Data ("""{"id":7,"Name":"Weekdays","Type":"Heating","Monday":{"Time":[630,2200],"DegreesC":[205,160]}}""");
		var heating = new WiserHeatingSchedule (controller, "Heating", data, new Dictionary<string, string> (), new Dictionary<string, string> ());
		var onOff = new WiserOnOffSchedule (controller, "OnOff", ScriptedHub.Data ("""{"id":8,"Name":"Switch"}"""), new Dictionary<string, string> (), new Dictionary<string, string> ());
		var level = new WiserLevelSchedule (controller, "Level", ScriptedHub.Data ("""{"id":9,"Name":"Light"}"""), new Dictionary<string, string> (), new Dictionary<string, string> ());
		string original = JObject.FromObject (heating.ScheduleData).ToString ();
		if (outcome == "Disconnected")
			hub.Respond ((_, _) => throw new HttpRequestException ("Connection lost after sending the command."));
		else
			hub.Reply (status: outcome == "Accepted" ? HttpStatusCode.OK : HttpStatusCode.BadRequest);

		string? file = null;
		try
			{
			if (operation == "Json")
				{
				file = Path.GetTempFileName ();
				File.WriteAllText (file, JObject.FromObject (data).ToString ());
				}

			// Use the editor's documented dictionary/list shape with a nonempty day.
			var editor = new Dictionary<string, object>
				{
				["Type"] = "Heating",
				["ScheduleData"] = new List<object>
					{
					new Dictionary<string, object>
						{
						["day"] = "Monday",
						["slots"] = new List<IDictionary<string, object>>
							{
							new Dictionary<string, object> { ["Time"] = "06:30", ["Setpoint"] = 20.5 },
							new Dictionary<string, object> { ["Time"] = "22:00", ["Setpoint"] = 16.0 }
							}
						}
					}
				};
			bool result = await (operation switch
				{
				"AssignHeating" => heating.AssignScheduleAsync ([4]),
				"UnassignHeating" => heating.UnassignScheduleAsync ([4]),
				"AssignOnOff" => onOff.AssignScheduleAsync ([4]),
				"UnassignOnOff" => onOff.UnassignScheduleAsync ([4]),
				"AssignLevel" => level.AssignScheduleAsync ([4]),
				"UnassignLevel" => level.UnassignScheduleAsync ([4]),
				"Copy" => heating.CopyScheduleAsync (10),
				"Delete" => heating.DeleteScheduleAsync (),
				"Set" => heating.SetScheduleAsync (data),
				"Json" => heating.SetScheduleFromFileAsync (file!),
				"Editor" => heating.SetScheduleFromWsDataAsync (editor),
				_ => throw new InvalidOperationException (operation)
				});

			Assert.Multiple (() =>
				{
				Assert.That (result, Is.EqualTo (outcome == "Accepted"));
				Assert.That (hub.Requests, Has.Count.EqualTo (1), "Do not repeat rejected or uncertain writes.");
				Assert.That (JObject.FromObject (heating.ScheduleData).ToString (), Is.EqualTo (original));
				});
			if (operation is "Json" or "Editor" or "Set" or "Copy")
				Assert.That (JObject.Parse (hub.Requests.Single ().Body)["Monday"], Is.Not.Null, "Exercise a real day payload, not an empty schedule.");
			hub.AssertComplete ();
			}
		finally
			{
			if (file != null)
				File.Delete (file);
			}
		}
	}
