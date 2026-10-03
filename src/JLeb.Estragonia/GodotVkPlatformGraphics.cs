using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>
/// Godot <see cref="RenderingDevice"/>-based <see cref="IPlatformGraphics"/> implementation.
/// Despite its name (kept for compatibility), it supports both the Vulkan and Metal rendering drivers.
/// </summary>
public sealed class GodotVkPlatformGraphics : IPlatformGraphics, IDisposable {

	private GodotSkiaGpu? _context;
	private int _refCount;

	bool IPlatformGraphics.UsesSharedContext
		=> true;

	internal GodotSkiaGpu GetSharedContext() {
		if (Volatile.Read(ref _refCount) == 0)
			ThrowDisposed();

		if (_context is null || _context.IsLost) {
			_context?.Dispose();
			_context = null;
			_context = CreateSkiaGpu();
		}

		return _context;
	}

	private static GodotSkiaGpu CreateSkiaGpu() {
		var driverName = RenderingServer.GetCurrentRenderingDriverName();

		if (RenderingServer.GetRenderingDevice() is not { } renderingDevice)
			throw new NotSupportedException(
				$"Estragonia requires the Forward+ or Mobile renderer (current rendering driver: '{driverName}')"
			);

		return driverName switch {
			"metal" => new GodotMetalSkiaGpu(renderingDevice),
			// SkiaSharp's Apple native libraries are built without Vulkan, so MoltenVK can't be used.
			"vulkan" when MetalInterop.IsApplePlatform => throw new PlatformNotSupportedException(
				"Estragonia only supports the Metal rendering driver on Apple platforms: "
				+ "set rendering/rendering_device/driver.macos (and driver.ios) to metal, which is Godot's default"
			),
			"vulkan" => new GodotVkSkiaGpu(renderingDevice),
			_ => throw new NotSupportedException(
				$"Estragonia doesn't support Godot's '{driverName}' rendering driver. Supported drivers: vulkan, metal"
			)
		};
	}

	[DoesNotReturn]
	[MethodImpl(MethodImplOptions.NoInlining)]
	private static void ThrowDisposed()
		=> throw new ObjectDisposedException(nameof(GodotVkPlatformGraphics));

	IPlatformGraphicsContext IPlatformGraphics.CreateContext()
		=> throw new NotSupportedException();

	IPlatformGraphicsContext IPlatformGraphics.GetSharedContext()
		=> GetSharedContext();

	public void AddRef()
		=> Interlocked.Increment(ref _refCount);

	public void Release() {
		if (Interlocked.Decrement(ref _refCount) == 0)
			Dispose();
	}


	public void Dispose() {
		if (_context is not null) {
			_context.Dispose();
			_context = null;
		}
	}
}
