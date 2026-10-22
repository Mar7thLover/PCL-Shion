[简体中文](README.md) | **English** | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

# PCL Shion

[![Stars](https://img.shields.io/github/stars/Mar7thLover/PCL-CE?style=for-the-badge&label=stars&labelColor=444444&color=eac54f&logo=github)](https://github.com/Mar7thLover/PCL-CE/)
[![Last commit](https://img.shields.io/github/last-commit/Mar7thLover/PCL-CE/dev?style=for-the-badge&label=last%20commit&labelColor=444444&logo=github)](https://github.com/Mar7thLover/PCL-CE/commits/dev)
[![Issues](https://img.shields.io/github/issues/Mar7thLover/PCL-CE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-CE/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/Mar7thLover/PCL-CE?style=for-the-badge&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-CE/pulls)

[Download](https://github.com/Mar7thLover/PCL-CE/releases) |
[Report a bug or suggest a feature](https://github.com/Mar7thLover/PCL-CE/issues/new/choose) |
[Contributing](CONTRIBUTING.md)

</div>

PCL Shion (詩音) is a **third-party modified version**, independently developed from [PCL Community Edition (PCL CE)](https://github.com/PCL-Community/PCL-CE), which is itself based on [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) by LTCat (龙腾猫跃).

This project is not affiliated with the official PCL or the PCL CE community. **Please do not report issues with this project to the official repository or to the PCL CE community.** Use [this repository's Issues](https://github.com/Mar7thLover/PCL-CE/issues) instead.

> [!WARNING]
> This project is under **heavy development**. Features and UI change frequently and may be unstable.

## ✨ How It Differs from PCL CE

### 🌏 Region restrictions removed, and they will never come back

In [#3673](https://github.com/PCL-Community/PCL-CE/pull/3673) and [#3676](https://github.com/PCL-Community/PCL-CE/pull/3676), upstream PCL CE started guessing whether the user is in mainland China from the system region, time zone and language, and used that to disable downloading, installing and completing Minecraft and modpacks.

PCL Shion has removed this restriction entirely, so downloading and installing work for users in every region. **We promise never to reintroduce any region-based feature restriction.**

### 🚀 Actively developed, feature suggestions welcome

Upstream PCL CE has entered maintenance mode and no longer accepts feature requests. PCL Shion keeps adding new features, and **any reasonable feature suggestion is welcome**. Just [open an issue](https://github.com/Mar7thLover/PCL-CE/issues/new/choose).

## 🗺️ Roadmap

- **Cross-platform UI framework**: gradually replace the current Windows-only WPF with a new cross-platform UI framework. The migration will be incremental: the new UI ships first as an experimental feature and becomes the default once it is mature.
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
| macOS / Linux / other operating systems | ⚠️ Cross-platform development only (cross-compilation) | [.NET 10 SDK](https://get.dot.net/10) |

**✅ Fully supported**: we provide as much support as possible, but you must be on the latest version of the launcher.

**⚠️ Should work, not guaranteed**: PCL Shion should run on these platforms, but not every feature is guaranteed to work.

**❌ Not supported**: PCL Shion works poorly on these platforms, or may not start at all. Please upgrade your operating system.

**⚠️ Cross-platform development only (cross-compilation)**: the source code builds on macOS and Linux but cannot run there directly. You can develop on these platforms and test the build output on Windows. This will gradually change as the cross-platform UI framework lands.

**Note**: only the latest version of the launcher is supported.

## 🔒 License

- `Plain Craft Launcher 2/` uses a [custom license](Plain%20Craft%20Launcher%202/LICENCE)
- `All other directories` use the [Apache License 2.0](LICENSE)

## 🙏 Acknowledgements

- [LTCat (龙腾猫跃)](https://github.com/LTCatt) and [Plain Craft Launcher](https://github.com/Meloong-Git/PCL): the original author and project of PCL. [Sponsor LTCat](https://afdian.com/a/LTCat)
- [PCL-Community](https://github.com/PCL-Community) and [PCL CE](https://github.com/PCL-Community/PCL-CE): the Community Edition this project is based on, and all of its contributors.

## 🌟 Statistics

[![Star History Chart](https://api.star-history.com/svg?repos=Mar7thLover/PCL-CE&type=Date)](https://www.star-history.com/#Mar7thLover/PCL-CE&Date)

## ❤️ Contributors

[![](https://contrib.rocks/image?repo=Mar7thLover/PCL-CE)](https://github.com/Mar7thLover/PCL-CE/graphs/contributors)
