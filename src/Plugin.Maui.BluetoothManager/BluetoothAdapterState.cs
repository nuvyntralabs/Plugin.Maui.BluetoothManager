namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Radio / authorization state of the platform Bluetooth adapter.
/// </summary>
public enum BluetoothAdapterState
{
	/// <summary>
	/// The adapter has not reported a state yet.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// BLE is not available on this target (for example the shared <c>net10.0</c> surface).
	/// </summary>
	Unsupported = 1,

	/// <summary>
	/// The user or OS has denied Bluetooth access.
	/// </summary>
	Unauthorized = 2,

	/// <summary>
	/// The adapter is turning on or resetting.
	/// </summary>
	Resetting = 3,

	/// <summary>
	/// Bluetooth is powered off.
	/// </summary>
	Off = 4,

	/// <summary>
	/// Bluetooth is powered on and ready to scan or connect.
	/// </summary>
	On = 5
}
