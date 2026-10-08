using System;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using JLeb.Estragonia.Input;

namespace HelloWorld.UI.Views;

/// <summary>
/// Shows what Estragonia's pen gives Avalonia (<see cref="GodotPen"/>): pointer type, contact, pressure, tilt, twist,
/// eraser, hover, and the Pencil's double-tap and squeeze. The first thing to run on an iPad with the EstragoniaPencil
/// plugin, or on a Windows tablet.
/// </summary>
public partial class PenPadView : UserControl {

	private Point? _last;

	public PenPadView() {
		InitializeComponent();
		Pad.PointerPressed += OnPointer;
		Pad.PointerMoved += OnPointer;
		Pad.PointerReleased += OnPointer;
		Pad.PointerExited += (_, _) => PointerText.Text = "Pointer left the pad.";
	}

	protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e) {
		base.OnAttachedToVisualTree(e);
		GodotPen.Interaction += OnInteraction;
	}

	protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e) {
		GodotPen.Interaction -= OnInteraction;
		base.OnDetachedFromVisualTree(e);
	}

	private void OnInteraction(PencilInteraction interaction)
		=> GestureText.Text = $"Pencil gesture: {interaction.Gesture} {interaction.Phase} (preferred: {interaction.PreferredAction})";

	private void OnPointer(object? sender, PointerEventArgs e) {
		var point = e.GetCurrentPoint(Pad);
		var p = point.Properties;
		var contact = p.IsLeftButtonPressed;
		var extras = (p.IsInverted ? "  eraser end" : "") + (p.IsBarrelButtonPressed ? "  barrel" : "");
		PointerText.Text = string.Create(CultureInfo.InvariantCulture,
			$"{e.Pointer.Type} {(contact ? "touching" : "hover")}  pressure {p.Pressure:0.00}  tilt {p.XTilt:0}°/{p.YTilt:0}°  twist {p.Twist:0}°{extras}");

		if (!contact) {
			_last = null;
			return;
		}

		// Each sample since the last event too (the Pencil samples faster than frames).
		foreach (var sample in e.GetIntermediatePoints(Pad)) {
			Draw(sample.Position, sample.Properties.Pressure, sample.Properties.IsInverted);
		}
	}

	private void Draw(Point to, float pressure, bool erase) {
		if (_last is { } from) {
			Ink.Children.Add(new Line {
				StartPoint = from,
				EndPoint = to,
				Stroke = erase ? Brushes.IndianRed : Brushes.White,
				StrokeThickness = 1 + 6 * Math.Clamp(pressure, 0f, 1f),
				StrokeLineCap = PenLineCap.Round
			});
		}

		_last = to;
	}

	private void OnClear(object? sender, RoutedEventArgs e) {
		Ink.Children.Clear();
		_last = null;
	}

}
