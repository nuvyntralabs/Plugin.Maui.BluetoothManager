namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Raised when a subscribed characteristic notifies or indicates a new value.
/// </summary>
public sealed class CharacteristicChangedEventArgs : EventArgs
{
	public CharacteristicChangedEventArgs(BleCharacteristic characteristic, byte[] value)
	{
		Characteristic = characteristic;
		Value = value;
	}

	public BleCharacteristic Characteristic { get; }

	public byte[] Value { get; }
}
