extends Node

# CI-only autoload, injected into samples/HelloWorld by .github/workflows/apple-test.yml:
# takes screenshots of the running sample, clicks the settings tab, resizes the window, then quits.
# Screenshots go to $SHOT_DIR/<$SHOT_NAME>-<step>.png.

var _start := 0

func _ready() -> void:
	_start = Time.get_ticks_msec()
	print("CI: driver=", RenderingServer.get_current_rendering_driver_name(),
		" method=", RenderingServer.get_current_rendering_method(),
		" adapter=", RenderingServer.get_video_adapter_name())

func _shot(step: String) -> void:
	var path := "%s/%s-%s.png" % [OS.get_environment("SHOT_DIR"), OS.get_environment("SHOT_NAME"), step]
	get_viewport().get_texture().get_image().save_png(path)
	print("CI: saved ", path, " fps=", Engine.get_frames_per_second())

func _click(pos: Vector2, pressed: bool) -> void:
	var e := InputEventMouseButton.new()
	e.button_index = MOUSE_BUTTON_LEFT
	e.pressed = pressed
	e.position = pos
	e.global_position = pos
	Input.parse_input_event(e)

func _once(key: String) -> bool:
	if has_meta(key):
		return false
	set_meta(key, true)
	return true

func _process(_delta: float) -> void:
	var t := Time.get_ticks_msec() - _start
	if t > 3000 and _once("start"):
		_shot("1-start")
	if t > 3200 and _once("down"):
		_click(Vector2(132, 89), true)
	if t > 3400 and _once("up"):
		_click(Vector2(132, 89), false)
	if t > 5000 and _once("settings"):
		_shot("2-settings-tab")
		get_window().size = Vector2i(800, 560)
	if t > 7000 and _once("resized"):
		_shot("3-resized")
		get_tree().quit()
