using System;
using Godot;

namespace JLeb.Estragonia.Input;

/// <summary>
/// The C# end of Estragonia's iPad plugin (<c>native/ios/EstragoniaPencil</c>, a Godot iOS plugin registered as the
/// <c>EstragoniaPencil</c> engine singleton). The plugin feeds the Apple Pencil's strokes and hover into Godot itself, as
/// mouse events of <see cref="GodotPen.PencilDeviceId"/>; what is not a pointer event comes through its signals:
/// <c>interaction(gesture, phase, preferred_action)</c> → <see cref="GodotPen.Interaction"/>, <c>hover_ended()</c> →
/// <see cref="GodotPen.EndHover"/>. Without the plugin (every other platform, or an iPad app that does not ship it) this
/// does nothing.
/// </summary>
internal static class NativePencil {

	public const string SingletonName = "EstragoniaPencil";

	private static GodotObject? s_plugin;

	/// <summary>Connects once, on Godot's main thread, when the first Avalonia control starts.</summary>
	public static void Connect() {
		if (s_plugin is not null || !Engine.HasSingleton(SingletonName))
			return;

		var plugin = Engine.GetSingleton(SingletonName);
		plugin.Connect("interaction", Callable.From<string, string, string>(OnInteraction));
		plugin.Connect("hover_ended", Callable.From(GodotPen.EndHover));
		s_plugin = plugin;
		SetCapture(GodotPen.Enabled);
	}

	/// <summary>Whether the plugin takes the Pencil away from Godot's touch input (false: the Pencil is a finger again).</summary>
	public static void SetCapture(bool capture)
		=> s_plugin?.Call("set_capture", capture);

	private static void OnInteraction(string gesture, string phase, string preferredAction) {
		if (ParseGesture(gesture) is { } g && ParsePhase(phase) is { } p)
			GodotPen.RaiseInteraction(new PencilInteraction(g, p, preferredAction));
	}

	internal static PencilGesture? ParseGesture(string text) => text switch {
		"double_tap" => PencilGesture.DoubleTap,
		"squeeze" => PencilGesture.Squeeze,
		_ => null
	};

	internal static PencilGesturePhase? ParsePhase(string text) => text switch {
		"began" => PencilGesturePhase.Began,
		"changed" => PencilGesturePhase.Changed,
		"ended" => PencilGesturePhase.Ended,
		"cancelled" => PencilGesturePhase.Cancelled,
		_ => null
	};

}
