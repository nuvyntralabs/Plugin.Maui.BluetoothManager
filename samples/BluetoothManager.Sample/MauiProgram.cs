using Microsoft.Extensions.Logging;
using Plugin.Maui.BluetoothManager;

namespace BluetoothManager.Sample;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder
			.UseMauiApp<App>()
			.UseBluetoothManager(options =>
			{
				options.EnableLogging = true;
				options.AutoReconnect = true;
				options.ConnectionTimeout = TimeSpan.FromSeconds(15);
				options.MaxReconnectAttempts = 5;
				options.DefaultScanDuration = TimeSpan.FromSeconds(8);
			});

#if DEBUG
		builder.Logging.AddDebug();
#endif

		return builder.Build();
	}
}
