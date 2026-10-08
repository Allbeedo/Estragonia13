using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Godot;
using JLeb.Estragonia.Input;
using AvCompositor = Avalonia.Rendering.Composition.Compositor;
using AvKey = Avalonia.Input.Key;
using GdCursorShape = Godot.Control.CursorShape;
using GdMouseButton = Godot.MouseButton;

namespace JLeb.Estragonia;

/// <summary>Implementation of Avalonia <see cref="ITopLevelImpl"/> that renders to a Godot texture.</summary>
public sealed class GodotTopLevelImpl : ITopLevelImpl {

	private readonly GodotVkPlatformGraphics _platformGraphics;
	private readonly IClipboard _clipboard;
	private readonly TouchDevice _touchDevice = new();
	private readonly Dictionary<int, PenTracker> _pens = new();

	private GodotSkiaSurface? _surface;
	private WindowTransparencyLevel _transparencyLevel = WindowTransparencyLevel.Transparent;
	private PixelSize _renderSize;
	private IInputRoot? _inputRoot;
	private GdCursorShape _cursorShape;
	private bool _isDisposed;
	private int _lastMouseDeviceId = GodotDevices.EmulatedDeviceId;

	public double RenderScaling { get; private set; } = 1.0;

	double ITopLevelImpl.DesktopScaling
		=> 1.0;

	IPlatformHandle? ITopLevelImpl.Handle
		=> null;

	public AvCompositor Compositor { get; }

	public Size ClientSize { get; private set; }

	public WindowTransparencyLevel TransparencyLevel {
		get => _transparencyLevel;
		private set {
			if (_transparencyLevel.Equals(value))
				return;

			_transparencyLevel = value;
			TransparencyLevelChanged?.Invoke(value);
		}
	}

	public Action<Rect>? Paint { get; set; }

	public Action<Size, WindowResizeReason>? Resized { get; set; }

	public Action? Closed { get; set; }

	public Action<RawInputEventArgs>? Input { get; set; }

	public Action? LostFocus { get;set; }

	public Action<GdCursorShape>? CursorChanged { get; set; }

	public Action<double>? ScalingChanged { get; set; }

	public Action<WindowTransparencyLevel>? TransparencyLevelChanged { get; set; }

	IPlatformRenderSurface[] ITopLevelImpl.Surfaces
		=> new IPlatformRenderSurface[] { GetOrCreateSurface() };

	AcrylicPlatformCompensationLevels ITopLevelImpl.AcrylicCompensationLevels
		=> new(1.0, 1.0, 1.0);

	public GodotTopLevelImpl(GodotVkPlatformGraphics platformGraphics, IClipboard clipboard, AvCompositor compositor) {
		_platformGraphics = platformGraphics;
		_clipboard = clipboard;
		Compositor = compositor;

		platformGraphics.AddRef();
	}

	private GodotSkiaSurface CreateSurface() {
		if (_isDisposed)
			throw new ObjectDisposedException(nameof(GodotTopLevelImpl));

		return _platformGraphics.GetSharedContext().CreateSurface(_renderSize, RenderScaling);
	}

	internal GodotSkiaSurface? TryGetSurface()
		=> _surface;

	internal GodotSkiaSurface GetOrCreateSurface()
		=> _surface ??= CreateSurface();

	/// <summary>Godot texture that Avalonia renders into (for the project-side host <c>_Draw</c>).</summary>
	public Texture2Drd GetGdTexture()
		=> GetOrCreateSurface().GdTexture;

	/// <summary>How many times the current surface has been drawn (used to detect post-resize redraws).</summary>
	public ulong SurfaceDrawCount
		=> TryGetSurface()?.DrawCount ?? 0;

	[SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "Doesn't affect correctness")]
	public void SetRenderSize(PixelSize renderSize, double renderScaling) {
		var hasScalingChanged = RenderScaling != renderScaling;
		if (_renderSize == renderSize && !hasScalingChanged)
			return;

		var oldClientSize = ClientSize;
		var unclampedClientSize = renderSize.ToSize(renderScaling);

		ClientSize = new Size(Math.Max(unclampedClientSize.Width, 0.0), Math.Max(unclampedClientSize.Height, 0.0));
		RenderScaling = renderScaling;

		if (_renderSize != renderSize) {
			_renderSize = renderSize;

			if (_surface is not null) {
				_surface.Dispose();
				_surface = null;
			}

			if (_isDisposed)
				return;

			_surface = CreateSurface();
		}

		if (hasScalingChanged) {
			if (_surface != null)
				_surface.RenderScaling = RenderScaling;
			ScalingChanged?.Invoke(RenderScaling);
		}

		if (oldClientSize != ClientSize)
			Resized?.Invoke(ClientSize, hasScalingChanged ? WindowResizeReason.DpiChange : WindowResizeReason.Unspecified);
	}

	public void OnDraw(Rect rect)
		=> Paint?.Invoke(rect);

	public bool OnMouseMotion(InputEventMouseMotion inputEvent, ulong timestamp) {
		if (GodotPen.IsPen(inputEvent))
			return OnPen(inputEvent, PenPhase.Move, timestamp);

		_lastMouseDeviceId = inputEvent.Device;

		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawPointerEventArgs(
			GodotDevices.GetMouse(inputEvent.Device),
			timestamp,
			_inputRoot,
			RawPointerEventType.Move,
			CreateRawPointerPoint(inputEvent.Position, inputEvent.Pressure, inputEvent.Tilt),
			inputEvent.GetRawInputModifiers()
		);

		input(args);

		return args.Handled;
	}

	public bool OnMouseButton(InputEventMouseButton inputEvent, ulong timestamp) {
		if (inputEvent.ButtonIndex == GdMouseButton.Left && GodotPen.IsPen(inputEvent))
			return OnPen(inputEvent, inputEvent.Pressed ? PenPhase.Down : PenPhase.Up, timestamp);

		_lastMouseDeviceId = inputEvent.Device;

		if (_inputRoot is null || Input is not { } input)
			return false;

		RawPointerEventArgs CreateButtonArgs(RawPointerEventType type)
			=> new(
				GodotDevices.GetMouse(inputEvent.Device),
				timestamp,
				_inputRoot,
				type,
				inputEvent.Position.ToAvaloniaPoint() / RenderScaling,
				inputEvent.GetRawInputModifiers()
			);

		RawMouseWheelEventArgs CreateWheelArgs(Vector delta)
			=> new(
				GodotDevices.GetMouse(inputEvent.Device),
				timestamp,
				_inputRoot,
				inputEvent.Position.ToAvaloniaPoint() / RenderScaling,
				delta,
				inputEvent.GetRawInputModifiers()
			);

		var args = (inputEvent.ButtonIndex, inputEvent.Pressed) switch {
			(GdMouseButton.Left, true) => CreateButtonArgs(RawPointerEventType.LeftButtonDown),
			(GdMouseButton.Left, false) => CreateButtonArgs(RawPointerEventType.LeftButtonUp),
			(GdMouseButton.Right, true) => CreateButtonArgs(RawPointerEventType.RightButtonDown),
			(GdMouseButton.Right, false) => CreateButtonArgs(RawPointerEventType.RightButtonUp),
			(GdMouseButton.Middle, true) => CreateButtonArgs(RawPointerEventType.MiddleButtonDown),
			(GdMouseButton.Middle, false) => CreateButtonArgs(RawPointerEventType.MiddleButtonUp),
			(GdMouseButton.Xbutton1, true) => CreateButtonArgs(RawPointerEventType.XButton1Down),
			(GdMouseButton.Xbutton1, false) => CreateButtonArgs(RawPointerEventType.XButton1Up),
			(GdMouseButton.Xbutton2, true) => CreateButtonArgs(RawPointerEventType.XButton2Down),
			(GdMouseButton.Xbutton2, false) => CreateButtonArgs(RawPointerEventType.XButton2Up),
			(GdMouseButton.WheelUp, _) => CreateWheelArgs(new Vector(0.0, inputEvent.Factor)),
			(GdMouseButton.WheelDown, _) => CreateWheelArgs(new Vector(0.0, -inputEvent.Factor)),
			(GdMouseButton.WheelLeft, _) => CreateWheelArgs(new Vector(inputEvent.Factor, 0.0)),
			(GdMouseButton.WheelRight, _) => CreateWheelArgs(new Vector(-inputEvent.Factor, 0.0)),
			_ => null
		};

		if (args is null)
			return false;

		input(args);

		return args.Handled;
	}

	public bool OnScreenTouch(InputEventScreenTouch inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawTouchEventArgs(
			_touchDevice,
			timestamp,
			_inputRoot,
			inputEvent.Pressed ? RawPointerEventType.TouchBegin : RawPointerEventType.TouchEnd,
			inputEvent.Position.ToAvaloniaPoint() / RenderScaling,
			InputModifiersProvider.GetRawInputModifiers(),
			inputEvent.Index
		);

		input(args);

		return args.Handled;
	}

	public bool OnScreenDrag(InputEventScreenDrag inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawTouchEventArgs(
			_touchDevice,
			timestamp,
			_inputRoot,
			RawPointerEventType.TouchUpdate,
			CreateRawPointerPoint(inputEvent.Position, inputEvent.Pressure, inputEvent.Tilt),
			inputEvent.GetRawInputModifiers(),
			inputEvent.Index
		);

		input(args);

		return args.Handled;
	}

	private RawPointerPoint CreateRawPointerPoint(Vector2 position, float pressure, Vector2 tilt)
		=> new() {
			Position = position.ToAvaloniaPoint() / RenderScaling,
			Twist = 0.0f,
			Pressure = pressure,
			XTilt = tilt.X * 90.0f,
			YTilt = tilt.Y * 90.0f
		};

	public bool OnKey(InputEventKey inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var keyCode = inputEvent.Keycode;
		var pressed = inputEvent.Pressed;
		var key = keyCode.ToAvaloniaKey();

		if (key != AvKey.None) {
			var args = new RawKeyEventArgs(
				GodotDevices.Keyboard,
				timestamp,
				_inputRoot,
				pressed ? RawKeyEventType.KeyDown : RawKeyEventType.KeyUp,
				key,
				inputEvent.GetRawInputModifiers(),
				inputEvent.PhysicalKeycode.ToAvaloniaPhysicalKey(),
				OS.GetKeycodeString(inputEvent.KeyLabel)
			);

			input(args);

			if (args.Handled)
				return true;
		}

		if (pressed && OS.IsKeycodeUnicode((long) keyCode)) {
			var text = Char.ConvertFromUtf32((int) inputEvent.Unicode);
			var args = new RawTextInputEventArgs(GodotDevices.Keyboard, timestamp, _inputRoot, text);

			input(args);

			if (args.Handled)
				return true;
		}

		return false;
	}

	public bool OnJoypadButton(InputEventJoypadButton inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawJoypadButtonEventArgs(
			GodotDevices.GetJoypad(inputEvent.Device),
			timestamp,
			_inputRoot,
			inputEvent.IsPressed() ? RawJoypadButtonEventType.ButtonDown : RawJoypadButtonEventType.ButtonUp,
			inputEvent.ButtonIndex
		);

		input(args);

		return args.Handled;
	}

	public bool OnJoypadMotion(InputEventJoypadMotion inputEvent, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawJoypadAxisEventArgs(
			GodotDevices.GetJoypad(inputEvent.Device),
			timestamp,
			_inputRoot,
			inputEvent.Axis,
			inputEvent.AxisValue
		);

		input(args);

		return args.Handled;
	}

	public void OnLostFocus()
		=> LostFocus?.Invoke();

	/// <summary>
	/// A pen's Godot mouse event as a pen (<see cref="GodotPen"/>): the tip down and up, moves touching or hovering, with
	/// pressure and tilt from the latest motion. Godot's button events carry neither, so a Down takes the pressure the pen
	/// last reported (the iPad plugin sends a motion at the touch point first).
	/// </summary>
	private bool OnPen(InputEventMouse inputEvent, PenPhase phase, ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		if (!_pens.TryGetValue(inputEvent.Device, out var pen))
			_pens[inputEvent.Device] = pen = new PenTracker();

		var (pressure, tilt, inverted) = inputEvent is InputEventMouseMotion motion
			? (motion.Pressure, motion.Tilt, motion.PenInverted)
			: (0f, Vector2.Zero, false);
		var sample = new PenSample(
			phase,
			inputEvent.Position.ToAvaloniaPoint() / RenderScaling,
			pressure,
			tilt.X * 90.0f,
			tilt.Y * 90.0f,
			Inverted: inverted,
			Barrel: (inputEvent.ButtonMask & MouseButtonMask.Right) != 0,
			Keys: inputEvent.GetRawInputModifiers()
		);

		var args = pen.Create(GodotDevices.GetPen(inputEvent.Device), timestamp, _inputRoot, sample);
		if (args is null)
			return false;

		if (pen.Hovering)
			GodotPen.Hovering(this);

		input(args);

		return args.Handled;
	}

	/// <summary>The pen stopped hovering over this top level (it moved away, or hover ended): Avalonia sees it leave.</summary>
	public bool OnPenLeave(ulong timestamp) {
		if (_inputRoot is null || Input is not { } input)
			return false;

		var handled = false;
		foreach (var (device, pen) in _pens) {
			if (pen.Create(GodotDevices.GetPen(device), timestamp, _inputRoot, new PenSample(PenPhase.Leave, new Point(-1, -1))) is { } args) {
				input(args);
				handled |= args.Handled;
			}
		}

		return handled;
	}

	public bool OnMouseExited(ulong timestamp) {
		OnPenLeave(timestamp);

		if (_inputRoot is null || Input is not { } input)
			return false;

		var args = new RawPointerEventArgs(
			GodotDevices.GetMouse(_lastMouseDeviceId),
			timestamp,
			_inputRoot,
			RawPointerEventType.LeaveWindow,
			new Point(-1, -1),
			InputModifiersProvider.GetRawInputModifiers()
		);

		input(args);

		return args.Handled;
	}

	void ITopLevelImpl.SetInputRoot(IInputRoot inputRoot)
		=> _inputRoot = inputRoot;

	Point ITopLevelImpl.PointToClient(PixelPoint point)
		=> point.ToPoint(RenderScaling);

	PixelPoint ITopLevelImpl.PointToScreen(Point point)
		=> PixelPoint.FromPoint(point, RenderScaling);

	void ITopLevelImpl.SetCursor(ICursorImpl? cursor) {
		var cursorShape = (cursor as GodotStandardCursorImpl)?.CursorShape ?? GdCursorShape.Arrow;
		if (_cursorShape == cursorShape)
			return;

		_cursorShape = cursorShape;
		CursorChanged?.Invoke(cursorShape);
	}

	IPopupImpl? ITopLevelImpl.CreatePopup()
		=> null;

	void ITopLevelImpl.SetTransparencyLevelHint(IReadOnlyList<WindowTransparencyLevel> transparencyLevels) {
		foreach (var transparencyLevel in transparencyLevels) {
			if (transparencyLevel == WindowTransparencyLevel.Transparent || transparencyLevel == WindowTransparencyLevel.None) {
				TransparencyLevel = transparencyLevel;
				return;
			}
		}
	}

	void ITopLevelImpl.SetFrameThemeVariant(PlatformThemeVariant themeVariant) {
	}

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType) {
		if (featureType == typeof(IClipboard))
			return _clipboard;

		return null;
	}

	public void Dispose() {
		if (_isDisposed)
			return;

		_isDisposed = true;

		if (_surface is not null) {
			_surface.Dispose();
			_surface = null;
		}

		Closed?.Invoke();

		_platformGraphics.Release();
	}

}
