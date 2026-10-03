# Input & rendering

## Rendering

Avalonia draws into a GPU texture shared with Godot (`Texture2Drd`), with no copy. The host `Control` blits that texture in `_Draw`. There is no separate Avalonia “window layer” — z-order follows the Godot scene tree / `z_index`.

The rendering driver is selected from `RenderingServer.GetCurrentRenderingDriverName()`:

| Driver | Platforms | Notes |
|--------|-----------|-------|
| `vulkan` | Windows, Linux | Skia draws into the texture's `VkImage`; image layouts are transitioned around each draw. |
| `metal` | macOS, iOS | Skia shares Godot's `MTLDevice` / `MTLCommandQueue` and draws into the texture's `MTLTexture`. Ordering relies on Metal's automatic hazard tracking; with `GODOT_MTL_FORCE_BARRIERS=1` Estragonia waits on the CPU after each draw instead. |

Other drivers (`d3d12`, the Compatibility renderer, and Vulkan through MoltenVK on Apple platforms, since SkiaSharp's Apple native libraries are built without Vulkan) are not supported.

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
