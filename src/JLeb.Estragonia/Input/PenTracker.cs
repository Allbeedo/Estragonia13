using System.Collections.Generic;
using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Raw;

namespace JLeb.Estragonia.Input;

/// <summary>What a pen sample means: the tip touches, the pen moves (touching or hovering), the tip lifts, the pen leaves.</summary>
internal enum PenPhase {
	Move,
	Down,
	Up,
	Leave
}

/// <summary>
/// One pen sample in Avalonia's units: position in device-independent pixels of the top level, pressure 0–1, tilt in
/// degrees (−90…90, as Avalonia's <see cref="RawPointerPoint"/>), twist in degrees (Apple Pencil Pro's barrel roll).
/// </summary>
internal readonly record struct PenSample(
	PenPhase Phase,
	Point Position,
	float Pressure = 0f,
	float XTilt = 0f,
	float YTilt = 0f,
	float Twist = 0f,
	bool Inverted = false,
	bool Barrel = false,
	RawInputModifiers Keys = RawInputModifiers.None
);

/// <summary>
/// Turns pen samples into Avalonia raw pointer events for one pen (no Godot types, so it is tested on its own). It keeps
/// whether the tip touches: a move while touching carries <see cref="RawInputModifiers.LeftMouseButton"/> (Avalonia's
/// "in contact"), a move without it is hover; a second Down is a move, an Up without a Down and a Leave while touching are
/// dropped, so Avalonia always sees down–move–up in order.
/// </summary>
internal sealed class PenTracker {

	/// <summary>Avalonia's default pressure, used for a Down when no pressure was reported yet.</summary>
	private const float DefaultPressure = 0.5f;

	/// <summary>The last pressure the pen reported, touching or not (a pen may report it just before the tip-down).</summary>
	private float _pressure;

	/// <summary>The last tilt and twist a move reported: a press or release without them keeps them (Godot's button events carry none).</summary>
	private (float X, float Y, float Twist) _angles;

	public bool InContact { get; private set; }

	public bool Hovering { get; private set; }

	/// <summary>The event for <paramref name="sample"/>, or null when it means nothing new.</summary>
	public RawPointerEventArgs? Create(IInputDevice pen, ulong timestamp, IInputRoot root, in PenSample sample,
		IReadOnlyList<PenSample>? intermediate = null) {
		RawPointerEventType type;
		var pressure = sample.Pressure;
		if (pressure > 0f)
			_pressure = pressure;

		var point = sample;
		if (sample.Phase == PenPhase.Move || sample.XTilt != 0f || sample.YTilt != 0f || sample.Twist != 0f)
			_angles = (sample.XTilt, sample.YTilt, sample.Twist);
		else if (sample.Phase is PenPhase.Down or PenPhase.Up)
			point = sample with { XTilt = _angles.X, YTilt = _angles.Y, Twist = _angles.Twist };

		switch (sample.Phase) {
			case PenPhase.Down when !InContact:
				type = RawPointerEventType.LeftButtonDown;
				if (pressure <= 0f)
					pressure = _pressure > 0f ? _pressure : DefaultPressure;
				InContact = true;
				Hovering = false;
				break;
			case PenPhase.Down:
			case PenPhase.Move:
				type = RawPointerEventType.Move;
				if (InContact && pressure <= 0f)
					pressure = _pressure > 0f ? _pressure : DefaultPressure;
				else if (!InContact) {
					pressure = 0f;
					Hovering = true;
				}
				break;
			case PenPhase.Up when InContact:
				type = RawPointerEventType.LeftButtonUp;
				pressure = 0f;
				_pressure = 0f;
				InContact = false;
				break;
			case PenPhase.Leave when !InContact && Hovering:
				type = RawPointerEventType.LeaveWindow;
				pressure = 0f;
				Hovering = false;
				break;
			default:
				return null;
		}

		var modifiers = sample.Keys & RawInputModifiers.KeyboardMask;
		if (InContact)
			modifiers |= RawInputModifiers.LeftMouseButton;
		// As Windows Ink, which Avalonia follows: inverted = the eraser end points at the screen (hovering or not), eraser =
		// it touches.
		if (sample.Inverted)
			modifiers |= InContact ? RawInputModifiers.PenInverted | RawInputModifiers.PenEraser : RawInputModifiers.PenInverted;
		if (sample.Barrel)
			modifiers |= RawInputModifiers.PenBarrelButton;

		var args = new RawPointerEventArgs(pen, timestamp, root, type, Point(point, pressure), modifiers);
		if (intermediate is { Count: > 0 } && type == RawPointerEventType.Move) {
			var inContact = InContact;
			var last = pressure;
			var points = new RawPointerPoint[intermediate.Count];
			for (var i = 0; i < points.Length; i++) {
				var p = intermediate[i].Pressure;
				points[i] = Point(intermediate[i], inContact ? (p > 0f ? p : last) : 0f);
			}

			args.IntermediatePoints = new(points);
		}

		return args;
	}

	/// <summary>Forgets the pen's state (the top level lost its input root, the pen's stream was cancelled).</summary>
	public void Reset() {
		InContact = false;
		Hovering = false;
		_pressure = 0f;
		_angles = default;
	}

	private static RawPointerPoint Point(in PenSample sample, float pressure)
		=> new() {
			Position = sample.Position,
			Pressure = pressure,
			XTilt = sample.XTilt,
			YTilt = sample.YTilt,
			Twist = sample.Twist
		};

}
