namespace JLeb.Estragonia;

/// <summary>
/// Graphics API specific hand-off of a shared Godot texture between Skia (drawing) and Godot (sampling).
/// </summary>
internal abstract class GodotSurfaceSync {

	/// <summary>Called before Skia draws into the texture.</summary>
	public abstract void BeginDraw();

	/// <summary>Called after Skia's work has been flushed, to give the texture back to Godot.</summary>
	public abstract void EndDraw();

	/// <summary>Called before the texture is freed, to ensure no pending GPU work still references it.</summary>
	public virtual void WaitIdle() {
	}

}
