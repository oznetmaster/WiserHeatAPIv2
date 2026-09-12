// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

[TestFixture]
public sealed class RoomControlSupportTests
	{
	[TestCase ("Auto", "FromSchedule", "", "", true)]
	[TestCase ("Auto", "FromSchedule", "None", "0", true)]
	[TestCase ("Manual", "FromSchedule", "", "", false)]
	[TestCase ("Auto", "FromBoost", "Boost", "0", false)]
	[TestCase ("Auto", "FromSchedule", "Manual", "0", false)]
	[TestCase ("Auto", "FromSchedule", "None", "123", false)]
	[TestCase ("Auto", "FromAwayMode", "None", "0", false)]
	public void TemperatureControl_RequiresRestorableScheduleState (string mode, string origin, string type, string timer, bool expected)
		{
		var data = new Dictionary<string, object> { ["Mode"] = mode, ["SetpointOrigin"] = origin, ["OverrideType"] = type, ["OverrideTimeoutUnixTime"] = timer };
		Assert.That (RoomControlSupport.IsScheduledWithoutOverride (data), Is.EqualTo (expected));
		}

	[TestCase (false)]
	[TestCase (true)]
	public async Task Restoration_RunsAfterSuccessOrFailedCommand (bool fail)
		{
		bool restored = false;
		Task Run () => RoomControlSupport.ChangeAndRestoreAsync (
			_ => fail ? throw new InvalidOperationException ("Command response lost") : Task.CompletedTask,
			token => { Assert.That (token.IsCancellationRequested, Is.False); restored = true; return Task.CompletedTask; }, 10);
		if (fail)
			Assert.ThrowsAsync<InvalidOperationException> (async () => await Run ());
		else
			await Run ();
		Assert.That (restored, Is.True);
		}

	[Test]
	public void RestorationFailure_PreservesBothFailures ()
		{
		var error = Assert.ThrowsAsync<AggregateException> (async () => await RoomControlSupport.ChangeAndRestoreAsync (
			_ => throw new InvalidOperationException ("exercise"), _ => throw new InvalidOperationException ("cleanup"), 10));
		Assert.That (error!.InnerExceptions.Select (e => e.Message), Is.EqualTo (new[] { "exercise", "cleanup" }));
		}
	}