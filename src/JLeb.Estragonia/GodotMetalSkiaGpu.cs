using System;
using Avalonia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>
/// Bridges the Godot Metal renderer (macOS / iOS) with a Skia context used by Avalonia.
/// Skia gets Godot's own <c>id&lt;MTLDevice&gt;</c> and <c>id&lt;MTLCommandQueue&gt;</c>,
/// and draws directly into the <c>id&lt;MTLTexture&gt;</c> backing each Godot texture (zero-copy).
/// </summary>
/// <remarks>
/// Adapted from SkiaGameRendering's <c>MetalSkiaSurfaceFactory</c> / <c>MetalGodotBackend</c>
/// (MIT, https://github.com/vchelaru/SkiaGameRendering, see issue #92 / PR #94).
/// </remarks>
internal sealed class GodotMetalSkiaGpu : GodotSkiaGpu {

	private readonly GRContext _grContext;
	private readonly bool _untrackedResources;

	protected override GRContext GrContext
		=> _grContext;

	public GodotMetalSkiaGpu(RenderingDevice renderingDevice)
		: base(renderingDevice) {

		if (!MetalInterop.IsApplePlatform)
			throw new PlatformNotSupportedException("Metal is only available on Apple platforms");

		var device = (IntPtr) renderingDevice.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice, default, 0UL);
		if (device == IntPtr.Zero || !MetalInterop.ConformsTo(device, "MTLDevice"))
			throw new InvalidOperationException("Godot didn't return a valid MTLDevice for driver resource LogicalDevice");

		var queue = (IntPtr) renderingDevice.GetDriverResource(RenderingDevice.DriverResource.CommandQueue, default, 0UL);
		if (queue == IntPtr.Zero || !MetalInterop.ConformsTo(queue, "MTLCommandQueue"))
			throw new InvalidOperationException("Godot didn't return a valid MTLCommandQueue for driver resource CommandQueue");

		using var backendContext = new GRMtlBackendContext {
			DeviceHandle = device,
			QueueHandle = queue
		};

		_grContext = GRContext.CreateMetal(backendContext)
			?? throw new InvalidOperationException("Couldn't create Metal context");

		_untrackedResources = OS.GetEnvironment("GODOT_MTL_FORCE_BARRIERS") == "1";
	}

	protected override GodotSkiaSurface CreateSurfaceCore(PixelSize size, double renderScaling) {
		var gdRdTexture = CreateSharedTexture(size);
		SKSurface skSurface;

		try {
			var mtlTexture = (IntPtr) RenderingDevice.GetDriverResource(RenderingDevice.DriverResource.Texture, gdRdTexture, 0UL);
			if (mtlTexture == IntPtr.Zero || !MetalInterop.ConformsTo(mtlTexture, "MTLTexture"))
				throw new InvalidOperationException("Couldn't get Metal texture from Godot texture");

			// Godot adds MTLTextureUsageRenderTarget for ColorAttachmentBit. Skia silently refuses to wrap a texture without it.
			var usage = MetalInterop.GetTextureUsage(mtlTexture);
			if ((usage & MetalInterop.MTLTextureUsageRenderTarget) == 0)
				throw new InvalidOperationException($"Metal texture usage 0x{usage:X} lacks MTLTextureUsageRenderTarget");

			// The pixel format is read from the MTLTexture itself: R8G8B8A8Unorm (MTLPixelFormatRGBA8Unorm) matches Rgba8888.
			using var renderTarget = new GRBackendRenderTarget(size.Width, size.Height, new GRMtlTextureInfo(mtlTexture));

			skSurface = SKSurface.Create(
				_grContext,
				renderTarget,
				GRSurfaceOrigin.TopLeft,
				SKColorType.Rgba8888,
				new SKSurfaceProperties(SKPixelGeometry.RgbHorizontal)
			) ?? throw new InvalidOperationException("Couldn't create Skia surface from Metal texture");
		}
		catch {
			RenderingDevice.FreeRid(gdRdTexture);
			throw;
		}

		var gdTexture = new Texture2Drd {
			TextureRdRid = gdRdTexture
		};

		return new GodotSkiaSurface(skSurface, gdTexture, RenderingDevice, renderScaling, new MetalSurfaceSync(_grContext, _untrackedResources));
	}

	public override void Dispose()
		=> _grContext.Dispose();

}
