# 更新说明

## 未发布

- **Apple 平台：** 新增 macOS / iOS 的 Metal 渲染后端（Godot 默认的 `metal` 驱动），改编自 [SkiaGameRendering](https://github.com/vchelaru/SkiaGameRendering)（[#92](https://github.com/vchelaru/SkiaGameRendering/issues/92)，MIT）。Apple 平台上通过 MoltenVK 的 Vulkan 会给出明确错误。
- 不支持的渲染驱动（`d3d12`、Compatibility）给出明确错误信息。
- 模板和示例按平台固定渲染驱动（Windows / Linux 为 Vulkan，macOS / iOS 为 Metal），因为 Godot 4.6+ 在 Windows 上默认是 D3D12。

## 1.0.6

- 模板和示例是 **一个 Godot 工程**（Avalonia 和宿主脚本都在 `UI/` 下）。

## 1.0.5

- 模板加上 `editorTreatAs: solution`，避免 Visual Studio 再套一层文件夹并在上一级生成 `.slnx`

## 1.0.4

- 首次把工程内 `Estragonia/` 宿主、独立 `*.UI` 预览项目、以及 `.sln` 与 `GodotGame` / `GodotGame.UI` 同层（不再多套一层）发到 nuget.org
- 通过 GitHub Actions + nuget.org Trusted Publishing 发布（`nuget-publish.yml`）

## 1.0.3

- Godot 宿主脚本（`AvaloniaControl`、`UiHost`、Autoload、`UserInterface`）放在工程内 `Estragonia/`；NuGet 库只带 `AvaloniaControlEngine`，便于编辑器热重载卸载游戏程序集
- 模板/示例仍把 Avalonia UI 拆成独立项目（`*.UI`），给预览器用
- 模板输出目录就是解决方案根目录（`.sln` 和 `GodotGame` / `GodotGame.UI` 同一层），不再多套一层（关闭 `preferNameDirectory`）

## 1.0.2

- Godot **4.7.2**（`Godot.NET.Sdk` / `GodotSharp`）
- 模板将 Avalonia 拆到 `GodotGame.UI`（预览器），Godot 脚本单独一个程序集；`.sln` 和两个项目同一层

## 1.0.0

本维护分支的首个版本：

- Avalonia 12 / Godot 4.7 / .NET 10
- CPM 统一包版本（`Directory.Packages.props`）
- `UiHost`、输入穿透、Avalonia 12 Dispatcher / 资源加载修复
- DocFX 文档 + GitHub Pages 工作流
- `dotnet new estragonia` 项目模板
- NuGet：[Ouse.Estragonia](https://www.nuget.org/packages/Ouse.Estragonia/) + [Ouse.Estragonia.Templates](https://www.nuget.org/packages/Ouse.Estragonia.Templates/)
- 源码：[0use-TE/Estragonia](https://github.com/0use-TE/Estragonia)

基于 [MrJul/Estragonia](https://github.com/MrJul/Estragonia)（MIT）。上游包名仍为 `JLeb.Estragonia`。
