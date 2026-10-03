using System;
using Avalonia;
using Godot;
using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>
/// Bridges Godot on Apple platforms with a Skia context used by Avalonia, using Metal.
/// Skia gets Godot's own <c>id&lt;MTLDevice&gt;</c> and <c>id&lt;MTLCommandQueue&gt;</c>,
/// and draws directly into the <c>id&lt;MTLTexture&gt;</c> backing each Godot texture (zero-copy).
/// The Metal objects come either from Godot's Metal driver (<see cref="CreateForMetalDriver"/>),
/// or from MoltenVK when Godot runs on Vulkan (<see cref="CreateForMoltenVK"/>, e.g. Intel Macs, where Godot has no Metal driver).
/// </summary>
/// <remarks>
/// Adapted from SkiaGameRendering's <c>MetalSkiaSurfaceFactory</c> / <c>MetalGodotBackend</c>
/// (MIT, https://github.com/vchelaru/SkiaGameRendering, see issue #92 / PR #94).
/// </remarks>
internal sealed class GodotMetalSkiaGpu : GodotSkiaGpu {

	private readonly GRContext _grContext;
	private readonly Func<Rid, IntPtr> _getMTLTexture;
	private readonly bool _untrackedResources;

	protected override GRContext GrContext
		=> _grContext;

	public override string Description { get; }

	/// <summary>Uses the Metal objects of Godot's Metal rendering driver.</summary>
	public static GodotMetalSkiaGpu CreateForMetalDriver(RenderingDevice renderingDevice) {
		var device = (IntPtr) renderingDevice.GetDriverResource(RenderingDevice.DriverResource.LogicalDevice, default, 0UL);
		var queue = (IntPtr) renderingDevice.GetDriverResource(RenderingDevice.DriverResource.CommandQueue, default, 0UL);

		return new GodotMetalSkiaGpu(
			renderingDevice,
			device,
			queue,
			texture => (IntPtr) renderingDevice.GetDriverResource(RenderingDevice.DriverResource.Texture, texture, 0UL),
			// Godot's Metal driver creates untracked resources with this flag.
			OS.GetEnvironment("GODOT_MTL_FORCE_BARRIERS") == "1",
			"Metal"
		);
	}

	/// <summary>
	/// Uses the Metal objects behind the Vulkan objects of Godot's Vulkan rendering driver, which runs on MoltenVK on Apple platforms.
	/// Returns null if MoltenVK's functions aren't available.
	/// </summary>
	/// <remarks>
	/// Skia commits its command buffers to the <c>MTLCommandQueue</c> behind Godot's <c>VkQueue</c>, so they're ordered with Godot's
	/// submissions, and MoltenVK creates tracked textures, so Metal orders Skia's writes and Godot's reads.
	/// Vulkan image layouts don't exist in Metal, so the texture's layout as tracked by Godot stays valid.
	/// </remarks>
	public static GodotMetalSkiaGpu? CreateForMoltenVK(RenderingDevice renderingDevice) {
		if (!MoltenVKInterop.IsAvailable)
			return null;

		var vkPhysicalDevice = (IntPtr) renderingDevice.GetDriverResource(RenderingDevice.DriverResource.PhysicalDevice, default, 0UL);
		var vkQueue = (IntPtr) renderingDevice.GetDriverResource(RenderingDevice.DriverResource.CommandQueue, default, 0UL);
		if (vkPhysicalDevice == IntPtr.Zero || vkQueue == IntPtr.Zero)
			throw new InvalidOperationException("Godot returned null Vulkan objects for driver resources PhysicalDevice / CommandQueue");

		return new GodotMetalSkiaGpu(
			renderingDevice,
			MoltenVKInterop.GetMTLDevice(vkPhysicalDevice),
			MoltenVKInterop.GetMTLCommandQueue(vkQueue),
			texture => {
				var vkImage = renderingDevice.GetDriverResource(RenderingDevice.DriverResource.Texture, texture, 0UL);
				return vkImage == 0UL ? IntPtr.Zero : MoltenVKInterop.GetMTLTexture(vkImage);
			},
			false,
			"Metal via MoltenVK"
		);
	}

	private GodotMetalSkiaGpu(
		RenderingDevice renderingDevice,
		IntPtr device,
		IntPtr queue,
		Func<Rid, IntPtr> getMTLTexture,
		bool untrackedResources,
		string description
	)
		: base(renderingDevice) {

		if (!MetalInterop.IsApplePlatform)
			throw new PlatformNotSupportedException("Metal is only available on Apple platforms");

		if (device == IntPtr.Zero || !MetalInterop.ConformsTo(device, "MTLDevice"))
			throw new InvalidOperationException("Couldn't get a valid MTLDevice from Godot");

		if (queue == IntPtr.Zero || !MetalInterop.ConformsTo(queue, "MTLCommandQueue"))
			throw new InvalidOperationException("Couldn't get a valid MTLCommandQueue from Godot");

		_getMTLTexture = getMTLTexture;
		_untrackedResources = untrackedResources;
		Description = description;

		using var backendContext = new GRMtlBackendContext {
			DeviceHandle = device,
			QueueHandle = queue
		};

		_grContext = GRContext.CreateMetal(backendContext)
			?? throw new InvalidOperationException("Couldn't create Metal context");
	}

	protected override GodotSkiaSurface CreateSurfaceCore(PixelSize size, double renderScaling) {
		var gdRdTexture = CreateSharedTexture(size);
		SKSurface skSurface;
		bool waitForGpu;

		try {
			var mtlTexture = _getMTLTexture(gdRdTexture);
			if (mtlTexture == IntPtr.Zero || !MetalInterop.ConformsTo(mtlTexture, "MTLTexture"))
				throw new InvalidOperationException("Couldn't get Metal texture from Godot texture");

			// Godot adds MTLTextureUsageRenderTarget for ColorAttachmentBit. Skia silently refuses to wrap a texture without it.
			var usage = MetalInterop.GetTextureUsage(mtlTexture);
			if ((usage & MetalInterop.MTLTextureUsageRenderTarget) == 0)
				throw new InvalidOperationException($"Metal texture usage 0x{usage:X} lacks MTLTextureUsageRenderTarget");

			// Without hazard tracking, nothing orders Skia's writes and Godot's reads: wait for the GPU after each draw instead.
			waitForGpu = _untrackedResources || MetalInterop.IsUntracked(mtlTexture);

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

		return new GodotSkiaSurface(skSurface, gdTexture, RenderingDevice, renderScaling, new MetalSurfaceSync(_grContext, waitForGpu));
	}

	public override void Dispose()
		=> _grContext.Dispose();

}
