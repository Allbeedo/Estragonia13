# Input & rendering

## Rendering

Avalonia draws into a GPU texture shared with Godot (`Texture2Drd`), with no copy. The host `Control` blits that texture in `_Draw`. There is no separate Avalonia “window layer” — z-order follows the Godot scene tree / `z_index`.

The rendering driver is selected from `RenderingServer.GetCurrentRenderingDriverName()`:

| Driver | Platforms | Notes |
|--------|-----------|-------|
| `vulkan` | Windows, Linux | Skia draws into the texture's `VkImage`; image layouts are transitioned around each draw. On Windows, Godot defaults to `d3d12`: set `rendering/rendering_device/driver.windows` to `vulkan` for GPU rendering. |
| `metal` | macOS, iOS | Skia shares Godot's `MTLDevice` / `MTLCommandQueue` and draws into the texture's `MTLTexture`. Ordering relies on Metal's automatic hazard tracking; with `GODOT_MTL_FORCE_BARRIERS=1` Estragonia waits on the CPU after each draw instead. |

| any other | all | **CPU fallback:** Avalonia renders with Skia into a memory buffer, uploaded to an `ImageTexture` after each frame. Only the dirty parts of the UI are redrawn, but the upload copies the whole texture whenever something changes. |

The CPU fallback is used for `d3d12`, the Compatibility renderer (`opengl3`), Vulkan through MoltenVK on Apple platforms (SkiaSharp's Apple native libraries are built without Vulkan), and whenever GPU initialization fails. Estragonia prints a warning saying so. It's fine for menus and HUDs; for large, constantly animating UIs, use a GPU driver. `GodotVkPlatformGraphics.IsSoftware` tells which path is in use.

## Input order (Godot)

```text
Node._Input
  → Control._GuiInput (Avalonia host, if _HasPoint)
  → Node._UnhandledInput
```

- Avalonia host typically `AcceptEvent()` for pointer events it receives, so later GUI / unhandled handlers do not see them.
- A node using `_Input` + `SetInputAsHandled()` can run **before** Avalonia and steal the event.

## Themes

Avalonia `RequestedThemeVariant` Light/Dark works. Platform “system” theme from Estragonia currently defaults to Dark via `GodotPlatformSettings`.
