namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// GATT characteristic properties advertised by the peripheral.
/// </summary>
[Flags]
public enum BleCharacteristicProperties
{
	None = 0,
	Broadcast = 1,
	Read = 2,
	WriteWithoutResponse = 4,
	Write = 8,
	Notify = 16,
	Indicate = 32,
	AuthenticatedSignedWrites = 64,
	ExtendedProperties = 128
}
