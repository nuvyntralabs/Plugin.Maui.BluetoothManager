using Microsoft.Extensions.DependencyInjection;
using Microsoft.Maui.Hosting;

namespace Plugin.Maui.BluetoothManager;

sealed class BluetoothManagerInitializer : IMauiInitializeService
{
	public void Initialize(IServiceProvider services)
	{
		var options = services.GetService<BluetoothManagerOptions>() ?? new BluetoothManagerOptions();
		var manager = services.GetService<IBluetoothManager>() ?? BluetoothManager.Current;

		if (options.EnableLogging)
		{
			var logger = options.Logger
				?? MauiAppBuilderExtensions.CreateLoggerAdapter(services)
				?? new DebugBluetoothManagerLogger();
			manager.EnableLogging(true, logger);
		}

		if (options.RequestPermissionsOnInitialize)
		{
			_ = manager.RequestPermissionsAsync();
		}
	}
}
