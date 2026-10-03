using System;
using System.Runtime.InteropServices;

namespace JLeb.Estragonia;

/// <summary>
/// MoltenVK's functions returning the Metal objects behind Vulkan handles (<c>mvk_deprecated_api.h</c>).
/// Godot links MoltenVK statically on macOS and exports these from its executable, so they're looked up in the main program:
/// this lets Skia draw with Metal into Godot's textures when Godot runs on Vulkan (e.g. Intel Macs, where Godot has no Metal driver).
/// </summary>
/// <remarks>
/// These functions don't require any Vulkan extension to be enabled, unlike <c>VK_EXT_metal_objects</c>,
/// which Godot doesn't enable.
/// </remarks>
internal static unsafe class MoltenVKInterop {

	private static readonly delegate* unmanaged<IntPtr, IntPtr*, void> s_getMTLDevice;
	private static readonly delegate* unmanaged<IntPtr, IntPtr*, void> s_getMTLCommandQueue;
	private static readonly delegate* unmanaged<ulong, IntPtr*, void> s_getMTLTexture;

	/// <summary>Gets whether the MoltenVK functions were found in the running program.</summary>
	public static bool IsAvailable { get; }

	static MoltenVKInterop() {
		if (!MetalInterop.IsApplePlatform)
			return;

		var program = NativeLibrary.GetMainProgramHandle();

		if (NativeLibrary.TryGetExport(program, "vkGetMTLDeviceMVK", out var getMTLDevice)
			&& NativeLibrary.TryGetExport(program, "vkGetMTLCommandQueueMVK", out var getMTLCommandQueue)
			&& NativeLibrary.TryGetExport(program, "vkGetMTLTextureMVK", out var getMTLTexture)) {
			s_getMTLDevice = (delegate* unmanaged<IntPtr, IntPtr*, void>) getMTLDevice;
			s_getMTLCommandQueue = (delegate* unmanaged<IntPtr, IntPtr*, void>) getMTLCommandQueue;
			s_getMTLTexture = (delegate* unmanaged<ulong, IntPtr*, void>) getMTLTexture;
			IsAvailable = true;
		}
	}

	/// <summary>Returns the <c>id&lt;MTLDevice&gt;</c> behind a <c>VkPhysicalDevice</c>.</summary>
	public static IntPtr GetMTLDevice(IntPtr vkPhysicalDevice) {
		IntPtr result;
		s_getMTLDevice(vkPhysicalDevice, &result);
		return result;
	}

	/// <summary>Returns the <c>id&lt;MTLCommandQueue&gt;</c> behind a <c>VkQueue</c>.</summary>
	public static IntPtr GetMTLCommandQueue(IntPtr vkQueue) {
		IntPtr result;
		s_getMTLCommandQueue(vkQueue, &result);
		return result;
	}

	/// <summary>Returns the <c>id&lt;MTLTexture&gt;</c> behind a <c>VkImage</c>.</summary>
	public static IntPtr GetMTLTexture(ulong vkImage) {
		IntPtr result;
		s_getMTLTexture(vkImage, &result);
		return result;
	}

}
