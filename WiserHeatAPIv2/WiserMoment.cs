// Copyright © 2026 Neil Colvin.
// Adapted from the Python implementation Copyright © 2021 Mark Parker
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace WiserHeatApiV2;

/// <summary>
/// Represents a Moment (predefined action) that can be triggered on the hub.
/// </summary>
public class WiserMoment (WiserRestController wiserRestController, IDictionary<string, object> momentData)
	{
	private Task<bool> SendCommandAsync (object cmd, System.Threading.CancellationToken cancellationToken = default) =>
		wiserRestController.SendCommandAsync (RestConstants.WISER_REST_SYSTEM, cmd, cancellationToken: cancellationToken);

	/// <summary>Gets the moment identifier.</summary>
	public int Id => momentData.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets the moment name.</summary>
	public string? Name => momentData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Activates this moment on the hub.</summary>
	public Task<bool> ActivateAsync (System.Threading.CancellationToken cancellationToken = default) =>
		SendCommandAsync (new
			{
			TriggerMoment = Id
			}, cancellationToken);
	}

/// <summary>
/// Collection wrapper and lookup helpers for Moments.
/// </summary>
public class WiserMoments
	{
	private readonly WiserRestController _wiserRestController;

	/// <summary>Creates a collection of Moments from hub data.</summary>
	public WiserMoments (WiserRestController wiserRestController, List<Dictionary<string, object>> momentsData)
		{
		_wiserRestController = wiserRestController;
		foreach (Dictionary<string, object> moment in momentsData)
			{
			All.Add (new WiserMoment (wiserRestController, moment));
			}
		}

	/// <summary>Updates the collection using new hub data.</summary>
	public void Update (List<Dictionary<string, object>> momentsData)
		{
		All.Clear ();
		foreach (Dictionary<string, object> moment in momentsData)
			{
			All.Add (new WiserMoment (_wiserRestController, moment));
			}
		}

	/// <summary>Gets all moments.</summary>
	public List<WiserMoment> All { get; } = [];
	/// <summary>Gets the number of moments.</summary>
	public int Count => All.Count;
	/// <summary>Finds a moment by its identifier.</summary>
	public WiserMoment? GetById (int id) => All.FirstOrDefault (moment => moment.Id == id);
	}
