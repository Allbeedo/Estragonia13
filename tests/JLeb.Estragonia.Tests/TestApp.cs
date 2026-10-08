using Avalonia;
using Avalonia.Headless;

[assembly: AvaloniaTestApplication(typeof(JLeb.Estragonia.Tests.TestApp))]

namespace JLeb.Estragonia.Tests;

/// <summary>Avalonia's headless platform, for tests that need a real window to receive input.</summary>
public sealed class TestApp : Application {

	public static AppBuilder BuildAvaloniaApp()
		=> AppBuilder.Configure<TestApp>().UseHeadless(new AvaloniaHeadlessPlatformOptions());

}
