using static JLeb.Estragonia.VkInterop;

namespace JLeb.Estragonia;

/// <summary>Transitions a shared <see cref="VkImage"/> between the layouts expected by Skia and Godot.</summary>
internal sealed class VkSurfaceSync : GodotSurfaceSync {

	private readonly VkImage _vkImage;
	private readonly VkBarrierHelper _barrierHelper;

	public VkImageLayout LastLayout { get; private set; }

	public VkSurfaceSync(VkImage vkImage, VkImageLayout lastLayout, VkBarrierHelper barrierHelper) {
		_vkImage = vkImage;
		LastLayout = lastLayout;
		_barrierHelper = barrierHelper;
	}

	// Godot leaves the image in SHADER_READ_ONLY_OPTIMAL but Skia expects it in COLOR_ATTACHMENT_OPTIMAL
	public override void BeginDraw()
		=> TransitionLayoutTo(VkImageLayout.COLOR_ATTACHMENT_OPTIMAL);

	// Switch back to SHADER_READ_ONLY_OPTIMAL for Godot
	public override void EndDraw()
		=> TransitionLayoutTo(VkImageLayout.SHADER_READ_ONLY_OPTIMAL);

	public void TransitionLayoutTo(VkImageLayout newLayout) {
		if (LastLayout == newLayout)
			return;

		var sourceAccessMask = LastLayout switch {
			VkImageLayout.COLOR_ATTACHMENT_OPTIMAL => VkAccessFlags.COLOR_ATTACHMENT_READ_BIT,
			VkImageLayout.SHADER_READ_ONLY_OPTIMAL => VkAccessFlags.SHADER_READ_BIT,
			_ => VkAccessFlags.MEMORY_READ_BIT | VkAccessFlags.MEMORY_WRITE_BIT
		};

		var destinationAccessMask = newLayout switch {
			VkImageLayout.COLOR_ATTACHMENT_OPTIMAL => VkAccessFlags.COLOR_ATTACHMENT_WRITE_BIT,
			VkImageLayout.SHADER_READ_ONLY_OPTIMAL => VkAccessFlags.SHADER_WRITE_BIT,
			_ => VkAccessFlags.MEMORY_READ_BIT | VkAccessFlags.MEMORY_WRITE_BIT
		};

		_barrierHelper.TransitionImageLayout(_vkImage, LastLayout, sourceAccessMask, newLayout, destinationAccessMask);
		LastLayout = newLayout;
	}

}
