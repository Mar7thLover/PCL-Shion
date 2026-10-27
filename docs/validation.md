# Avalonia 正式入口与稳定性改造：验证记录

验证日期：2026-10-26（UTC）。上游来源与取舍见[上游整合表](upstream-integration.md)。本文记录实际完成的验证，不把交叉编译或 headless 测试等同于所有平台的人工验收。

## 验证环境与基线

| 项目 | 记录 |
|---|---|
| 基线分支 / 提交 | `dev` / `9fb1c0144a7595e7770a8119574784a373f503f1` |
| 本地宿主 | Linux x64 |
| .NET SDK | `10.0.403` |
| 配置 | `Release` |
| 正式解决方案 | `PCL.Shion.slnx` |
| 测试结果文件 | MSTest TRX；CI 分别保存 Core 与应用测试结果 |

新的默认解决方案只构建 Avalonia、跨平台 Core、源生成器和两组测试。旧 WPF 解决方案移到 `legacy/PCL.Wpf.slnx`，源码与许可证保留用于追溯，不参与正式构建、CI 或发布。仍复用的旧语言文件按 XML 数据读取，不引入 WPF 程序集。

## 构建与自动化测试

| 检查 | 实际结果 |
|---|---|
| 完整解决方案 Release 构建 | 成功，0 错误、260 警告 |
| `PCL.Shion.Core.Test` | **908 / 908 通过**，0 失败、0 跳过 |
| `PCL.Shion.Test` | **31 / 31 通过**，0 失败、0 跳过 |
| .NET 测试合计 | **939 / 939 通过** |
| 发布替换脚本离线测试 | **11 / 11 通过**，包含多种 HTTP 状态子用例 |
| 六个 RID 的发布目录 / runtimeconfig 检查 | 全部通过 |
| 工作流 YAML、项目 XML 与 plist 结构检查 | 通过 |
| `git diff --check` | 通过 |

原始基线在同一 SDK 下也产生 260 条构建警告。本次没有把已有警告全部清理；“0 错误”不意味着代码不存在剩余问题。

新增与强化的回归覆盖以下行为：

- **下载与关闭**：有界工作线程、跨批次同目标串行提交、等待及校验期间取消、Dispose 收尾、文件大小超限提前停止、校验失败保留旧文件、单个 403 不阻断其余下载。
- **安装与路径**：安装中实例暂不进入扫描结果，失败 / 取消释放状态；重复安装被拒绝；损坏或锁定的单个配置不阻断其他实例；清单、资源和本机 ZIP 路径中的越界与非法输入被拒绝。
- **进程生命周期**：短进程最后一段日志被读完后再发出退出事件，启动 / 退出顺序稳定，观察者异常不阻断收尾，重复 Dispose 安全。
- **模组解析**：嵌套深度、节点数、元数据大小和累计解压预算，损坏 / 超大文件隔离，TOML 深度上限，取消扫描，尾部 `.disabled` / `.old` 处理，以及不完整元数据不产生确定性缺失依赖结论。
- **数据与诊断**：原子文件替换、备份恢复、锁定或权限失败时拒绝覆盖原配置、空集合与异常数值归一化、常见令牌字段和 Authorization 内容脱敏。
- **补丁和 UI 状态**：无效或空补丁不破坏原 JAR，不适用的 ZIP 不报告成功；后台任务重新启动后旧结果不能覆盖新结果；关闭窗口取消后台任务；登录弹窗的 Escape 路径只取消一次。

应用测试使用 Avalonia Headless 创建真实 `App` 和 `MainWindow`，加载主题、AXAML、控件树和布局，并检查部分设置及登录交互。这些测试没有显示原生窗口，也没有验证真实 GPU、窗口管理器、微软账号或 Java 游戏进程。

### 三平台 CI 发现的问题与修复

初次三平台 CI 的 Linux 任务全部通过，Windows 和 macOS 成功构建后暴露了额外边界，已据此修复并重新执行本地验证：

- Windows 在原子替换文件的瞬间，新读取可能短暂失败并退回旧备份。`JsonFileStore.Read` 现在与写入者使用相同的路径锁，避免本进程正常保存被误判为配置不可读；并发回归仍要求每次读到完整主文件。目录替换失败的测试同时接受文件系统的 IO 与拒绝访问错误，继续检查原目标保留及临时文件清理。
- macOS 的特殊目录测试原先依赖修改 `HOME`，与 [.NET 的系统原生目录行为](https://learn.microsoft.com/en-us/dotnet/core/compatibility/core-libraries/8.0/getfolderpath-unix)不符。公开目录 API 保持系统行为，测试通过内部解析器独立验证 `DoNotVerify`、缺失目录以及相对 / 空路径回退，不再修改进程环境或跳过 Windows。该组从 5 个用例扩展到 10 个。

远端三平台的每轮状态和日志保存在 [PR #1 的 Checks](https://github.com/Mar7thLover/PCL-Shion/pull/1/checks)。上表的 939 项为最终修复后的本地 Linux 结果；Windows 的 Unix 执行权限专属用例按平台条件跳过。

## 实测包体积

以下为单文件可执行程序的**未压缩大小**，包含应用和所需本机库，不含 .NET Runtime。六个 RID 均在 Linux x64 宿主上实际执行 `dotnet publish` 并通过 `scripts/verify-publish.py`。

| RID | 字节数 | MiB |
|---|---:|---:|
| `win-x64` | 35,655,560 | 34.00 |
| `win-arm64` | 34,275,222 | 32.69 |
| `linux-x64` | 30,587,117 | 29.17 |
| `linux-arm64` | 30,161,909 | 28.76 |
| `osx-x64` | 36,591,610 | 34.90 |
| `osx-arm64` | 36,680,794 | 34.98 |

在独立的原始基线工作区，使用同一 SDK、Linux x64 RID 和单文件 / 本机库参数，按原配置发布自包含版本，实测 **104,104,078 字节（99.28 MiB）**。改造后为 **30,587,117 字节（29.17 MiB）**，单个启动器产物缩小 **70.6%**。

该比较不包含单独安装的 .NET Runtime 所占空间，不代表整机总占用降低相同比例，也不是内存或启动耗时测试。最终 tar.gz、macOS `.app` 附带图标及 CI 注入版本信息后的大小会有所不同。

Windows 原生依赖包附带的 PDB 会绕过常规 `DebugSymbols=false`。发布目标现在显式排除这些符号文件；验证脚本拒绝任何意外的旁置文件，确保 Windows 仅移动单个 EXE 时不会遗漏依赖。

## 发布约束与验证流程

所有 RID 均采用 framework-dependent 单文件发布，`SelfContained` 与 `PublishSelfContained` 为 `false`，runtimeconfig 只引用普通 `Microsoft.NETCore.App 10.x`。用户须预装与下载包架构匹配的 **.NET 10 Runtime**；不要求 Windows Desktop Runtime，已安装相应架构 .NET 10 SDK 也可运行。

没有启用程序集裁剪：现有 JSON、AXAML 和兼容层使用反射，直接裁剪可能改变运行行为。项目在发布时拒绝重新开启自包含或未经支持的裁剪。

CI 定义了 Windows、Linux、macOS 三个宿主的构建、两组 .NET 测试、发布替换脚本离线测试以及发布目录检查。正式发布还要求六个 RID 打包、SHA-256 校验和 Sigstore 验证成功，然后生成草稿、上传资产并发布。替换既有版本仍需显式打开 `replace_existing`；删除旧发布或 tag 时，仅明确的资源 404 被视为不存在，权限错误、服务器错误或网络失败会终止流程。没有在本地调用真实的发布删除或正式发布操作。

这次本地完成的是六个 RID 的应用交叉发布。完整 macOS `.app` 组装、临时签名以及 GitHub Release 上传依赖工作流在对应环境执行，本地没有运行这些平台专属步骤。macOS 包尚无 Apple 公证；README 按 [Apple 的首次打开说明](https://support.apple.com/en-us/102445)提供入口。

## 复现命令

在仓库根目录执行，需 .NET 10 SDK 与 Python 3：

```bash
dotnet build PCL.Shion.slnx -c Release -m:1 -p:UseSharedCompilation=false
dotnet test PCL.Shion.Core.Test/PCL.Shion.Core.Test.csproj -c Release --no-build \
  --logger trx --results-directory ./TestResults/core
dotnet test PCL.Shion.Test/PCL.Shion.Test.csproj -c Release --no-build \
  --logger trx --results-directory ./TestResults/desktop
python scripts/test-release-replace.py
```

以下以 Linux x64 为例，每次使用新的空发布目录；更换 RID 可复现表中其他目标：

```bash
dotnet publish PCL.Shion/PCL.Shion.csproj -c Release -r linux-x64 \
  --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:DebugSymbols=false -p:UseSharedCompilation=false -o ./publish
python scripts/verify-publish.py --publish-dir ./publish --rid linux-x64 \
  --runtime-config PCL.Shion/bin/Release/net10.0/linux-x64/PCL.Shion.runtimeconfig.json
```

## 尚未完成的验收与功能边界

- **平台人工验收**：Windows / macOS 真实窗口、显示缩放、GPU 与系统文件对话框，以及真实微软登录、Minecraft / Java 启动、整合包和 EasyTier 联机端到端测试尚未完成。本地交叉发布成功不能替代这些检查。
- **WPF 功能对齐**：Avalonia 已取代正式构建和发行入口，但历史功能并非全部对齐。启动脚本导出、测试模式启动当前保留禁用控件和说明；运行时自定义 XAML、部分加载器、完整整合包导入和在线模组更新仍有待补齐。本次不把这些缺口标记为已实现。
- **旧数据迁移**：旧 WPF 设置和账号不会自动覆盖到 Avalonia。用户应保留原目录，在新界面重新添加游戏目录并登录账号，存档和模组无需删除。
- **保证范围**：安装生命周期是进程内协调，不是多个启动器进程之间的互斥；清单路径检查不是完整的符号链接沙箱；日志脱敏覆盖常见字段，不保证识别任意自由文本中的所有秘密。
- **稳定性度量**：本次有明确故障边界和回归测试证据，但没有上线崩溃率、长期压力或真实用户遥测数据，不能量化宣称崩溃率下降多少。
