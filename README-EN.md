[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

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

- **Cross-platform UI framework**: the new Avalonia-based UI now ships as the official UI on macOS and Linux, while Windows keeps the WPF UI for now. The Avalonia UI will keep catching up on features and gradually replace WPF once it is mature.
- **Themes and personalization**: richer themes and personalization options.
- **Fun little tools**: more handy and fun built-in tools.
- **A new multiplayer solution**: an alternative online multiplayer solution that does not depend on Natayark OpenID.
- **…and more**: more plans will be announced as development goes on. Your ideas are welcome too.

## 💻 Supported Platforms

| Operating System | Support Status | Requirements |
|---|---|---|
| Windows 10 1809 (17763) or later | ✅ Fully supported | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ Should work, not guaranteed | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 or earlier | ❌ Not supported | / |
| macOS 12 or later (Intel / Apple silicon) | 🧪 Avalonia UI, under active development | None (the package bundles the .NET runtime) |
| Linux (x64 / ARM64) | 🧪 Avalonia UI, under active development | None (the package bundles the .NET runtime) |
| Other operating systems | ❌ Not supported | / |

**✅ Fully supported**: we provide as much support as possible, but you must be on the latest version of the launcher.

**⚠️ Should work, not guaranteed**: PCL Shion should run on these platforms, but not every feature is guaranteed to work.

**❌ Not supported**: PCL Shion works poorly on these platforms, or may not start at all. Please upgrade your operating system.

**🧪 Avalonia UI, under active development**: macOS and Linux use the cross-platform Avalonia UI. Download `PCL_Shion_*_osx-*.tar.gz` or `PCL_Shion_*_linux-*.tar.gz` for your platform from [Releases](https://github.com/Mar7thLover/PCL-Shion/releases). This UI is ported from the WPF version, so some features may not yet match the Windows version; please report any problems you run into. The macOS build is not notarized by Apple, so the first time you open it, right-click it in Finder and choose Open.

**Note**: only the latest version of the launcher is supported.

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
