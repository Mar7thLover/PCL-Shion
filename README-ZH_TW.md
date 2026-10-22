[简体中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="Plain Craft Launcher 2/Images/icon.ico" alt="Logo" width="80" height="80">

# PCL 詩音（Shion）

[![Stars](https://img.shields.io/github/stars/Mar7thLover/PCL-Shion?style=for-the-badge&label=stars&labelColor=444444&color=eac54f&logo=github)](https://github.com/Mar7thLover/PCL-Shion/)
[![Last commit](https://img.shields.io/github/last-commit/Mar7thLover/PCL-Shion/dev?style=for-the-badge&label=last%20commit&labelColor=444444&logo=github)](https://github.com/Mar7thLover/PCL-Shion/commits/dev)
[![Issues](https://img.shields.io/github/issues/Mar7thLover/PCL-Shion?style=for-the-badge&label=issues&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-Shion/issues)
[![Pull requests](https://img.shields.io/github/issues-pr/Mar7thLover/PCL-Shion?style=for-the-badge&label=pull%20requests&labelColor=444444&color=1F883D&logo=github)](https://github.com/Mar7thLover/PCL-Shion/pulls)

[下載](https://github.com/Mar7thLover/PCL-Shion/releases) |
[回報問題或建議](https://github.com/Mar7thLover/PCL-Shion/issues/new/choose) |
[貢獻指南](CONTRIBUTING.md)

</div>

PCL 詩音（Shion）是基於 [PCL 社群版（PCL CE）](https://github.com/PCL-Community/PCL-CE) 獨立進行二次開發的**第三方修改版**，PCL CE 則源自龍騰貓躍的 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL)。

本專案與 PCL 官方、PCL CE 社群均無隸屬關係，**請不要向官方倉庫或 PCL CE 社群回報本專案的問題**，有問題請在 [本倉庫的 Issues](https://github.com/Mar7thLover/PCL-Shion/issues) 提出。

> [!WARNING]
> 本專案正處於**密集開發階段**，功能與介面變動頻繁，可能出現不穩定的情況。

## ✨ 與 PCL CE 的不同

### 🌏 移除區域限制，並承諾不會重新引入

上游 PCL CE 在 [#3673](https://github.com/PCL-Community/PCL-CE/pull/3673) 與 [#3676](https://github.com/PCL-Community/PCL-CE/pull/3676) 中，依據系統地區、時區與語言判斷使用者是否位於中國大陸，並據此停用 Minecraft 本體、模組包的下載、安裝與補全。

PCL 詩音已完整移除這套限制，所有地區的使用者都可以正常使用下載與安裝功能。**我們承諾不會重新引入任何基於地區的功能限制。**

### 🚀 銳意開發，歡迎新功能建議

上游 PCL CE 已進入維護階段，不再受理新功能請求；PCL 詩音則會持續開發新功能，**歡迎任何合理的新功能建議**，請直接 [提交 Issue](https://github.com/Mar7thLover/PCL-Shion/issues/new/choose)。

## 🗺️ 開發計畫

- **跨平台 UI 框架**：準備以新的跨平台 UI 框架逐步取代現有僅支援 Windows 的 WPF。遷移會漸進進行，新介面先作為實驗性功能提供，成熟後再成為預設。
- **主題與個人化**：引入更豐富的主題與個人化選項。
- **有趣的小工具**：加入更多實用又好玩的小工具。
- **新的連線方案**：考慮引入不依賴 Natayark OpenID 的連線方案，作為現有方案的替代選擇。
- **……以及更多**：更多計畫會在開發過程中陸續公布，也歡迎提出你的想法。

## 💻 支援平台

| 作業系統 | 支援情況 | 環境需求 |
|---|---|---|
| Windows 10 1809 (17763) 或更高 | ✅ 完整支援 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 8 - Windows 10 1809 (17763) | ⚠️ 理論可用，不保證可用 | [.NET 10 Desktop Runtime](https://get.dot.net/10) |
| Windows 7 或更舊版本 | ❌ 不支援 | / |
| macOS / Linux / 其他作業系統 | ⚠️ 僅跨平台開發支援（交叉編譯） | [.NET 10 SDK](https://get.dot.net/10) |

**✅ 完整支援**：盡可能提供一切相關支援，但必須確保啟動器為最新版本。

**⚠️ 理論可用，不保證可用**：PCL 詩音應該可以在這些平台上執行，但不保證功能完全可用。

**❌ 不支援**：PCL 詩音在這些平台的可用性較低，甚至根本無法開啟。請升級作業系統後使用。

**⚠️ 僅跨平台開發支援（交叉編譯）**：原始碼可以在 macOS 與 Linux 平台編譯，但無法直接執行。你可以在這些平台上開發，再把編譯產物轉移到 Windows 系統測試。隨著跨平台 UI 框架的引入，這一情況會逐步改變。

**註**：僅對最新版本的啟動器提供支援。

## 🔒 授權條款

- `Plain Craft Launcher 2/` 使用 [自訂授權條款](Plain%20Craft%20Launcher%202/LICENCE)
- `其餘所有目錄` 使用 [Apache License 2.0](LICENSE)

## 🙏 致謝

- [龍騰貓躍](https://github.com/LTCatt) 與 [Plain Craft Launcher](https://github.com/Meloong-Git/PCL)：PCL 的原作者與原專案。[贊助龍騰貓躍](https://afdian.com/a/LTCat)
- [PCL-Community](https://github.com/PCL-Community) 與 [PCL CE](https://github.com/PCL-Community/PCL-CE)：本專案所基於的社群版及其全體貢獻者。

## 🌟 統計資料

[![Star History Chart](https://api.star-history.com/svg?repos=Mar7thLover/PCL-Shion&type=Date)](https://www.star-history.com/#Mar7thLover/PCL-Shion&Date)

## ❤️ 貢獻者

[![](https://contrib.rocks/image?repo=Mar7thLover/PCL-Shion)](https://github.com/Mar7thLover/PCL-Shion/graphs/contributors)
