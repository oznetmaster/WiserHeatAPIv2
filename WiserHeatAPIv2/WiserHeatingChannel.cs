// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

namespace WiserHeatApiV2;

/// <summary>
/// Represents a heating channel with demand and relay state.
/// </summary>
/// <param name="data">The hub data for this device or channel.</param>
public class WiserHeatingChannel (IDictionary<string, object> data)
	{
	/// <summary>Gets the demand on/off output state.</summary>
	/// <value>The demand on/off output state.</value>
	public string? DemandOnOffOutput => data.TryGetValue ("DemandOnOffOutput", out var output) ? output.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the heating relay status.</summary>
	/// <value>The heating relay status.</value>
	public string? HeatingRelayStatus => data.TryGetValue ("HeatingRelayState", out var state) ? state.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the channel identifier.</summary>
	/// <value>The channel identifier.</value>
	public int Id => data.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets a value indicating whether a smart valve is preventing demand.</summary>
	/// <value>A value indicating whether a smart valve is preventing demand.</value>
	public bool IsSmartValvePreventingDemand => data.TryGetValue ("IsSmartValvePreventingDemand", out var preventing) && ConvertInvariant.ToBoolean (preventing);

	/// <summary>Gets the channel name.</summary>
	/// <value>The channel name.</value>
	public string? Name => data.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Gets the current percentage demand.</summary>
	/// <value>The current percentage demand.</value>
	public int PercentageDemand => data.TryGetValue ("PercentageDemand", out var demand) ? ConvertInvariant.ToInt32 (demand) : 0;

	/// <summary>Gets the room ids associated with this channel.</summary>
	/// <value>The room ids associated with this channel.</value>
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
	/// <param name="heatingChannelData">The heating channel records returned by the hub.</param>
	/// <param name="rooms">The room collection used to resolve channel associations.</param>
	public WiserHeatingChannels (List<Dictionary<string, object>> heatingChannelData, WiserRooms rooms)
		{
		_rooms = rooms;
		foreach (Dictionary<string, object> channel in heatingChannelData)
			{
			All.Add (new WiserHeatingChannel (channel));
			}
		}

	/// <summary>Replaces the collection with new channel data.</summary>
	/// <param name="heatingChannelData">The heating channel records returned by the hub.</param>
	/// <param name="rooms">The room collection used to resolve channel associations.</param>
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
	/// <value>All heating channels.</value>
	public List<WiserHeatingChannel> All { get; } = [];
	/// <summary>Gets the number of channels.</summary>
	/// <value>The number of channels.</value>
	public int Count => All.Count;
	/// <summary>Finds a channel by its identifier.</summary>
	/// <param name="id">The identifier to look up.</param>
	/// <returns>The first matching heating channel, or <see langword="null"/> if none matches.</returns>
	public WiserHeatingChannel? GetById (int id) => All.FirstOrDefault (channel => channel.Id == id);
	/// <summary>Finds a channel associated with a room id.</summary>
	/// <param name="id">The identifier of the associated room.</param>
	/// <returns>The first matching heating channel, or <see langword="null"/> if none matches.</returns>
	public WiserHeatingChannel? GetByRoomId (int id) => All.FirstOrDefault (channel => channel.RoomIds.Contains (id));
	/// <summary>Finds a channel associated with a room name.</summary>
	/// <param name="roomName">The room name used to locate an associated heating channel.</param>
	/// <returns>The first matching heating channel, or <see langword="null"/> if none matches.</returns>
	public WiserHeatingChannel? GetByRoomName (string roomName) =>
		_rooms.GetByName (roomName) is WiserRoom room ? GetByRoomId (room.Id) : null;
	}
