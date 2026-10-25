**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

# PCL 詩音（Shion）

[![Stars](https://img.shields.io/github/stars/Mar7thLover/PCL-Shion?style=for-the-badge&label=stars&labelColor=444444&color=eac54f&logo=github)](https://github.com/Mar7thLover/PCL-Shion/)
[![Last commit](https://img.shields.io/github/last-commit/Mar7thLover/PCL-Shion/dev?style=for-the-badge&label=last%20commit&labelColor=444444&logo=github)](https://github.com/Mar7thLover/PCL-Shion/commits/dev)
[![Issues](https://img.shields.io/github/issues/Mar7thLover/PCL-Shion?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-Shion/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/Mar7thLover/PCL-Shion?style=for-the-badge&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-Shion/pulls)

[下载](https://github.com/Mar7thLover/PCL-Shion/releases) |
[提交问题或建议](https://github.com/Mar7thLover/PCL-Shion/issues/new/choose) |
[贡献指南](CONTRIBUTING.md)

</div>

PCL 詩音（Shion）是基于 [PCL 社区版（PCL CE）](https://github.com/PCL-Community/PCL-CE) 独立进行二次开发的**第三方修改版**，PCL CE 又源自龙腾猫跃的 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL)。

本项目与 PCL 官方、PCL CE 社区均无隶属关系，**请不要向官方仓库或 PCL CE 社区反馈本项目的问题**，有问题请在 [本仓库的 Issues](https://github.com/Mar7thLover/PCL-Shion/issues) 提出。

> [!WARNING]
> 本项目正处于**密集开发阶段**，功能和界面变动频繁，可能出现不稳定的情况。

## ✨ 与 PCL CE 的不同

### 🌏 移除区域限制，并承诺不会重新引入

上游 PCL CE 在 [#3673](https://github.com/PCL-Community/PCL-CE/pull/3673) 和 [#3676](https://github.com/PCL-Community/PCL-CE/pull/3676) 中，根据系统地区、时区和语言判断用户是否位于中国大陆，并据此禁用 Minecraft 本体、整合包的下载、安装与补全。

PCL 詩音已完整移除这套限制，所有地区的用户都可以正常使用下载与安装功能。**我们承诺不会重新引入任何基于地区的功能限制。**

### 🚀 锐意开发，欢迎新功能建议

上游 PCL CE 已进入维护阶段，不再受理新功能请求；PCL 詩音则会持续开发新功能，**欢迎任何合理的新功能建议**，请直接 [提交 Issue](https://github.com/Mar7thLover/PCL-Shion/issues/new/choose)。

## 🗺️ 开发计划

- **跨平台 UI 框架**：基于 Avalonia 的新界面已作为 macOS 与 Linux 平台的正式界面提供，Windows 平台暂时继续使用 WPF 界面。之后会继续补齐 Avalonia 界面的功能，成熟后再逐步取代 WPF。
- **主题与个性化**：引入更丰富的主题与个性化选项。
- **有趣的小工具**：加入更多实用又好玩的小工具。
- **新的联机方案**：考虑引入不依赖 Natayark OpenID 的联机方案，作为现有方案的替代选择。
- **……以及更多**：更多计划会在开发过程中陆续公布，也欢迎提出你的想法。

## 💻 支持平台

| 操作系统 | 支持情况 | 环境要求 |
|---|---|---|
| Windows 10 1809 (17763) 或更高 | ✅ 完整支持 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ 理论能跑，不保证可用 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 或更低版本 | ❌ 不支持 | / |
| macOS 12 或更高（Intel / Apple 芯片） | 🧪 Avalonia 界面，持续完善中 | 无（发布包已自带 .NET 运行时） |
| Linux（x64 / ARM64） | 🧪 Avalonia 界面，持续完善中 | 无（发布包已自带 .NET 运行时） |
| 其他操作系统 | ❌ 不支持 | / |

**✅ 完整支持**：尽可能提供一切相关支持，但必须确保启动器为最新版本。

**⚠️ 理论能跑，不保证可用**：PCL 詩音应该可以在这些平台上运行，但不保证功能完全可用。

**❌ 不支持**：PCL 詩音在这些平台的可用性较低，甚至压根打不开。请升级操作系统后使用。

**🧪 Avalonia 界面，持续完善中**：macOS 与 Linux 使用基于 Avalonia 的跨平台界面，可以在 [Releases](https://github.com/Mar7thLover/PCL-Shion/releases) 下载对应平台的 `PCL_Shion_*_osx-*.tar.gz` 或 `PCL_Shion_*_linux-*.tar.gz`。这套界面由 WPF 版移植而来，部分功能可能尚未完全对齐 Windows 版，遇到问题欢迎反馈。Windows 用户也可以下载 `PCL_Shion_*_win-*.exe` 体验 Avalonia 界面，它与 WPF 版的 `PCL_Shion_*_x64.exe` / `PCL_Shion_*_ARM64.exe` 互不冲突。macOS 版没有经过 Apple 公证，首次打开时需要在 Finder 中右键选择“打开”。

**注**：仅对最新版本的启动器提供支持。

## 📦 下载哪个文件

在 [Releases](https://github.com/Mar7thLover/PCL-Shion/releases/latest) 页面的 Assets 中按下表选择：

| 系统 | 推荐下载 | 说明 |
|---|---|---|
| Windows x64 | `PCL_Shion_Release_x64.exe` | WPF 界面，功能最完整，需要 [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows ARM64 | `PCL_Shion_Release_ARM64.exe` | 同上 |
| Windows（尝鲜） | `PCL_Shion_Release_win-x64.exe` / `win-arm64.exe` | Avalonia 新界面，自带运行时；主题配色、渲染后端等新功能只在这一版中提供 |
| macOS | `PCL_Shion_Release_osx-arm64.tar.gz`（Apple 芯片）/ `osx-x64.tar.gz`（Intel） | Avalonia 界面；首次打开需在 Finder 中右键选择“打开” |
| Linux | `PCL_Shion_Release_linux-x64.tar.gz` / `linux-arm64.tar.gz` | Avalonia 界面 |

`.sigstore.json` 是对应文件的签名，普通用户不需要下载。

## 🔒 许可证

- `Plain Craft Launcher 2/` 使用 [自定义许可证](Plain%20Craft%20Launcher%202/LICENCE)
- `其余所有目录` 使用 [Apache License 2.0](LICENSE)

## 🙏 致谢

- [龙腾猫跃](https://github.com/LTCatt) 与 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL)：PCL 的原作者与原项目。[赞助龙腾猫跃](https://afdian.com/a/LTCat)
- [PCL-Community](https://github.com/PCL-Community) 与 [PCL CE](https://github.com/PCL-Community/PCL-CE)：本项目所基于的社区版及其全体贡献者。

## 🌟 统计数据

[![Star History Chart](https://api.star-history.com/svg?repos=Mar7thLover/PCL-Shion&type=Date)](https://www.star-history.com/#Mar7thLover/PCL-Shion&Date)

## ❤️ 贡献者

[![](https://contrib.rocks/image?repo=Mar7thLover/PCL-Shion)](https://github.com/Mar7thLover/PCL-Shion/graphs/contributors)
