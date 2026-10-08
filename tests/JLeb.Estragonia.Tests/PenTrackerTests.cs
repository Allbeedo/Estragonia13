using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Raw;
using JLeb.Estragonia.Input;
using Xunit;

namespace JLeb.Estragonia.Tests;

/// <summary>
/// The pen's way into Avalonia (<see cref="PenTracker"/>, <see cref="GodotPen"/>): a pen is a pen with pressure, tilt,
/// twist, hover and its eraser end, and Avalonia always sees down–move–up in order, whatever order the samples come in.
/// </summary>
public class PenTrackerTests {

	/// <summary>A root that answers nothing: the tracker only hands it to the event.</summary>
	private sealed class NoRoot : IInputRoot {
		IFocusManager IInputRoot.FocusManager => null!;
		IInputElement? IInputRoot.PointerOverElement { get; set; }
		Avalonia.Input.TextInput.ITextInputMethodImpl? IInputRoot.InputMethod => null;
		InputElement IInputRoot.RootElement => null!;
		InputElement IInputRoot.FocusRoot => null!;
	}

	private static readonly IInputRoot TheRoot = new NoRoot();
	private static readonly IInputDevice Pen = new PenDevice(releasePointerOnPenUp: true);

	private static RawPointerEventArgs? Feed(PenTracker pen, PenSample sample, IReadOnlyList<PenSample>? intermediate = null)
		=> pen.Create(Pen, 1, TheRoot, sample, intermediate);

	[Fact]
	public void A_stroke_is_a_pen_press_moves_with_pressure_and_tilt_and_a_release() {
		var pen = new PenTracker();
		var hover = Feed(pen, new PenSample(PenPhase.Move, new Point(10, 20), XTilt: 30, YTilt: -15))!;
		Assert.Equal(RawPointerEventType.Move, hover.Type);
		Assert.Equal(0f, hover.Point.Pressure);                              // hovering: no pressure
		Assert.False(hover.InputModifiers.HasFlag(RawInputModifiers.LeftMouseButton));
		Assert.True(pen.Hovering);

		var down = Feed(pen, new PenSample(PenPhase.Down, new Point(10, 20), 0.3f, 30, -15))!;
		Assert.Equal(RawPointerEventType.LeftButtonDown, down.Type);
		Assert.Equal((0.3f, 30f, -15f), (down.Point.Pressure, down.Point.XTilt, down.Point.YTilt));
		Assert.True(down.InputModifiers.HasFlag(RawInputModifiers.LeftMouseButton));
		Assert.Same(Pen, down.Device);

		var move = Feed(pen, new PenSample(PenPhase.Move, new Point(12, 24), 0.8f, 10, 5, Twist: 45))!;
		Assert.Equal(RawPointerEventType.Move, move.Type);
		Assert.Equal((new Point(12, 24), 0.8f, 45f), (move.Point.Position, move.Point.Pressure, move.Point.Twist));
		Assert.True(move.InputModifiers.HasFlag(RawInputModifiers.LeftMouseButton));

		var up = Feed(pen, new PenSample(PenPhase.Up, new Point(12, 24)))!;
		Assert.Equal(RawPointerEventType.LeftButtonUp, up.Type);
		Assert.Equal(0f, up.Point.Pressure);
		Assert.False(up.InputModifiers.HasFlag(RawInputModifiers.LeftMouseButton));
		Assert.False(pen.InContact);
	}

	[Fact]
	public void A_press_takes_the_pressure_reported_just_before_it_or_avalonias_default() {
		// The iPad plugin sends a motion with the touch's pressure, then the press (Godot's button events have none).
		var pen = new PenTracker();
		Feed(pen, new PenSample(PenPhase.Move, new Point(1, 1), 0.42f));
		Assert.Equal(0.42f, Feed(pen, new PenSample(PenPhase.Down, new Point(1, 1)))!.Point.Pressure);
		Assert.Equal(0.42f, Feed(pen, new PenSample(PenPhase.Move, new Point(2, 2)))!.Point.Pressure);   // a move without pressure keeps it
		Feed(pen, new PenSample(PenPhase.Up, new Point(2, 2)));

		Assert.Equal(0.5f, Feed(pen, new PenSample(PenPhase.Down, new Point(3, 3)))!.Point.Pressure);   // nothing reported since the release
	}

	[Fact]
	public void Out_of_order_samples_never_reach_avalonia() {
		var pen = new PenTracker();
		Assert.Null(Feed(pen, new PenSample(PenPhase.Up, new Point(0, 0))));       // a release without a press
		Assert.Null(Feed(pen, new PenSample(PenPhase.Leave, new Point(-1, -1))));  // leaving without hovering

		Feed(pen, new PenSample(PenPhase.Down, new Point(0, 0), 0.5f));
		Assert.Equal(RawPointerEventType.Move, Feed(pen, new PenSample(PenPhase.Down, new Point(1, 0), 0.6f))!.Type);   // a second press
		Assert.Null(Feed(pen, new PenSample(PenPhase.Leave, new Point(-1, -1))));  // the pen cannot leave while it touches
		Assert.True(pen.InContact);
	}

	[Fact]
	public void Hover_ends_with_a_leave_once() {
		var pen = new PenTracker();
		Feed(pen, new PenSample(PenPhase.Move, new Point(5, 5)));
		var leave = Feed(pen, new PenSample(PenPhase.Leave, new Point(-1, -1)))!;
		Assert.Equal(RawPointerEventType.LeaveWindow, leave.Type);
		Assert.False(pen.Hovering);
		Assert.Null(Feed(pen, new PenSample(PenPhase.Leave, new Point(-1, -1))));
	}

	[Fact]
	public void The_eraser_end_the_barrel_button_and_the_keys_are_carried_but_not_mouse_buttons() {
		var pen = new PenTracker();
		var args = Feed(pen, new PenSample(PenPhase.Down, new Point(0, 0), 0.5f, Inverted: true, Barrel: true,
			Keys: RawInputModifiers.Shift | RawInputModifiers.RightMouseButton))!;
		Assert.True(args.InputModifiers.HasFlag(RawInputModifiers.PenInverted));
		Assert.True(args.InputModifiers.HasFlag(RawInputModifiers.PenBarrelButton));
		Assert.True(args.InputModifiers.HasFlag(RawInputModifiers.Shift));
		Assert.False(args.InputModifiers.HasFlag(RawInputModifiers.RightMouseButton));   // only the tip is a button
	}

	[Fact]
	public void Samples_between_frames_reach_avalonia_as_intermediate_points() {
		// The Apple Pencil samples at 240 Hz; the samples between two frames come with the frame's move.
		var pen = new PenTracker();
		Feed(pen, new PenSample(PenPhase.Down, new Point(0, 0), 0.5f));
		var between = new[] { new PenSample(PenPhase.Move, new Point(1, 0), 0.55f), new PenSample(PenPhase.Move, new Point(2, 0)) };
		var args = Feed(pen, new PenSample(PenPhase.Move, new Point(3, 0), 0.7f), between)!;
		var points = args.IntermediatePoints!.Value!;
		Assert.Equal([new Point(1, 0), new Point(2, 0)], points.Select(p => p.Position));
		Assert.Equal([0.55f, 0.7f], points.Select(p => p.Pressure));   // a sample without pressure takes the frame's
	}

	[Theory]
	[InlineData("double_tap", PencilGesture.DoubleTap)]
	[InlineData("squeeze", PencilGesture.Squeeze)]
	public void The_plugins_gesture_names_are_read(string text, PencilGesture gesture)
		=> Assert.Equal(gesture, NativePencil.ParseGesture(text));

	[Fact]
	public void Unknown_gesture_names_and_phases_are_ignored() {
		Assert.Null(NativePencil.ParseGesture("barrel_roll"));
		Assert.Null(NativePencil.ParsePhase("paused"));
		Assert.Equal(PencilGesturePhase.Cancelled, NativePencil.ParsePhase("cancelled"));
	}

	[Fact]
	public void Gestures_reach_whoever_listens() {
		var seen = new List<PencilInteraction>();
		void Listen(PencilInteraction i) => seen.Add(i);
		GodotPen.Interaction += Listen;
		try {
			GodotPen.RaiseInteraction(new PencilInteraction(PencilGesture.DoubleTap, PencilGesturePhase.Ended, "switchEraser"));
		}
		finally {
			GodotPen.Interaction -= Listen;
		}

		Assert.Equal([new PencilInteraction(PencilGesture.DoubleTap, PencilGesturePhase.Ended, "switchEraser")], seen);
	}

}
