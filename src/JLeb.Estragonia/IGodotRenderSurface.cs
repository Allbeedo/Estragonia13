using System;
using Avalonia.Platform.Surfaces;
using Godot;

namespace JLeb.Estragonia;

/// <summary>A surface Avalonia renders into, exposed to Godot as a texture.</summary>
/// <remarks>Either GPU-backed (<see cref="GodotSkiaSurface"/>) or CPU-backed (<see cref="GodotSoftwareSurface"/>).</remarks>
internal interface IGodotRenderSurface : IPlatformRenderSurface, IDisposable {

	Texture2D GdTexture { get; }

	double RenderScaling { get; set; }

	ulong DrawCount { get; }

	bool IsDisposed { get; }

}
