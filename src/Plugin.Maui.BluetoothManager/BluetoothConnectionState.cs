namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// High-level connection lifecycle for a managed BLE device.
/// </summary>
public enum BluetoothConnectionState
{
	/// <summary>
	/// No session is tracked, or the last session was released.
	/// </summary>
	Disconnected = 0,

	/// <summary>
	/// A GATT connect is in flight.
	/// </summary>
	Connecting = 1,

	/// <summary>
	/// The link is up and services are being discovered.
	/// </summary>
	Discovering = 2,

	/// <summary>
	/// The device is connected and ready for read / write / notify.
	/// </summary>
	Connected = 3,

	/// <summary>
	/// A user-requested disconnect is in flight.
	/// </summary>
	Disconnecting = 4,

	/// <summary>
	/// The manager is retrying after an unexpected drop.
	/// </summary>
	Reconnecting = 5,

	/// <summary>
	/// Connect timed out or failed and auto-reconnect is not running.
	/// </summary>
	Failed = 6
}
