// Estragonia's Apple Pencil plugin for iPad (GDExtension, godot-cpp). See ../README.md.
//
// It takes the Apple Pencil away from Godot's touch input — Godot's iOS port never says which touch is the Pencil — and
// feeds it back into Godot as mouse events of device ESTRAGONIA_PENCIL_DEVICE (the C# side's GodotPen.PencilDeviceId),
// with pressure and tilt, so Godot still routes every event to the control under it and Estragonia hands it to Avalonia
// as a pen. Hover (iPadOS 16.1+, M2 and later) comes the same way; double-tap and squeeze come as the "interaction"
// signal, the end of hover as "hover_ended". Fingers are never touched.

#pragma once

#include <godot_cpp/classes/object.hpp>
#include <godot_cpp/core/class_db.hpp>
#include <godot_cpp/variant/vector2.hpp>

namespace estragonia {

// Must equal JLeb.Estragonia.Input.GodotPen.PencilDeviceId ("EPEN").
constexpr int ESTRAGONIA_PENCIL_DEVICE = 0x4550454E;

class EstragoniaPencil : public godot::Object {
	GDCLASS(EstragoniaPencil, godot::Object)

public:
	static EstragoniaPencil *get_singleton();

	EstragoniaPencil();
	~EstragoniaPencil() override;

	// Whether the Pencil is taken from Godot's touch input (true by default; false: the Pencil is a finger again).
	void set_capture(bool capture);
	bool get_capture() const;

	// Called from the UIKit side (main thread). Positions are in Godot's window pixels; tilt in -1..1 as Godot's.
	void pencil_moved(godot::Vector2 position, float pressure, godot::Vector2 tilt, bool touching);
	void pencil_pressed(godot::Vector2 position, bool pressed);
	void hover_moved(godot::Vector2 position, godot::Vector2 tilt);
	void hover_ended();
	void interaction(const godot::String &gesture, const godot::String &phase, const godot::String &preferred_action);

protected:
	static void _bind_methods();

private:
	static EstragoniaPencil *singleton;
	void *ui = nullptr;   // the Objective-C side (EPPencilUI), retained through a bridged pointer
	bool capture = true;
	godot::Vector2 last_position;
};

} // namespace estragonia
