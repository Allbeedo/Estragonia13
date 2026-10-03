using System;
using System.Runtime.InteropServices;

namespace JLeb.Estragonia;

/// <summary>
/// The few Objective-C runtime calls needed to sanity check the Metal objects Godot hands over.
/// Skia takes the raw <c>id&lt;MTLDevice&gt;</c> / <c>id&lt;MTLCommandQueue&gt;</c> / <c>id&lt;MTLTexture&gt;</c> pointers itself,
/// so no Metal binding library is needed.
/// </summary>
/// <remarks>
/// Adapted from SkiaGameRendering's <c>MetalNative</c> (MIT, https://github.com/vchelaru/SkiaGameRendering, see issue #92 / PR #94).
/// </remarks>
internal static partial class MetalInterop {

	private const string ObjCLibrary = "/usr/lib/libobjc.A.dylib";

	/// <summary><c>MTLTextureUsageRenderTarget</c>.</summary>
	public const ulong MTLTextureUsageRenderTarget = 0x4;

	public static bool IsApplePlatform
		=> OperatingSystem.IsMacOS() || OperatingSystem.IsIOS() || OperatingSystem.IsTvOS() || OperatingSystem.IsMacCatalyst();

	[LibraryImport(ObjCLibrary, EntryPoint = "sel_registerName", StringMarshalling = StringMarshalling.Utf8)]
	private static partial IntPtr GetSelector(string name);

	[LibraryImport(ObjCLibrary, EntryPoint = "objc_getProtocol", StringMarshalling = StringMarshalling.Utf8)]
	private static partial IntPtr GetProtocol(string name);

	[LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
	private static partial nuint SendNUInt(IntPtr receiver, IntPtr selector);

	[LibraryImport(ObjCLibrary, EntryPoint = "objc_msgSend")]
	[return: MarshalAs(UnmanagedType.I1)]
	private static partial bool SendBool(IntPtr receiver, IntPtr selector, IntPtr arg);

	/// <summary>
	/// Returns whether <paramref name="obj"/> is an Objective-C object implementing <paramref name="protocol"/>.
	/// Catches a wrong handle before Skia dereferences it.
	/// Returns <c>true</c> if the protocol isn't registered (Metal.framework not loaded), since there's nothing to check against.
	/// </summary>
	public static bool ConformsTo(IntPtr obj, string protocol) {
		var proto = GetProtocol(protocol);
		return proto == IntPtr.Zero || SendBool(obj, GetSelector("conformsToProtocol:"), proto);
	}

	/// <summary>Returns the <c>MTLTextureUsage</c> flags of an <c>id&lt;MTLTexture&gt;</c>.</summary>
	public static ulong GetTextureUsage(IntPtr texture)
		=> SendNUInt(texture, GetSelector("usage"));

	/// <summary>
	/// Returns whether Metal's automatic hazard tracking is disabled for an <c>id&lt;MTLResource&gt;</c>
	/// (<c>MTLHazardTrackingModeUntracked</c>), in which case nothing orders Skia's writes and Godot's reads.
	/// </summary>
	public static bool IsUntracked(IntPtr resource)
		=> SendNUInt(resource, GetSelector("hazardTrackingMode")) == MTLHazardTrackingModeUntracked;

	private const nuint MTLHazardTrackingModeUntracked = 1;

}
