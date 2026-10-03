extends Node

# CI-only autoload, injected into samples/HelloWorld by .github/workflows/apple-test.yml:
# takes screenshots of the running sample, clicks the settings tab, resizes the window, then quits.
# Screenshots go to $SHOT_DIR/<$SHOT_NAME>-<step>.png.
# Each step waits for both some time and some rendered frames, so slow (software-rendered) machines still
# show the result of the previous step.

const STEP_MS := 1500
const STEP_FRAMES := 10

var _step := 0
var _step_start_ms := 0
var _step_start_frame := 0

func _ready() -> void:
	_step_start_ms = Time.get_ticks_msec()
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

func _process(_delta: float) -> void:
	var frame := Engine.get_process_frames()
	if Time.get_ticks_msec() - _step_start_ms < STEP_MS or frame - _step_start_frame < STEP_FRAMES:
		return
	_step_start_ms = Time.get_ticks_msec()
	_step_start_frame = frame
	_step += 1
	match _step:
		1:
			_shot("1-start")
			_click(Vector2(132, 89), true)
		2:
			_click(Vector2(132, 89), false)
		3:
			_shot("2-settings-tab")
			get_window().size = Vector2i(800, 560)
		4:
			_shot("3-resized")
			get_tree().quit()
