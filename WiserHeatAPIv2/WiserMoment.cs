// Copyright © 2026 Neil Colvin.
// Licensed under the MIT License. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;

namespace WiserHeatApiV2;

/// <summary>
/// Represents a Moment (predefined action) that can be triggered on the hub.
/// </summary>
/// <param name="wiserRestController">The controller used to communicate with the hub.</param>
/// <param name="momentData">The hub data describing this moment.</param>
public class WiserMoment (WiserRestController wiserRestController, IDictionary<string, object> momentData)
	{
	private Task<bool> SendCommandAsync (object cmd, System.Threading.CancellationToken cancellationToken = default) =>
		wiserRestController.SendCommandAsync (RestConstants.WISER_REST_SYSTEM, cmd, cancellationToken: cancellationToken);

	/// <summary>Gets the moment identifier.</summary>
	/// <value>The moment identifier.</value>
	public int Id => momentData.TryGetValue ("id", out var id) ? ConvertInvariant.ToInt32 (id) : 0;

	/// <summary>Gets the moment name.</summary>
	/// <value>The moment name.</value>
	public string? Name => momentData.TryGetValue ("Name", out var name) ? name.ToString () : Constants.TEXT_UNKNOWN;

	/// <summary>Activates this moment on the hub.</summary>
	/// <param name="cancellationToken">A token used to cancel the operation.</param>
	/// <returns>A task containing true if the hub accepts the activation command; otherwise false.</returns>
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
	/// <param name="wiserRestController">The controller used to communicate with the hub.</param>
	/// <param name="momentsData">The moment records returned by the hub.</param>
	public WiserMoments (WiserRestController wiserRestController, List<Dictionary<string, object>> momentsData)
		{
		_wiserRestController = wiserRestController;
		foreach (Dictionary<string, object> moment in momentsData)
			{
			All.Add (new WiserMoment (wiserRestController, moment));
			}
		}

	/// <summary>Updates the collection using new hub data.</summary>
	/// <param name="momentsData">The moment records returned by the hub.</param>
	public void Update (List<Dictionary<string, object>> momentsData)
		{
		All.Clear ();
		foreach (Dictionary<string, object> moment in momentsData)
			{
			All.Add (new WiserMoment (_wiserRestController, moment));
			}
		}

	/// <summary>Gets all moments.</summary>
	/// <value>All moments.</value>
	public List<WiserMoment> All { get; } = [];
	/// <summary>Gets the number of moments.</summary>
	/// <value>The number of moments.</value>
	public int Count => All.Count;
	/// <summary>Finds a moment by its identifier.</summary>
	/// <param name="id">The identifier to look up.</param>
	/// <returns>The first matching moment, or <see langword="null"/> if none matches.</returns>
	public WiserMoment? GetById (int id) => All.FirstOrDefault (moment => moment.Id == id);
	}
