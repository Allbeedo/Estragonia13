using System;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Platform.Surfaces;
using Godot;
using GdImage = Godot.Image;

namespace JLeb.Estragonia;

/// <summary>
/// CPU fallback used when no GPU backend supports Godot's rendering driver (e.g. d3d12, Compatibility):
/// Avalonia renders with Skia into a memory buffer, which is uploaded to an <see cref="ImageTexture"/> after each frame.
/// </summary>
/// <remarks>
/// The buffer keeps its contents between frames, so Avalonia only redraws the dirty parts of the UI.
/// The upload is still a full-texture copy each time Avalonia renders something.
/// </remarks>
internal sealed class GodotSoftwareSurface : IFramebufferPlatformSurface, IGodotRenderSurface {

	private const int BytesPerPixel = 4;

	private readonly PixelSize _size;
	private readonly byte[] _pixels;
	private readonly GdImage _image;
	private readonly ImageTexture _texture;

	public double RenderScaling { get; set; }

	public ulong DrawCount { get; private set; }

	public bool IsDisposed { get; private set; }

	public Texture2D GdTexture
		=> _texture;

	bool IPlatformRenderSurface.IsReady
		=> !IsDisposed;

	public GodotSoftwareSurface(PixelSize size, double renderScaling) {
		_size = new PixelSize(Math.Max(size.Width, 1), Math.Max(size.Height, 1));
		RenderScaling = renderScaling;

		// Pinned so Skia can draw directly into it.
		_pixels = GC.AllocateArray<byte>(_size.Width * _size.Height * BytesPerPixel, pinned: true);
		_image = GdImage.CreateEmpty(_size.Width, _size.Height, false, GdImage.Format.Rgba8);
		_texture = ImageTexture.CreateFromImage(_image);
	}

	IFramebufferRenderTarget IFramebufferPlatformSurface.CreateFramebufferRenderTarget()
		=> new RenderTarget(this);

	private void Upload() {
		if (IsDisposed)
			return;

		_image.SetData(_size.Width, _size.Height, false, GdImage.Format.Rgba8, _pixels);
		_texture.Update(_image);
		DrawCount++;
	}

	public void Dispose() {
		if (IsDisposed)
			return;

		IsDisposed = true;
		_texture.Dispose();
		_image.Dispose();
	}

	private sealed class RenderTarget : IFramebufferRenderTarget {

		private readonly GodotSoftwareSurface _surface;
		private readonly double _renderScaling;
		private bool _hasRendered;

		public RenderTarget(GodotSoftwareSurface surface) {
			_surface = surface;
			_renderScaling = surface.RenderScaling;
		}

		bool IFramebufferRenderTarget.RetainsFrameContents
			=> true;

		[System.Diagnostics.CodeAnalysis.SuppressMessage("ReSharper", "CompareOfFloatsByEqualityOperator", Justification = "Doesn't affect correctness")]
		PlatformRenderTargetState IPlatformRenderSurfaceRenderTarget.State
			=> _surface.IsDisposed || _renderScaling != _surface.RenderScaling
				? PlatformRenderTargetState.Corrupted
				: PlatformRenderTargetState.Ready;

		public ILockedFramebuffer Lock(IRenderTarget.RenderTargetSceneInfo sceneInfo, out FramebufferLockProperties properties) {
			ObjectDisposedException.ThrowIf(_surface.IsDisposed, _surface);

			properties = new FramebufferLockProperties(PreviousFrameIsRetained: _hasRendered);
			_hasRendered = true;

			var dpi = 96.0 * _surface.RenderScaling;

			return new LockedFramebuffer(
				Marshal.UnsafeAddrOfPinnedArrayElement(_surface._pixels, 0),
				_surface._size,
				_surface._size.Width * BytesPerPixel,
				new Vector(dpi, dpi),
				PixelFormats.Rgba8888,
				AlphaFormat.Premul,
				_surface.Upload
			);
		}

		public void Dispose() {
		}

	}

}
