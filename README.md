**简体中文** | [English](README-EN.md) | [繁體中文](README-ZH_TW.md)

<div align="center">

<img src="PCL.Shion/Assets/Images/icon.png" alt="Logo" width="80" height="80">

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

- **统一跨平台界面**：Windows、macOS、Linux 均以 Avalonia 为正式入口；WPF 仅保留历史源码供迁移参考，不再参与默认构建和发布。持续补齐功能、改善稳定性。
- **主题与个性化**：引入更丰富的主题与个性化选项。
- **有趣的小工具**：加入更多实用又好玩的小工具。
- **新的联机方案**：考虑引入不依赖 Natayark OpenID 的联机方案，作为现有方案的替代选择。
- **……以及更多**：更多计划会在开发过程中陆续公布，也欢迎提出你的想法。

## 💻 支持平台

从本次迁移后的构建开始，三个平台统一使用 **Avalonia + .NET 10**。所有发布包均为 **framework-dependent**，不再自包含 .NET 运行时。

| 操作系统 | 界面与支持范围 | 环境要求 |
|---|---|---|
| Windows 11（x64 / ARM64） | 正式 Avalonia 界面 | 对应架构的 [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Windows 10 22H2（x64） | 尽力兼容，受系统与框架支持周期限制 | 同上 |
| macOS 15 或更高（Intel / Apple 芯片） | 正式 Avalonia 界面 | 对应架构的 .NET 10 Runtime |
| Linux（x64 / ARM64，glibc） | 正式 Avalonia 界面，使用 X11 / XWayland 桌面 | .NET 10 Runtime 和系统图形依赖 |
| 更早或其他操作系统 | 不在当前验证范围内 | 参考下方框架支持说明 |

安装普通 **.NET Runtime** 即可；不需要 Windows Desktop Runtime。已有同架构的 .NET 10 SDK 也可运行。可用 `dotnet --list-runtimes` 确认安装了 `Microsoft.NETCore.App 10.x`。运行时架构必须与下载包一致，例如 Apple 芯片选择 ARM64 Runtime 和 AppleSilicon 包。

具体系统版本与 Linux 依赖以 [.NET 10 支持列表](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)、[Avalonia 支持平台](https://docs.avaloniaui.net/docs/supported-platforms)及 [Linux 部署说明](https://docs.avaloniaui.net/docs/platform-specific-guides/linux)为准。项目仍处于密集开发阶段，CI 构建与自动化测试不代替每个平台的真实游戏启动验收。

## 📦 下载哪个文件

在 [Releases](https://github.com/Mar7thLover/PCL-Shion/releases/latest) 页面的 Assets 中按下表选择；Beta 使用相同命名，把 `Release` 换成 `Beta`。所有包均需预先安装 .NET 10 Runtime。

| 系统 | 文件名 |
|---|---|
| Windows x64 | `PCL_Shion_Release_Avalonia_Windows-x64.exe` |
| Windows ARM64 | `PCL_Shion_Release_Avalonia_Windows-ARM64.exe` |
| macOS Apple 芯片 | `PCL_Shion_Release_Avalonia_macOS-AppleSilicon.tar.gz` |
| macOS Intel | `PCL_Shion_Release_Avalonia_macOS-Intel.tar.gz` |
| Linux x64 | `PCL_Shion_Release_Avalonia_Linux-x64.tar.gz` |
| Linux ARM64 | `PCL_Shion_Release_Avalonia_Linux-ARM64.tar.gz` |

Windows 为单个可执行文件；Linux 解压后运行 `PCL.Shion`；macOS 解压后打开 `PCL Shion.app`。macOS 包未经过 Apple 公证；首次打开如被系统拦截，在确认下载来源后，按 [Apple 官方说明](https://support.apple.com/en-us/102445)进入“系统设置 → 隐私与安全性 → 仍要打开”。发布同时提供 `SHA256SUMS` 与 Sigstore 签名，包大小以实际资产为准。

### 从旧版升级

1. 安装与下载包同架构的 .NET 10 Runtime，再运行新版本。
2. 从旧 Avalonia 版本升级可沿用现有 Avalonia 数据。WPF 的设置与账号此前独立保存，不能直接覆盖到 Avalonia；请保留旧目录，在新界面重新添加原有游戏目录并登录账号，原有存档和模组无需删除。
3. 历史 WPF 和旧版自包含资产仍按当时的说明使用；后续构建只提供 Avalonia，不再生成 WPF 发行包。

### 已知功能差异

Avalonia 已成为默认入口，但与历史 WPF 版尚未完全对齐。**启动脚本导出、测试模式启动暂未实现**，界面保留禁用按钮和说明；正常游戏启动请使用启动页。其余验收边界及测试结果见[验证记录](docs/validation.md)。

## 🛠️ 构建

安装 .NET 10 SDK 后执行：

```bash
dotnet build PCL.Shion.slnx -c Release
dotnet test PCL.Shion.Core.Test/PCL.Shion.Core.Test.csproj -c Release
dotnet test PCL.Shion.Test/PCL.Shion.Test.csproj -c Release
```

发行构建、包检查与上游整合规范见 [贡献指南](CONTRIBUTING.md)。历史 WPF 工程位于 [legacy/PCL.Wpf.slnx](legacy/PCL.Wpf.slnx)。

[上游整合表](docs/upstream-integration.md) · [验证记录](docs/validation.md)

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
