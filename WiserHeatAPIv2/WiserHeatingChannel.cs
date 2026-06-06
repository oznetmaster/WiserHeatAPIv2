// Copyright © 2026 Neil Colvin.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

/// <summary>
/// Represents a heating channel with demand and relay state.
/// </summary>
public class WiserHeatingChannel (IDictionary<string, object> data)
	{
	/// <summary>Gets the demand on/off output state.</summary>
	public string? DemandOnOffOutput => data.TryGetValue ("DemandOnOffOutput", out var output) ? output.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the heating relay status.</summary>
	public string? HeatingRelayStatus => data.TryGetValue ("HeatingRelayState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the channel identifier.</summary>
	public int Id => data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets a value indicating whether a smart valve is preventing demand.</summary>
	public bool IsSmartValvePreventingDemand => data.TryGetValue ("IsSmartValvePreventingDemand", out var preventing) && ConvertInvariant.ToBoolean (preventing);

	/// <summary>Gets the channel name.</summary>
	public string? Name => data.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the current percentage demand.</summary>
	public int PercentageDemand => data.TryGetValue ("PercentageDemand", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;

	/// <summary>Gets the room ids associated with this channel.</summary>
	public List<int> RoomIds => data.TryGetValue ("RoomIds", out var roomIds) && roomIds is List<object> roomIdsList
		 ? [.. roomIdsList.Select (ConvertInvariant.ToInt32)]
		 : [];
	}

/// <summary>
/// Collection wrapper and lookup helpers for heating channels.
/// </summary>
public class WiserHeatingChannels
	{
	private WiserRooms _rooms;

	/// <summary>Creates a collection of heating channels.</summary>
	public WiserHeatingChannels (List<Dictionary<string, object>> heatingChannelData, WiserRooms rooms)
		{
		_rooms = rooms;
		foreach (Dictionary<string, object> channel in heatingChannelData)
			{
			All.Add (new WiserHeatingChannel (channel));
			}
		}

	/// <summary>Replaces the collection with new channel data.</summary>
	public void Update (List<Dictionary<string, object>> heatingChannelData, WiserRooms rooms)
		{
		_rooms = rooms;
		All.Clear ();
		foreach (Dictionary<string, object> channel in heatingChannelData)
			{
			All.Add (new WiserHeatingChannel (channel));
			}
		}

	/// <summary>Gets all heating channels.</summary>
	public List<WiserHeatingChannel> All { get; } = [];
	/// <summary>Gets the number of channels.</summary>
	public int Count => All.Count;
	/// <summary>Finds a channel by its identifier.</summary>
	public WiserHeatingChannel? GetById (int id) => All.FirstOrDefault (channel => channel.Id == id);
	/// <summary>Finds a channel associated with a room id.</summary>
	public WiserHeatingChannel? GetByRoomId (int id) => All.FirstOrDefault (channel => channel.RoomIds.Contains (id));
	/// <summary>Finds a channel associated with a room name.</summary>
	public WiserHeatingChannel? GetByRoomName (string roomName) =>
		_rooms.GetByName (roomName) is WiserRoom room ? GetByRoomId (room.Id) : null;
	}
