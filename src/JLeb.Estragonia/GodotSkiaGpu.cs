using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>
/// Bridges a Godot <see cref="RenderingDevice"/> with a Skia context used by Avalonia.
/// Derived classes provide the graphics API specific parts (<see cref="GodotVkSkiaGpu"/>, <see cref="GodotMetalSkiaGpu"/>).
/// </summary>
internal abstract class GodotSkiaGpu : ISkiaGpu {

	protected GodotSkiaGpu(RenderingDevice renderingDevice) {
		RenderingDevice = renderingDevice;
	}

	protected RenderingDevice RenderingDevice { get; }

	protected abstract GRContext GrContext { get; }

	public bool IsLost
		=> GrContext.IsAbandoned;

	IPlatformGraphicsContext? ISkiaGpu.PlatformGraphicsContext
		=> null;

	/// <summary>Creates the Godot texture shared with Skia, with usage bits suitable for every backend.</summary>
	protected Rid CreateSharedTexture(PixelSize size) {
		var gdRdTextureFormat = new RDTextureFormat {
			Format = RenderingDevice.DataFormat.R8G8B8A8Unorm,
			TextureType = RenderingDevice.TextureType.Type2D,
			Width = (uint) size.Width,
			Height = (uint) size.Height,
			Depth = 1,
			ArrayLayers = 1,
			Mipmaps = 1,
			Samples = RenderingDevice.TextureSamples.Samples1,
			UsageBits = RenderingDevice.TextureUsageBits.SamplingBit
				| RenderingDevice.TextureUsageBits.CanCopyFromBit
				| RenderingDevice.TextureUsageBits.CanCopyToBit
				| RenderingDevice.TextureUsageBits.ColorAttachmentBit
		};

		var gdRdTexture = RenderingDevice.TextureCreate(gdRdTextureFormat, new RDTextureView());
		if (!gdRdTexture.IsValid)
			throw new InvalidOperationException($"Couldn't create a {size.Width}x{size.Height} Godot texture");

		return gdRdTexture;
	}

	public GodotSkiaSurface CreateSurface(PixelSize size, double renderScaling)
		=> CreateSurfaceCore(new PixelSize(Math.Max(size.Width, 1), Math.Max(size.Height, 1)), renderScaling);

	protected abstract GodotSkiaSurface CreateSurfaceCore(PixelSize size, double renderScaling);

	object? IOptionalFeatureProvider.TryGetFeature(Type featureType)
		=> null;

	IDisposable IPlatformGraphicsContext.EnsureCurrent()
		=> EmptyDisposable.Instance;

	bool ISkiaGpu.IsReadyToCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
		=> surfaces.OfType<GodotSkiaSurface>().Any(surface => !surface.IsDisposed);

	ISkiaGpuRenderTarget? ISkiaGpu.TryCreateRenderTarget(IEnumerable<IPlatformRenderSurface> surfaces)
		=> surfaces.OfType<GodotSkiaSurface>().FirstOrDefault(surface => !surface.IsDisposed) is { } surface
			? new GodotSkiaRenderTarget(surface, GrContext)
			: null;

	IScopedResource<GRContext>? ISkiaGpu.TryGetGrContext()
		=> ScopedResource<GRContext>.Create(GrContext, static () => { });

	ISkiaSurface? ISkiaGpu.TryCreateSurface(PixelSize size, ISkiaGpuRenderSession? session)
		=> session is GodotSkiaGpuRenderSession godotSession
			? CreateSurface(size, godotSession.Surface.RenderScaling)
			: null;

	public abstract void Dispose();

}
