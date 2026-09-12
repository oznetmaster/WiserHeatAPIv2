// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatAPIv2.Tests;

internal static class RoomControlSupport
	{
	internal static bool IsScheduledWithoutOverride (Dictionary<string, object> room)
		{
		string Text (string key) => room.TryGetValue (key, out var value) ? value?.ToString () ?? "" : "";
		string origin = Text ("SetpointOrigin");
		if (origin.Length == 0)
			origin = Text ("SetPointOrigin");
		return Text ("Mode") == "Auto" && origin == "FromSchedule"
			&& (Text ("OverrideType") is "" or "None")
			&& (Text ("OverrideTimeoutUnixTime") is "" or "0");
		}

	// A change can reach the hub even when its response is lost. Always restore, including
	// when the change itself throws, and give cleanup its own uncancelled deadline.
	internal static async Task ChangeAndRestoreAsync (Func<CancellationToken, Task> exercise,
		Func<CancellationToken, Task> restore, int timeoutSeconds)
		{
		Exception? failure = null;
		try
			{
			using var timeout = new CancellationTokenSource (TimeSpan.FromSeconds (timeoutSeconds));
			await exercise (timeout.Token);
			}
		catch (Exception error)
			{
			failure = error;
			throw;
			}
		finally
			{
			try
				{
				using var cleanup = new CancellationTokenSource (TimeSpan.FromSeconds (timeoutSeconds));
				await restore (cleanup.Token);
				}
			catch (Exception cleanupError)
				{
				if (failure != null)
					throw new AggregateException ("The control test failed and restoration also failed; check the configured room.", failure, cleanupError);
				throw new InvalidOperationException ("Room restoration failed; check the configured room.", cleanupError);
				}
			}
		}
	}