# 输入与渲染

## 渲染

Avalonia 画到与 Godot 共享的 GPU 纹理（零拷贝；Windows / Linux 为 Vulkan，macOS / iOS 为 Metal），由宿主 `Control._Draw` 贴出。层级跟 Godot 场景树 / `z_index` 走。

Metal 下依赖 Metal 自动 hazard tracking 保证顺序；若设置 `GODOT_MTL_FORCE_BARRIERS=1`，每次绘制后会在 CPU 上等待 GPU。Windows 上 Godot 默认是 `d3d12`，要用 GPU 渲染须把 `rendering/rendering_device/driver.windows` 设为 `vulkan`。

**CPU 回退：** `d3d12`、Compatibility 渲染器（`opengl3`）、Apple 平台上通过 MoltenVK 的 Vulkan（SkiaSharp 的 Apple 原生库未包含 Vulkan），以及 GPU 初始化失败时，Avalonia 会用 Skia 在内存缓冲区中渲染，每帧后上传到 `ImageTexture`，并打印警告。只重绘 UI 的脏区域，但有变化时会上传整张纹理。菜单和 HUD 没问题；大型、持续动画的 UI 请使用 GPU 驱动。`GodotVkPlatformGraphics.IsSoftware` 表示当前是否为 CPU 渲染。

## 输入顺序

```text
_Input → _GuiInput（Avalonia 宿主）→ _UnhandledInput
```

宿主收到的指针事件通常会 `AcceptEvent()`。更早的 `_Input` 若 `SetInputAsHandled()`，Avalonia 可能收不到。
