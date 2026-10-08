using System;
using Avalonia.VisualTree;
using Godot;
using HelloWorld.UI.ViewModels;
using HelloWorld.UI.Views;
using JLeb.Estragonia;
using AvControl = Avalonia.Controls.Control;

namespace HelloWorld;

public partial class UserInterface : UiHost {

	private HelloWorldViewModel? _vm;

	protected override AvControl CreateRoot() {
		_vm = new HelloWorldViewModel();
		return new HelloWorldView { DataContext = _vm };
	}

	public override void _Process(double delta) {
		base._Process(delta);
		PenDemo(delta);
		if (_vm is null)
			return;

		var fps = Engine.GetFramesPerSecond();
		var frameMs = delta > 0 ? delta * 1000.0 : 0;
		_vm.ReportFrame(fps, frameMs);
	}

	// ESTRAGONIA_PEN_DEMO=1: a scripted pen stroke over the Pen tab's pad, sent into Godot exactly as the iPad plugin sends
	// the Apple Pencil (mouse events of GodotPen.PencilDeviceId with pressure and tilt) — checks Godot's routing,
	// Estragonia and Avalonia end to end where there is no pen (a virtual display).
	private double _penDemo = -1;
	private int _penStep;

	public override void _Ready() {
		base._Ready();
		if (OS.GetEnvironment("ESTRAGONIA_PEN_DEMO") == "1")
			_penDemo = 0;
	}

	private void PenDemo(double delta) {
		if (_penDemo < 0)
			return;

		_penDemo += delta;
		if (_penDemo < 2 + _penStep * 0.05 || PenPad() is not { } pad)
			return;

		// Avalonia's device-independent pixels of the pad, in Godot's window pixels.
		var rect = pad.Bounds;
		var top = Avalonia.Controls.TopLevel.GetTopLevel(pad);
		var origin = top is null ? new Avalonia.Point() : Avalonia.VisualExtensions.TranslatePoint(pad, new Avalonia.Point(0, 0), top) ?? new Avalonia.Point();
		var scale = RenderScaling;
		Vector2 At(double fx, double fy) => GlobalPosition + new Vector2(
			(float)((origin.X + rect.Width * fx) * scale), (float)((origin.Y + rect.Height * fy) * scale));

		const int Points = 40;
		var i = _penStep++;
		if (i == 0) {
			Send(new InputEventMouseMotion { Position = At(0.1, 0.5), Pressure = 0.3f, Tilt = new Vector2(0.3f, 0f) });   // hover with the touch's pressure
			Send(new InputEventMouseButton { Position = At(0.1, 0.5), ButtonIndex = MouseButton.Left, Pressed = true, ButtonMask = MouseButtonMask.Left });
		}
		else if (i <= Points) {
			var t = i / (double)Points;
			Send(new InputEventMouseMotion {
				Position = At(0.1 + 0.8 * t, 0.5 + 0.3 * Math.Sin(t * Math.PI * 2)),
				Pressure = (float)(0.2 + 0.8 * t), Tilt = new Vector2(0.3f, 0.1f), ButtonMask = MouseButtonMask.Left
			});
		}
		else if (i == Points + 1) {
			Send(new InputEventMouseButton { Position = At(0.9, 0.5), ButtonIndex = MouseButton.Left, Pressed = false });
			Send(new InputEventMouseMotion { Position = At(0.92, 0.45), Tilt = new Vector2(0.3f, 0.1f) });   // hover after the stroke
			GD.Print("[pen-demo] stroke sent");
			_penDemo = -1;
		}
	}

	private static void Send(InputEventMouse e) {
		e.Device = JLeb.Estragonia.Input.GodotPen.PencilDeviceId;
		e.GlobalPosition = e.Position;
		Input.ParseInputEvent(e);
	}

	private Avalonia.Controls.Border? PenPad() {
		if (Control is not HelloWorldView view)
			return null;
		if (view.FindDescendantOfType<Avalonia.Controls.TabControl>() is { } tabs && tabs.SelectedIndex != 3) {
			tabs.SelectedIndex = 3;   // the Pen tab
			return null;
		}

		return view.FindDescendantOfType<PenPadView>()?.Pad;
	}
}
