using System;
using Godot;

namespace JLeb.Estragonia.Input;

/// <summary>A Pencil gesture that is not a stroke: Apple Pencil's double-tap and (Pencil Pro) squeeze.</summary>
public enum PencilGesture {
	DoubleTap,
	Squeeze
}

/// <summary>Where a squeeze is (a double-tap is always <see cref="Ended"/>).</summary>
public enum PencilGesturePhase {
	Began,
	Changed,
	Ended,
	Cancelled
}

/// <summary>
/// A Pencil gesture with the action the person chose for it in the system settings ("switchEraser", "switchPrevious",
/// "showColorPalette", "showInkAttributes", "showContextualPalette", "runSystemShortcut", "ignore"; empty when unknown).
/// What the app does with it is the app's choice — Apple asks apps to follow the preferred action where it applies.
/// </summary>
public readonly record struct PencilInteraction(PencilGesture Gesture, PencilGesturePhase Phase, string PreferredAction);

/// <summary>
/// Pen input in Estragonia: a pen reaches Avalonia as a pen (<see cref="Avalonia.Input.PointerType.Pen"/>, with pressure,
/// tilt, twist, hover and the eraser end), never as a mouse or a finger.
/// <para>
/// How a pen is told apart: Godot's mouse events with <see cref="PencilDeviceId"/> as their device are a pen. Estragonia's
/// iPad plugin (<c>native/ios/EstragoniaPencil</c>) feeds the Apple Pencil into Godot that way — Godot's own iOS input
/// never says which touch is the Pencil — so Godot still routes each event to the control under it, in its coordinates.
/// On other systems Godot reports a pen as a mouse that carries pressure and tilt but says nothing else; an app that
/// knows its pen sets <see cref="IsDesktopPen"/>.
/// </para>
/// </summary>
public static class GodotPen {

	/// <summary>The device of every Godot event that is a pen: chosen far from Godot's own device numbers.</summary>
	public const int PencilDeviceId = 0x45_50_45_4E;   // "EPEN"

	private static bool s_enabled = true;

	/// <summary>
	/// False sends pen events through the mouse path as before, and the iPad plugin leaves the Pencil to Godot's touch
	/// input (a finger). Defaults to true.
	/// </summary>
	public static bool Enabled {
		get => s_enabled;
		set {
			s_enabled = value;
			NativePencil.SetCapture(value);
		}
	}

	/// <summary>
	/// Optional: whether a Godot mouse event (Windows, macOS, Linux) is really a pen — for example <c>e =&gt; e is
	/// InputEventMouseMotion { Pressure: &gt; 0f and &lt; 1f }</c> on a tablet. Null (the default) leaves desktop events as
	/// mouse events.
	/// </summary>
	public static Func<InputEventMouse, bool>? IsDesktopPen { get; set; }

	/// <summary>Raised for Pencil gestures that are not strokes (double-tap, squeeze), on Godot's main thread.</summary>
	public static event Action<PencilInteraction>? Interaction;

	private static WeakReference<GodotTopLevelImpl>? s_hoverTarget;

	/// <summary>Whether <paramref name="inputEvent"/> is a pen's.</summary>
	public static bool IsPen(InputEventMouse inputEvent)
		=> Enabled && (inputEvent.Device == PencilDeviceId || (IsDesktopPen?.Invoke(inputEvent) ?? false));

	/// <summary>Reports a Pencil gesture (called by the iPad plugin's bridge; an app may call it too).</summary>
	public static void RaiseInteraction(PencilInteraction interaction)
		=> Interaction?.Invoke(interaction);

	/// <summary>The pen stopped hovering: the top level it hovered over is told it left.</summary>
	public static void EndHover() {
		if (s_hoverTarget is not null && s_hoverTarget.TryGetTarget(out var target))
			target.OnPenLeave(Time.GetTicksMsec());
		s_hoverTarget = null;
	}

	internal static void Hovering(GodotTopLevelImpl target) {
		if (s_hoverTarget is null || !s_hoverTarget.TryGetTarget(out var current) || !ReferenceEquals(current, target))
			s_hoverTarget = new WeakReference<GodotTopLevelImpl>(target);
	}

}
