**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

# PCL 詩音（Shion）

[![Stars](https://img.shields.io/github/stars/Mar7thLover/PCL-CE?style=for-the-badge&label=stars&labelColor=444444&color=eac54f&logo=github)](https://github.com/Mar7thLover/PCL-CE/)
[![Last commit](https://img.shields.io/github/last-commit/Mar7thLover/PCL-CE/dev?style=for-the-badge&label=last%20commit&labelColor=444444&logo=github)](https://github.com/Mar7thLover/PCL-CE/commits/dev)
[![Issues](https://img.shields.io/github/issues/Mar7thLover/PCL-CE?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-CE/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/Mar7thLover/PCL-CE?style=for-the-badge&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-CE/pulls)

[下载](https://github.com/Mar7thLover/PCL-CE/releases) |
[提交问题或建议](https://github.com/Mar7thLover/PCL-CE/issues/new/choose) |
[贡献指南](CONTRIBUTING.md)

</div>

PCL 詩音（Shion）是基于 [PCL 社区版（PCL CE）](https://github.com/PCL-Community/PCL-CE) 独立进行二次开发的**第三方修改版**，PCL CE 又源自龙腾猫跃的 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL)。

本项目与 PCL 官方、PCL CE 社区均无隶属关系，**请不要向官方仓库或 PCL CE 社区反馈本项目的问题**，有问题请在 [本仓库的 Issues](https://github.com/Mar7thLover/PCL-CE/issues) 提出。

> [!WARNING]
> 本项目正处于**密集开发阶段**，功能和界面变动频繁，可能出现不稳定的情况。

## ✨ 与 PCL CE 的不同

### 🌏 移除区域限制，并承诺不会重新引入

上游 PCL CE 在 [#3673](https://github.com/PCL-Community/PCL-CE/pull/3673) 和 [#3676](https://github.com/PCL-Community/PCL-CE/pull/3676) 中，根据系统地区、时区和语言判断用户是否位于中国大陆，并据此禁用 Minecraft 本体、整合包的下载、安装与补全。

PCL 詩音已完整移除这套限制，所有地区的用户都可以正常使用下载与安装功能。**我们承诺不会重新引入任何基于地区的功能限制。**

### 🚀 锐意开发，欢迎新功能建议

上游 PCL CE 已进入维护阶段，不再受理新功能请求；PCL 詩音则会持续开发新功能，**欢迎任何合理的新功能建议**，请直接 [提交 Issue](https://github.com/Mar7thLover/PCL-CE/issues/new/choose)。

## 🗺️ 开发计划

- **跨平台 UI 框架**：准备用新的跨平台 UI 框架逐步取代现有仅支持 Windows 的 WPF。迁移会渐进进行，新界面先作为实验性功能提供，成熟后再成为默认。
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
| macOS / Linux / 其他操作系统 | ⚠️ 仅跨平台开发支持（交叉编译） | [.NET 10 SDK](https://get.dot.net/10) |

**✅ 完整支持**：尽可能提供一切相关支持，但必须确保启动器为最新版本。

**⚠️ 理论能跑，不保证可用**：PCL 詩音应该可以在这些平台上运行，但不保证功能完全可用。

**❌ 不支持**：PCL 詩音在这些平台的可用性较低，甚至压根打不开。请升级操作系统后使用。

**⚠️ 仅跨平台开发支持（交叉编译）**：源代码可以在 macOS 与 Linux 平台编译，但无法直接运行。你可以在这些平台上开发，再把编译产物转移到 Windows 系统测试。随着跨平台 UI 框架的引入，这一情况会逐步改变。

**注**：仅对最新版本的启动器提供支持。

## 🔒 许可证

- `Plain Craft Launcher 2/` 使用 [自定义许可证](Plain%20Craft%20Launcher%202/LICENCE)
- `其余所有目录` 使用 [Apache License 2.0](LICENSE)

## 🙏 致谢

- [龙腾猫跃](https://github.com/LTCatt) 与 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL)：PCL 的原作者与原项目。[赞助龙腾猫跃](https://afdian.com/a/LTCat)
- [PCL-Community](https://github.com/PCL-Community) 与 [PCL CE](https://github.com/PCL-Community/PCL-CE)：本项目所基于的社区版及其全体贡献者。

## 🌟 统计数据

[![Star History Chart](https://api.star-history.com/svg?repos=Mar7thLover/PCL-CE&type=Date)](https://www.star-history.com/#Mar7thLover/PCL-CE&Date)

## ❤️ 贡献者

[![](https://contrib.rocks/image?repo=Mar7thLover/PCL-CE)](https://github.com/Mar7thLover/PCL-CE/graphs/contributors)
