[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Shion/Assets/Images/icon.png" alt="Logo" width="80" height="80">

# PCL Shion

[![Stars](https://img.shields.io/github/stars/Mar7thLover/PCL-Shion?style=for-the-badge&label=stars&labelColor=444444&color=eac54f&logo=github)](https://github.com/Mar7thLover/PCL-Shion/)
[![Last commit](https://img.shields.io/github/last-commit/Mar7thLover/PCL-Shion/dev?style=for-the-badge&label=last%20commit&labelColor=444444&logo=github)](https://github.com/Mar7thLover/PCL-Shion/commits/dev)
[![Issues](https://img.shields.io/github/issues/Mar7thLover/PCL-Shion?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-Shion/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/Mar7thLover/PCL-Shion?style=for-the-badge&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-Shion/pulls)

[Download](https://github.com/Mar7thLover/PCL-Shion/releases) |
[Report a bug or suggest a feature](https://github.com/Mar7thLover/PCL-Shion/issues/new/choose) |
[Contributing](CONTRIBUTING.md)

</div>

PCL Shion (詩音) is a **third-party modified version**, independently developed from [PCL Community Edition (PCL CE)](https://github.com/PCL-Community/PCL-CE), which is itself based on [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) by LTCat (龙腾猫跃).

This project is not affiliated with the official PCL or the PCL CE community. **Please do not report issues with this project to the official repository or to the PCL CE community.** Use [this repository's Issues](https://github.com/Mar7thLover/PCL-Shion/issues) instead.

> [!WARNING]
> This project is under **heavy development**. Features and UI change frequently and may be unstable.

## ✨ How It Differs from PCL CE

### 🌏 Region restrictions removed, and they will never come back

In [#3673](https://github.com/PCL-Community/PCL-CE/pull/3673) and [#3676](https://github.com/PCL-Community/PCL-CE/pull/3676), upstream PCL CE started guessing whether the user is in mainland China from the system region, time zone and language, and used that to disable downloading, installing and completing Minecraft and modpacks.

PCL Shion has removed this restriction entirely, so downloading and installing work for users in every region. **We promise never to reintroduce any region-based feature restriction.**

### 🚀 Actively developed, feature suggestions welcome

Upstream PCL CE has entered maintenance mode and no longer accepts feature requests. PCL Shion keeps adding new features, and **any reasonable feature suggestion is welcome**. Just [open an issue](https://github.com/Mar7thLover/PCL-Shion/issues/new/choose).

## 🗺️ Roadmap

- **One cross-platform UI**: Windows, macOS and Linux now use Avalonia as the official entry point. WPF remains only as historical source for migration reference and is excluded from default builds and releases. Feature coverage and stability continue to improve.
- **Themes and personalization**: richer themes and personalization options.
- **Fun little tools**: more handy and fun built-in tools.
- **A new multiplayer solution**: an alternative online multiplayer solution that does not depend on Natayark OpenID.
- **…and more**: more plans will be announced as development goes on. Your ideas are welcome too.

## 💻 Supported Platforms

Builds after this migration use **Avalonia + .NET 10** on every desktop platform. All release packages are **framework-dependent** and no longer bundle the .NET runtime.

| Operating system | UI and support scope | Requirements |
|---|---|---|
| Windows 11 (x64 / ARM64) | Official Avalonia UI | Matching architecture of the [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Windows 10 22H2 (x64) | Best effort, subject to OS and framework lifecycles | Same as above |
| macOS 15 or later (Intel / Apple silicon) | Official Avalonia UI | Matching architecture of the .NET 10 Runtime |
| Linux (x64 / ARM64, glibc) | Official Avalonia UI; X11 / XWayland desktop | .NET 10 Runtime and native desktop dependencies |
| Earlier or other operating systems | Outside the current validation scope | See the framework support links below |

The ordinary **.NET Runtime** is sufficient; Windows Desktop Runtime is not required. An installed .NET 10 SDK of the same architecture also works. Run `dotnet --list-runtimes` to check for `Microsoft.NETCore.App 10.x`. Match runtime and package architecture: for example, use ARM64 Runtime with the AppleSilicon package on Apple silicon.

See the [.NET 10 OS support list](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md), [Avalonia supported platforms](https://docs.avaloniaui.net/docs/supported-platforms), and [Linux dependencies](https://docs.avaloniaui.net/docs/platform-specific-guides/linux) for system details. Development is ongoing; CI builds and automated tests do not replace real game-launch acceptance testing on each platform.

## 📦 Choose a Download

Select the matching asset from [Releases](https://github.com/Mar7thLover/PCL-Shion/releases/latest). Beta builds replace `Release` with `Beta`. **Every package requires a separately installed .NET 10 Runtime.**

| Platform | File name |
|---|---|
| Windows x64 | `PCL_Shion_Release_Avalonia_Windows-x64.exe` |
| Windows ARM64 | `PCL_Shion_Release_Avalonia_Windows-ARM64.exe` |
| macOS Apple silicon | `PCL_Shion_Release_Avalonia_macOS-AppleSilicon.tar.gz` |
| macOS Intel | `PCL_Shion_Release_Avalonia_macOS-Intel.tar.gz` |
| Linux x64 | `PCL_Shion_Release_Avalonia_Linux-x64.tar.gz` |
| Linux ARM64 | `PCL_Shion_Release_Avalonia_Linux-ARM64.tar.gz` |

Windows uses a single executable. On Linux, extract the archive and run `PCL.Shion`; on macOS, open the extracted `PCL Shion.app`. The macOS package is not notarized by Apple. If macOS blocks the first launch, verify the download source and follow [Apple's instructions](https://support.apple.com/en-us/102445) under System Settings → Privacy & Security → Open Anyway. Releases include `SHA256SUMS` and Sigstore signatures. Actual package sizes are shown in release assets.

### Upgrading

1. Install the .NET 10 Runtime matching the download architecture, then start the new launcher.
2. Existing Avalonia data can be reused. WPF previously stored its settings and accounts separately; do not overwrite Avalonia data with WPF settings. Keep the old folder, add your existing game directories in the new UI, and sign in again. Existing worlds and mods do not need to be deleted.
3. Historical WPF and self-contained downloads retain their original requirements. Future builds publish Avalonia only.

### Known Feature Differences

Avalonia is the default entry point, but does not yet match every historical WPF feature. **Launch script export and test-mode launch are not implemented**; their buttons are disabled with an explanation. Use the launch page for normal game launch. See [validation results](docs/validation.md) for remaining acceptance checks and test coverage.

## 🛠️ Build

With the .NET 10 SDK installed:

```bash
dotnet build PCL.Shion.slnx -c Release
dotnet test PCL.Shion.Core.Test/PCL.Shion.Core.Test.csproj -c Release
dotnet test PCL.Shion.Test/PCL.Shion.Test.csproj -c Release
```

See [Contributing](CONTRIBUTING.md) for framework-dependent publishing and package verification. The historical WPF solution is [legacy/PCL.Wpf.slnx](legacy/PCL.Wpf.slnx).

[Upstream integration](docs/upstream-integration.md) · [Validation results](docs/validation.md)

## 🔒 License

- `Plain Craft Launcher 2/` uses a [custom license](Plain%20Craft%20Launcher%202/LICENCE)
- `All other directories` use the [Apache License 2.0](LICENSE)

## 🙏 Acknowledgements

- [LTCat (龙腾猫跃)](https://github.com/LTCatt) and [Plain Craft Launcher](https://github.com/Meloong-Git/PCL): the original author and project of PCL. [Sponsor LTCat](https://afdian.com/a/LTCat)
- [PCL-Community](https://github.com/PCL-Community) and [PCL CE](https://github.com/PCL-Community/PCL-CE): the Community Edition this project is based on, and all of its contributors.

## 🌟 Statistics

[![Star History Chart](https://api.star-history.com/svg?repos=Mar7thLover/PCL-Shion&type=Date)](https://www.star-history.com/#Mar7thLover/PCL-Shion&Date)

## ❤️ Contributors

[![](https://contrib.rocks/image?repo=Mar7thLover/PCL-Shion)](https://github.com/Mar7thLover/PCL-Shion/graphs/contributors)
