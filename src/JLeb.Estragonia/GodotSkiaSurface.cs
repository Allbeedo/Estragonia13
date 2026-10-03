using System;
using Avalonia.Platform.Surfaces;
using Avalonia.Skia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>Encapsulates a Skia surface along with the Godot texture it comes from.</summary>
internal sealed class GodotSkiaSurface : ISkiaSurface, IGodotRenderSurface {

	public SKSurface SkSurface { get; }

	public Texture2Drd GdTexture { get; }

	Texture2D IGodotRenderSurface.GdTexture
		=> GdTexture;

	public RenderingDevice RenderingDevice { get; }

	public double RenderScaling { get; set; }

	public GodotSurfaceSync Sync { get; }

	public ulong DrawCount { get; set; }

	public bool IsDisposed { get; private set; }

	bool IPlatformRenderSurface.IsReady
		=> !IsDisposed;

	SKSurface ISkiaSurface.Surface
		=> SkSurface;

	bool ISkiaSurface.CanBlit
		=> false;

	public GodotSkiaSurface(
		SKSurface skSurface,
		Texture2Drd gdTexture,
		RenderingDevice renderingDevice,
		double renderScaling,
		GodotSurfaceSync sync
	) {
		SkSurface = skSurface;
		GdTexture = gdTexture;
		RenderingDevice = renderingDevice;
		RenderScaling = renderScaling;
		Sync = sync;
		IsDisposed = false;
	}

	void ISkiaSurface.Blit(SKCanvas canvas)
		=> throw new NotSupportedException();

	public void Dispose() {
		if (IsDisposed)
			return;

		IsDisposed = true;
		SkSurface.Dispose();
		Sync.WaitIdle();
		RenderingDevice.FreeRid(GdTexture.TextureRdRid);
		GdTexture.Dispose();
	}

}
