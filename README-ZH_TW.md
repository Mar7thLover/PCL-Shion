[简体中文](README.md) | [English](README-EN.md) | **繁體中文**

<div align="center">

<img src="PCL.Shion/Assets/Images/icon.png" alt="Logo" width="80" height="80">

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

- **統一跨平台介面**：Windows、macOS、Linux 均以 Avalonia 為正式入口；WPF 僅保留歷史原始碼供移植參考，不再參與預設建置與發布。持續補齊功能並改善穩定性。
- **主題與個人化**：引入更豐富的主題與個人化選項。
- **有趣的小工具**：加入更多實用又好玩的小工具。
- **新的連線方案**：考慮引入不依賴 Natayark OpenID 的連線方案，作為現有方案的替代選擇。
- **……以及更多**：更多計畫會在開發過程中陸續公布，也歡迎提出你的想法。

## 💻 支援平台

從本次移植後的建置開始，三個平台統一使用 **Avalonia + .NET 10**。所有發布包均採 **framework-dependent**，不再內含 .NET 執行環境。

| 作業系統 | 介面與支援範圍 | 環境需求 |
|---|---|---|
| Windows 11（x64 / ARM64） | 正式 Avalonia 介面 | 對應架構的 [.NET 10 Runtime](https://dotnet.microsoft.com/download/dotnet/10.0) |
| Windows 10 22H2（x64） | 盡力相容，受系統與框架支援週期限制 | 同上 |
| macOS 15 或更新版本（Intel / Apple 晶片） | 正式 Avalonia 介面 | 對應架構的 .NET 10 Runtime |
| Linux（x64 / ARM64，glibc） | 正式 Avalonia 介面，使用 X11 / XWayland 桌面 | .NET 10 Runtime 與系統圖形相依套件 |
| 更早或其他作業系統 | 不在目前驗證範圍內 | 參考下方框架支援說明 |

安裝一般 **.NET Runtime** 即可；不需要 Windows Desktop Runtime。已有相同架構的 .NET 10 SDK 也可執行。可用 `dotnet --list-runtimes` 確認已安裝 `Microsoft.NETCore.App 10.x`。執行環境架構必須與下載包一致，例如 Apple 晶片選擇 ARM64 Runtime 與 AppleSilicon 包。

詳細系統版本與 Linux 相依套件請參閱 [.NET 10 支援清單](https://github.com/dotnet/core/blob/main/release-notes/10.0/supported-os.md)、[Avalonia 支援平台](https://docs.avaloniaui.net/docs/supported-platforms)及 [Linux 部署說明](https://docs.avaloniaui.net/docs/platform-specific-guides/linux)。專案仍處密集開發階段，CI 建置與自動化測試不取代各平台的實際遊戲啟動驗收。

## 📦 下載哪個檔案

在 [Releases](https://github.com/Mar7thLover/PCL-Shion/releases/latest) 頁面的 Assets 中依下表選擇；Beta 使用相同命名，將 `Release` 換成 `Beta`。所有包均需預先安裝 .NET 10 Runtime。

| 系統 | 檔案名稱 |
|---|---|
| Windows x64 | `PCL_Shion_Release_Avalonia_Windows-x64.exe` |
| Windows ARM64 | `PCL_Shion_Release_Avalonia_Windows-ARM64.exe` |
| macOS Apple 晶片 | `PCL_Shion_Release_Avalonia_macOS-AppleSilicon.tar.gz` |
| macOS Intel | `PCL_Shion_Release_Avalonia_macOS-Intel.tar.gz` |
| Linux x64 | `PCL_Shion_Release_Avalonia_Linux-x64.tar.gz` |
| Linux ARM64 | `PCL_Shion_Release_Avalonia_Linux-ARM64.tar.gz` |

Windows 為單一執行檔；Linux 解壓縮後執行 `PCL.Shion`；macOS 解壓縮後開啟 `PCL Shion.app`。macOS 套件未經 Apple 公證；首次開啟若被系統阻擋，確認下載來源後，依 [Apple 官方說明](https://support.apple.com/en-us/102445)前往「系統設定 → 隱私權與安全性 → 強制打開」。發布同時提供 `SHA256SUMS` 與 Sigstore 簽章，套件大小以實際檔案為準。

### 從舊版升級

1. 安裝與下載包相同架構的 .NET 10 Runtime，再執行新版本。
2. 從舊 Avalonia 版本升級可沿用現有 Avalonia 資料。WPF 的設定與帳號先前獨立儲存，不能直接覆蓋至 Avalonia；請保留舊目錄，在新介面重新加入原有遊戲目錄並登入帳號，原有存檔與模組無需刪除。
3. 歷史 WPF 與舊版內含執行環境的檔案仍依當時說明使用；後續建置僅提供 Avalonia，不再產生 WPF 發布包。

### 已知功能差異

Avalonia 已成為預設入口，但尚未完整對齊歷史 WPF 版。**啟動指令碼匯出、測試模式啟動尚未實作**，介面保留停用按鈕與說明；一般遊戲啟動請使用啟動頁。其他驗收範圍與測試結果請參閱[驗證記錄](docs/validation.md)。

## 🛠️ 建置

安裝 .NET 10 SDK 後執行：

```bash
dotnet build PCL.Shion.slnx -c Release
dotnet test PCL.Shion.Core.Test/PCL.Shion.Core.Test.csproj -c Release
dotnet test PCL.Shion.Test/PCL.Shion.Test.csproj -c Release
```

發布建置與套件檢查請參閱[貢獻指南](CONTRIBUTING.md)。歷史 WPF 工程位於 [legacy/PCL.Wpf.slnx](legacy/PCL.Wpf.slnx)。

[上游整合表](docs/upstream-integration.md) · [驗證記錄](docs/validation.md)

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
