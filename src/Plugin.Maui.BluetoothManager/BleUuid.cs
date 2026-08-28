namespace Plugin.Maui.BluetoothManager;

/// <summary>
/// Helpers for 16-bit Bluetooth SIG UUIDs and 128-bit custom UUIDs.
/// </summary>
public static class BleUuid
{
	/// <summary>
	/// Bluetooth Base UUID used to expand 16-bit assigned numbers.
	/// </summary>
	public static readonly Guid BluetoothBase = Guid.Parse("00000000-0000-1000-8000-00805f9b34fb");

	/// <summary>
	/// Client Characteristic Configuration descriptor used to enable notify / indicate.
	/// </summary>
	public static readonly Guid ClientCharacteristicConfiguration = Guid.Parse("00002902-0000-1000-8000-00805f9b34fb");

	/// <summary>
	/// Expands a 16-bit assigned number into a 128-bit Bluetooth UUID.
	/// </summary>
	public static Guid FromShort(ushort value)
	{
		var bytes = BluetoothBase.ToByteArray();
		bytes[0] = (byte)(value & 0xFF);
		bytes[1] = (byte)(value >> 8);
		return new Guid(bytes);
	}

	/// <summary>
	/// Parses a 16-bit hex value (for example <c>180A</c>) or a full GUID string.
	/// </summary>
	public static Guid Parse(string value)
	{
		ArgumentException.ThrowIfNullOrWhiteSpace(value);

		var trimmed = value.Trim();
		if (trimmed.Length is 4 && ushort.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber, null, out var shortId))
			return FromShort(shortId);

		return Guid.Parse(trimmed);
	}
}
