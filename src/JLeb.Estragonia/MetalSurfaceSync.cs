using SkiaSharp;

namespace JLeb.Estragonia;

/// <summary>
/// Hands a shared <c>id&lt;MTLTexture&gt;</c> between Skia and Godot.
/// Metal textures have no layouts, so there's nothing to transition: Skia commits its command buffers to Godot's own
/// <c>MTLCommandQueue</c>, and Metal's automatic hazard tracking orders Godot's later sampling after Skia's writes.
/// </summary>
/// <remarks>
/// Godot's Metal driver creates untracked resources when <c>GODOT_MTL_FORCE_BARRIERS=1</c> is set.
/// Nothing orders the two in that case, so we wait for the GPU on the CPU after each draw instead.
/// Adapted from SkiaGameRendering's <c>MetalGodotBackend</c> (MIT, https://github.com/vchelaru/SkiaGameRendering).
/// </remarks>
internal sealed class MetalSurfaceSync : GodotSurfaceSync {

	private readonly GRContext _grContext;
	private readonly bool _waitForGpu;

	public MetalSurfaceSync(GRContext grContext, bool waitForGpu) {
		_grContext = grContext;
		_waitForGpu = waitForGpu;
	}

	public override void BeginDraw() {
	}

	public override void EndDraw()
		=> _grContext.Flush(submit: true, synchronous: _waitForGpu);

	public override void WaitIdle()
		=> _grContext.Flush(submit: true, synchronous: true);

}
