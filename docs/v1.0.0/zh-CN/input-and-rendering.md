# 输入与渲染

## 渲染

Avalonia 画到与 Godot 共享的 GPU 纹理（零拷贝；Windows / Linux 为 Vulkan，macOS / iOS 为 Metal），由宿主 `Control._Draw` 贴出。层级跟 Godot 场景树 / `z_index` 走。

Metal 下依赖 Metal 自动 hazard tracking 保证顺序；若设置 `GODOT_MTL_FORCE_BARRIERS=1`，每次绘制后会在 CPU 上等待 GPU。不支持 `d3d12`、Compatibility 渲染器，以及 Apple 平台上通过 MoltenVK 的 Vulkan（SkiaSharp 的 Apple 原生库未包含 Vulkan）。

## 输入顺序

```text
_Input → _GuiInput（Avalonia 宿主）→ _UnhandledInput
```

宿主收到的指针事件通常会 `AcceptEvent()`。更早的 `_Input` 若 `SetInputAsHandled()`，Avalonia 可能收不到。
