# Release notes

## Unreleased

- **Apple platforms:** new Metal rendering backend for macOS / iOS (Godot's default `metal` driver), adapted from [SkiaGameRendering](https://github.com/vchelaru/SkiaGameRendering) ([#92](https://github.com/vchelaru/SkiaGameRendering/issues/92), MIT). Vulkan through MoltenVK is rejected with a clear error.
- Clear error messages for unsupported rendering drivers (`d3d12`, Compatibility).
- Template and sample pin the rendering driver per platform (Vulkan on Windows / Linux, Metal on macOS / iOS), since Godot 4.6+ defaults to D3D12 on Windows.

## 1.0.6

- Template and sample are a **single Godot project** (Avalonia and host scripts under `UI/`).

## 1.0.5

- Template: `editorTreatAs: solution` so Visual Studio does not wrap the output in an extra folder + parent `.slnx`

## 1.0.4

- First nuget.org release of the in-project `Estragonia/` hosts, separate `*.UI` previewer project, and solution files next to `GodotGame` / `GodotGame.UI` (no extra parent folder)
- Publish from GitHub Actions via nuget.org Trusted Publishing (`nuget-publish.yml`)

## 1.0.3

- Godot host scripts (`AvaloniaControl`, `UiHost`, Autoload, `UserInterface`) live in the Godot project under `Estragonia/`; the NuGet library only ships `AvaloniaControlEngine` so editor hot-reload can unload the game assembly
- Template/sample keep a separate Avalonia UI project (`*.UI`) for the designer previewer
- Template output is the solution root (`.sln` next to `GodotGame` / `GodotGame.UI`); no extra parent folder (`preferNameDirectory` off)

## 1.0.2

- Godot **4.7.2** (`Godot.NET.Sdk` / `GodotSharp`)
- Template splits Avalonia into `GodotGame.UI` (previewer) vs Godot scripts (`AvaloniaLoader` / `UiHost`); `.sln` sits next to the two projects

## 1.0.0

Initial release of this maintained fork:

- Avalonia 12 / Godot 4.7 / .NET 10
- Central Package Management (`Directory.Packages.props`)
- `UiHost`, input pass-through, Avalonia 12 dispatcher / asset-loader fixes
- DocFX documentation + GitHub Pages workflow
- `dotnet new estragonia` project template
- NuGet: [Ouse.Estragonia](https://www.nuget.org/packages/Ouse.Estragonia/) + [Ouse.Estragonia.Templates](https://www.nuget.org/packages/Ouse.Estragonia.Templates/)
- Source: [0use-TE/Estragonia](https://github.com/0use-TE/Estragonia)

Based on [MrJul/Estragonia](https://github.com/MrJul/Estragonia) (MIT). Upstream package id remains `JLeb.Estragonia`.
