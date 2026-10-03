using System;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Threading;
using Avalonia;
using Avalonia.Platform;
using Godot;

namespace JLeb.Estragonia;

/// <summary>
/// Godot <see cref="IPlatformGraphics"/> implementation.
/// Despite its name (kept for compatibility), it renders on the GPU with the Vulkan and Metal rendering drivers,
/// and falls back to CPU rendering (<see cref="GodotSoftwareSurface"/>) for any other driver.
/// </summary>
public sealed class GodotVkPlatformGraphics : IPlatformGraphicsWithFeatures, IPlatformGraphicsReadyStateFeature, IDisposable {

	private GodotSkiaGpu? _context;
	private bool? _isSoftware;
	private int _refCount;

	bool IPlatformGraphics.UsesSharedContext
		=> true;

	bool IPlatformGraphicsReadyStateFeature.IsReady
		=> true;

	// When false, Avalonia doesn't request a GPU context and renders into our framebuffer surfaces instead.
	bool IPlatformGraphicsReadyStateFeature.UsesContexts
		=> !IsSoftware;

	/// <summary>Gets whether Avalonia is rendered on the CPU because no GPU backend supports Godot's rendering driver.</summary>
	public bool IsSoftware {
		get {
			if (_isSoftware is null) {
				_context = TryCreateSkiaGpu();
				_isSoftware = _context is null;
			}

			return _isSoftware.Value;
		}
	}

	internal GodotSkiaGpu GetSharedContext() {
		if (Volatile.Read(ref _refCount) == 0)
			ThrowDisposed();

		if (IsSoftware)
			throw new InvalidOperationException("Estragonia is rendering on the CPU, there's no GPU context");

		if (_context is null || _context.IsLost) {
			_context?.Dispose();
			_context = null;
			_context = CreateSkiaGpu() ?? throw new InvalidOperationException("Couldn't recreate the GPU context");
		}

		return _context;
	}

	private static GodotSkiaGpu? TryCreateSkiaGpu() {
		try {
			var gpu = CreateSkiaGpu();
			if (gpu is not null)
				GD.Print($"Estragonia is rendering on the GPU with {gpu.Description}");
			return gpu;
		}
		catch (Exception ex) {
			GD.PushError($"Estragonia couldn't initialize GPU rendering, falling back to CPU rendering: {ex}");
			return null;
		}
	}

	/// <summary>Creates the GPU backend matching Godot's rendering driver, or returns null if there's none.</summary>
	private static GodotSkiaGpu? CreateSkiaGpu() {
		var driverName = RenderingServer.GetCurrentRenderingDriverName();
		var renderingDevice = RenderingServer.GetRenderingDevice();

		// SkiaSharp's Apple native libraries are built without Vulkan: on Apple platforms, Godot's Vulkan driver runs on MoltenVK,
		// and Skia draws with Metal into the Metal textures behind Godot's Vulkan images instead.
		if (renderingDevice is not null && driverName == "vulkan" && MetalInterop.IsApplePlatform
			&& GodotMetalSkiaGpu.CreateForMoltenVK(renderingDevice) is { } moltenVKGpu)
			return moltenVKGpu;

		var fastPathHint = driverName switch {
			_ when renderingDevice is null
				=> "GPU rendering requires the Forward+ or Mobile renderer",
			"metal"
				=> null,
			"vulkan" when MetalInterop.IsApplePlatform
				=> "MoltenVK's Metal interop functions weren't found in the running program",
			"vulkan"
				=> null,
			"d3d12"
				=> "set rendering/rendering_device/driver.windows to vulkan for GPU rendering (d3d12 is Godot's default on Windows since 4.6)",
			_
				=> "GPU rendering supports the vulkan and metal drivers only"
		};

		if (fastPathHint is not null) {
			GD.PushWarning($"Estragonia is rendering on the CPU with Godot's '{driverName}' rendering driver, which is slower: {fastPathHint}");
			return null;
		}

		return driverName == "metal"
			? GodotMetalSkiaGpu.CreateForMetalDriver(renderingDevice!)
			: new GodotVkSkiaGpu(renderingDevice!);
	}

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType)
		=> featureType == typeof(IPlatformGraphicsReadyStateFeature) ? this : null;

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
