namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Result of a Bluetooth (and, on older Android, location) permission check.
/// </summary>
public enum BluetoothPermissionStatus
{
	/// <summary>
	/// The OS has not been asked, or the status cannot be mapped.
	/// </summary>
	Unknown = 0,

	/// <summary>
	/// Scan and connect permissions are granted (and location on Android API 30 and below).
	/// </summary>
	Granted = 1,

	/// <summary>
	/// The user denied at least one required permission. The app may ask again.
	/// </summary>
	Denied = 2,

	/// <summary>
	/// The user denied and selected "don't ask again", or iOS restricted the feature.
	/// Open Settings to recover.
	/// </summary>
	DeniedForever = 3,

	/// <summary>
	/// A profile restriction (MDM / parental controls) blocks Bluetooth.
	/// </summary>
	Restricted = 4
}
