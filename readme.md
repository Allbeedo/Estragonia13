# Estragonia

在 **Godot 4** 中嵌入 **Avalonia** UI 的桥接库（Vulkan / Skia 共享纹理）。

[![NuGet](https://img.shields.io/nuget/v/Ouse.Estragonia.svg)](https://www.nuget.org/packages/Ouse.Estragonia/)
[![Templates](https://img.shields.io/nuget/v/Ouse.Estragonia.Templates.svg)](https://www.nuget.org/packages/Ouse.Estragonia.Templates/)
[![GitHub](https://img.shields.io/badge/GitHub-0use--TE%2FEstragonia-181717?logo=github)](https://github.com/0use-TE/Estragonia)

| | 链接 |
|--|------|
| 库 | [Ouse.Estragonia](https://www.nuget.org/packages/Ouse.Estragonia/) |
| 模板 | [Ouse.Estragonia.Templates](https://www.nuget.org/packages/Ouse.Estragonia.Templates/) |
| 源码 | [github.com/0use-TE/Estragonia](https://github.com/0use-TE/Estragonia) |
| 文档 | [GitHub Pages](https://0use-te.github.io/Estragonia/)（推送 `main` 后由 Actions 部署） |

> 代码命名空间仍为 **`JLeb.Estragonia`**；NuGet 包名为 **`Ouse.Estragonia`**（与上游 `JLeb.Estragonia` 区分）。

## 重要声明

- 本仓库大量代码由 **AI 辅助编写与改写**（含 Avalonia 12 / Godot 4.7.2 / .NET 10 适配）。
- **不保证稳定性**，请勿在未验证的情况下直接用于生产。
- **维护者会审查**关键改动；欢迎 Issue / PR，但请自行充分测试。

## 署名与许可

基于 [Julien Lebosquain](https://github.com/MrJul) 的开源项目 [Estragonia](https://github.com/MrJul/Estragonia)（**MIT**）。  
原作者版权与许可声明见 [`license.txt`](license.txt)；修改与再分发须保留 MIT 要求的版权与许可文本。

## 环境

| 项 | 版本 |
|----|------|
| .NET SDK | 10.x |
| Godot | 4.7.2+（.NET / Forward+ 或 Mobile） |
| Avalonia | 12.x |

---

## 使用教程（推荐：模板）

### 1. 安装模板

```bash
dotnet new install Ouse.Estragonia.Templates
```

更新到新版本时先卸再装：

```bash
dotnet new uninstall Ouse.Estragonia.Templates
dotnet new install Ouse.Estragonia.Templates
```

### 2. 创建项目

```bash
# -n = 解决方案名；--GodotProjectName = Godot / C# 项目名（须为合法 C# 标识符）
dotnet new estragonia -n MySolution --GodotProjectName MyGame -o MySolution
cd MySolution
dotnet restore
```

Visual Studio：新建项目 → 搜 **Estragonia Godot App**（装模板后请**关掉再开 VS**）。  
对话框应出现 **Create in new folder**（不要再用会套一层的「将解决方案和项目放在同一目录」旧选项）。勾选后得到：

`输出目录/项目名/` 里同时有解决方案文件和 `GodotGame/`（Avalonia 也在这个工程里）。

### 3. 用 Godot 打开

用 **Godot 4.7.2+（.NET）** 打开 **`MyGame/project.godot`**。  
`.sln` 和 `MyGame` 在同一层；不要打开 `.godot/` 缓存目录。

模板已配置好：

- Autoload：`AvaloniaLoader`（`UseGodot()` 只初始化一次）
- 默认宿主：`UserInterface`（`UiHost`）
- Avalonia：`App` / `Views` / `ViewModels` 与 Godot 脚本同一个工程

### 4. 改 UI

- 界面：`MyGame/UI/Views/MainView.axaml`
- 逻辑：`MyGame/UI/ViewModels/MainViewModel.cs`
- 主题：`MyGame/UI/App.axaml`（默认 Semi.Avalonia）

更多说明见 [`templates/README.md`](templates/README.md) 与 [文档 · 快速开始](docs/v1.0.0/zh-CN/getting-started.md)。

---

## 使用教程（手动加包）

已有 Godot C# 工程时：

```bash
dotnet add package Ouse.Estragonia
dotnet add package Semi.Avalonia
# 可选 MVVM
dotnet add package CommunityToolkit.Mvvm
```

1. 增加 Avalonia `Application`（含主题）。
2. Autoload 里调用一次：

```csharp
AppBuilder.Configure<App>()
    .UseGodot()
    .SetupWithoutStarting();

GodotAvalonia.EnsureAssetLoader(typeof(App).Assembly);
GetWindow()?.SetImeActive(true);
```

3. 把模板/示例里 `UI/Estragonia/` 下的宿主脚本复制进 **Godot 工程**（不要放 NuGet 程序集）。场景脚本继承 `UiHost`，实现 `CreateRoot()`。

详见 [docs/v1.0.0/zh-CN/hosting.md](docs/v1.0.0/zh-CN/hosting.md)。

---

## 仓库内示例

1. 用 Godot 打开 `samples/HelloWorld`。
2. 编译并运行（该示例用 `ProjectReference` 指向源码，便于改库）。

## 笔 / Apple Pencil（Pen input）

笔以 **Avalonia 的笔**（`PointerType.Pen`）到达 Avalonia：压力、倾斜、悬停、橡皮端；不再当作鼠标或手指。

- **识别方式：** 设备号为 `GodotPen.PencilDeviceId` 的 Godot 鼠标事件就是笔。这样 Godot 仍把每个事件路由到其下方的控件（本地坐标），Estragonia 再交给 Avalonia。
- **iPad：** Godot 的 iOS 端口不区分 Pencil 与手指。`native/ios/EstragoniaPencil`（GDExtension）只截取 Pencil 的触摸，以上述设备号送回 Godot，并带上压力、倾斜、240 Hz 中间采样、悬停（iPadOS 16.1+）以及双击 / 捏压（`GodotPen.Interaction`）。**尚未在 Mac / iPad 上编译或运行**，见其 README。
- **桌面（Windows 平板等）：** Godot 把笔报告为带压力的鼠标；如需当作笔，设置 `GodotPen.IsDesktopPen`。
- **关闭：** `GodotPen.Enabled = false`。
- **示例：** HelloWorld 的 “Pen” 标签页显示指针类型、压力、倾斜；`ESTRAGONIA_PEN_DEMO=1` 时自动发送一笔模拟 Pencil 的输入（在 Linux 虚拟显示上验证过整条路径）。

## 测试

```
dotnet test tests/JLeb.Estragonia.Tests   # 笔：Godot 无关的 PenTracker，以及经 Avalonia 无头窗口到控件的整条路径
```

## 仓库结构

```
src/JLeb.Estragonia/              # 桥接库（NuGet: Ouse.Estragonia）
tests/JLeb.Estragonia.Tests/      # 测试
native/ios/EstragoniaPencil/      # iPad 上 Apple Pencil 的 GDExtension（Objective-C++）
templates/                        # dotnet new 模板（NuGet: Ouse.Estragonia.Templates）
samples/HelloWorld/               # Godot + Avalonia 示例
docs/v1.0.0/                      # 手写文档（英 / 中）
```

## 热重载提示

`AvaloniaControl` / `UiHost` 必须放在 **Godot 工程程序集**里。库里只保留 `AvaloniaControlEngine`。  
若仍出现 `Failed to unload assemblies`，**完全重启 Godot**（当前日志是 4.7.1 时请改用 **4.7.2**）。

## 文档 / GitHub Pages

- 在线文档：<https://0use-te.github.io/Estragonia/>
- 工作流：`.github/workflows/docs.yml`

本地预览：

```bash
docfx docfx.json
docfx serve _site --port 8080
```

> `api/` 与 `_site/` 为生成物，已 gitignore。维护规则见 [`DOCFX-AI-PROMPT.md`](DOCFX-AI-PROMPT.md)。
