namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// GATT write mode.
/// </summary>
public enum BleWriteType
{
	/// <summary>
	/// Choose WithResponse when the characteristic supports it, otherwise WithoutResponse.
	/// </summary>
	Default = 0,

	/// <summary>
	/// Write with a response from the peripheral.
	/// </summary>
	WithResponse = 1,

	/// <summary>
	/// Write without a response. Typical for printers and high-throughput sensors.
	/// </summary>
	WithoutResponse = 2
}
