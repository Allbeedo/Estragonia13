# EstragoniaPencil — Apple Pencil on iPad for Estragonia

Godot's iOS port reports the Apple Pencil as just another touch: it never says which touch is the Pencil, and Estragonia
could only hand it to Avalonia as a finger. This GDExtension fixes that on iPad, inside Estragonia's stack (Avalonia and
Godot are not changed):

| What | How |
|---|---|
| Pencil vs finger | A gesture recognizer that only takes Pencil touches (`allowedTouchTypes`) and takes them before Godot's view does (`delaysTouchesBegan`); fingers reach Godot as before |
| Strokes | Fed back into Godot as mouse events of device `0x4550454E` (`GodotPen.PencilDeviceId` on the C# side), so Godot still routes each one to the control under it, in its coordinates; Estragonia turns them into an Avalonia **pen** (`PointerType.Pen`) |
| Pressure | `force / maximumPossibleForce` |
| Tilt | From altitude and azimuth, as the W3C Pointer Events specification converts them; Avalonia's `XTilt`/`YTilt` in degrees |
| 240 Hz samples | Every coalesced touch since the last frame is its own motion (turn off Godot's `Input.use_accumulated_input` to keep them all) |
| Hover (iPadOS 16.1+, M2 and later iPads) | `UIHoverGestureRecognizer` with a height above the screen (`zOffset`, 16.4+): pen moves without contact; the end of hover is the `hover_ended` signal |
| Double-tap, squeeze (Pencil Pro) | `UIPencilInteraction`: the `interaction(gesture, phase, preferred_action)` signal → `GodotPen.Interaction` in C#, with the action the person chose in Settings |
| Turning it off | `GodotPen.Enabled = false` → `set_capture(false)`: the Pencil is a finger again |

The C# side is already in Estragonia (`src/JLeb.Estragonia/Input/GodotPen.cs`, `PenTracker.cs`, `NativePencil.cs`) and is
tested (`tests/JLeb.Estragonia.Tests`). It connects to this plugin by itself when the `EstragoniaPencil` engine singleton
exists; without the plugin nothing changes.

## Status

**Written, not built or run.** It was written without a Mac. Before relying on it:

1. Build it (below) against the godot-cpp branch of the app's Godot version.
2. Run Estragonia's HelloWorld sample on an iPad with a Pencil: the pen line in its corner should show `Pen`, pressure and
   tilt changing, `hover` before the tip touches, and fingers still scrolling as fingers.
3. Check the events Godot itself sends for the Pencil when the plugin is off (pressure, tilt) — for the record.

## Building (on a Mac with Xcode)

```sh
git clone -b <branch matching the app's Godot> https://github.com/godotengine/godot-cpp
scons godot_cpp=godot-cpp platform=ios arch=arm64 target=template_debug
scons godot_cpp=godot-cpp platform=ios arch=arm64 target=template_release
scons godot_cpp=godot-cpp platform=ios arch=universal ios_simulator=yes target=template_debug   # simulator

# One .xcframework per target, with the device and simulator libraries (and godot-cpp's static library beside it):
xcodebuild -create-xcframework \
  -library bin/libestragonia_pencil.ios.template_debug.a \
  -library bin/libestragonia_pencil.ios.template_debug.simulator.a \
  -output bin/libestragonia_pencil.ios.template_debug.xcframework
```

Copy `estragonia_pencil.gdextension` and `bin/` to `res://addons/estragonia_pencil/` in the Godot project and export
for iOS as usual. The C# side needs nothing more.

## Why mouse events of a reserved device

Godot routes mouse events through its GUI exactly as it routes the Pencil's position on screen: to the control under it,
converted to that control's coordinates, with capture while pressed. Estragonia's controls receive them in `_GuiInput` as
always and `GodotPen.IsPen` recognizes the device — no second path, no hit-testing of our own, and the same C# code
serves Windows pens (where Godot already reports a pen as a mouse with pressure).
