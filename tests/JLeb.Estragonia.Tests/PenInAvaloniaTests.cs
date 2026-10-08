using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using JLeb.Estragonia.Input;
using Xunit;

namespace JLeb.Estragonia.Tests;

/// <summary>
/// What a control sees when the tracker's events go through Avalonia's own input pipeline (headless window): a pen with
/// pressure and tilt, in contact while the tip touches, hovering otherwise — the events Estragonia makes from Godot's.
/// </summary>
public class PenInAvaloniaTests {

	private sealed record Seen(string Event, PointerType Type, bool InContact, float Pressure, float XTilt, bool Inverted, bool Eraser);

	[AvaloniaFact]
	public void A_control_sees_a_pen_press_drag_and_release_with_pressure_and_tilt_then_hover() {
		var target = new Border { Background = Avalonia.Media.Brushes.White, Width = 200, Height = 200 };
		var window = new Window { Content = target, Width = 200, Height = 200 };
		window.Show();
		AvaloniaHeadlessPlatform.ForceRenderTimerTick();

		var seen = new List<Seen>();
		void Record(string name, PointerEventArgs e) {
			var p = e.GetCurrentPoint(target);
			seen.Add(new Seen(name, e.Pointer.Type, p.Properties.IsLeftButtonPressed, p.Properties.Pressure, p.Properties.XTilt, p.Properties.IsInverted, p.Properties.IsEraser));
		}

		target.PointerPressed += (_, e) => Record("pressed", e);
		target.PointerMoved += (_, e) => Record("moved", e);
		target.PointerReleased += (_, e) => Record("released", e);

		var impl = (ITopLevelImpl)window.PlatformImpl!;
		// The root Avalonia gave the headless window (SetInputRoot), as Estragonia keeps the one Avalonia gives its top level.
		var root = (IInputRoot)impl.GetType().GetProperty("InputRoot")!.GetValue(impl)!;
		var pen = new PenTracker();
		var device = GodotDevices.GetPen(GodotPen.PencilDeviceId);
		void Feed(PenSample s) {
			if (pen.Create(device, 1, root, s) is { } args)
				impl.Input!(args);
		}

		// Pen only, no mouse before it: the window's layout is run first so the content is hit-tested.
		Avalonia.Threading.Dispatcher.UIThread.RunJobs();
		AvaloniaHeadlessPlatform.ForceRenderTimerTick();

		Feed(new PenSample(PenPhase.Move, new Point(50, 50), 0.4f, 20, 0));       // hover, the touch's pressure reported first
		Feed(new PenSample(PenPhase.Down, new Point(50, 50)));                   // as Godot's button event: no pressure, no tilt
		Feed(new PenSample(PenPhase.Move, new Point(60, 55), 0.9f, 25, 5));
		Feed(new PenSample(PenPhase.Up, new Point(60, 55)));
		Feed(new PenSample(PenPhase.Move, new Point(70, 60), Inverted: true));   // the eraser end, hovering
		Feed(new PenSample(PenPhase.Down, new Point(70, 60), 0.5f, Inverted: true));

		Assert.Equal(
			[
				new Seen("moved", PointerType.Pen, false, 0f, 20f, false, false),
				new Seen("pressed", PointerType.Pen, true, 0.4f, 20f, false, false),
				new Seen("moved", PointerType.Pen, true, 0.9f, 25f, false, false),
				new Seen("released", PointerType.Pen, false, 0f, 25f, false, false),
				new Seen("moved", PointerType.Pen, false, 0f, 0f, true, false),
				new Seen("pressed", PointerType.Pen, true, 0.5f, 0f, true, true),
			],
			seen);
	}

}
