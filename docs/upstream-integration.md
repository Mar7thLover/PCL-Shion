# 上游需求与稳定性整合记录

调研日期：2026-10-25（America/Los_Angeles）。对照基线：`Mar7thLover/PCL-Shion` 的 `dev`，提交 `9fb1c0144a7595e7770a8119574784a373f503f1`。

最终验证日期：2026-10-26（UTC）。本次交付通过 908 项 Core 测试和 31 项应用测试，共 939 项；六个 RID 的发布验证通过，发布产物不包含 .NET 运行时。验证环境、命令、产物检查和剩余限制见[验证记录](validation.md)。

GitHub 仓库元数据的 `parent` 与 `source` 均为 [PCL-Community/PCL-CE](https://github.com/PCL-Community/PCL-CE)。本仓库 README 说明更早来源是 [Meloong-Git/PCL](https://github.com/Meloong-Git/PCL)。以下优先检查直接上游的实际 issue、PR 内容与状态，再与 Shion 的 Avalonia 实现比对；没有把 WPF 修复自动视作新前端的同一个缺陷，也没有机械合并上游分支。

## 筛选表

| 来源 | 调研时上游状态 | 需求 / 缺陷与取舍 | Shion 整合位置 / 验收 | 本次状态 |
|---|---|---|---|---|
| [#3029](https://github.com/PCL-Community/PCL-CE/issues/3029) | Open | 上游明确提出 Avalonia 迁移及移除 WPF。Shion 已有跨平台前端，适合把它升级为所有平台的正式构建入口。取消自包含运行时是本次用户的额外要求，不是该 issue 的原始要求。 | 解决方案、CI、发布脚本、三语 README；正式包只包含 Avalonia 应用及其依赖，使用系统 .NET Runtime。 | 已完成正式构建入口切换；Avalonia 构建与六 RID 发布验证通过，平台运行验收范围见验证记录。 |
| [PR #3441](https://github.com/PCL-Community/PCL-CE/pull/3441) | Merged，2026-10-04 | 上游对病态 Jar-in-Jar 使用全局节点预算。Shion 的 `ModArchiveReader` 有无界递归、无界 `CopyTo` 和 `ReadToEnd`，直接影响内存与响应。采用预算和容错原则；不声称移植该 PR 的全部依赖图、缓存与级联操作。 | `PCL.Shion.Core/Mods`；新增深层嵌套、宽树、超大元数据 / 嵌套条目、预算累计、坏文件隔离、TOML 深度回归。 | 已实现所选预算与容错修复；相关回归通过。 |
| [#3701](https://github.com/PCL-Community/PCL-CE/issues/3701) | Open，正在处理 | 用户要求在解析卡住时关闭模组解析，以便禁用或删除问题文件。新增可持久化开关，并将取消传入哈希、解压与扫描循环。 | 正式实例资源页 `PageInstanceCompResource`、`Compat/ModLocalComp` 和备用 `ModsViewModel`；关闭解析后仍能列出、启停、删除文件，刷新 / 离开页面取消旧任务。 | 已实现；关闭解析同时跳过文件哈希，加载中提供快速回退按钮。 |
| [#3666](https://github.com/PCL-Community/PCL-CE/issues/3666) | Open，等待处理 | 安装中的整合包会被误判成损坏实例。为安装过程增加明确生命周期，扫描时排除未完成实例。 | `InstallationSession` 接入原版 / 加载器安装；`InstanceScanner` 在安装中隐藏实例，取消 / 失败释放状态。 | 部分实现：原版 / 加载器安装生命周期及相关回归已完成；当前 Avalonia 整合包导入尚不完整，不声称完整解决上游整合包 issue。 |
| [PR #3506](https://github.com/PCL-Community/PCL-CE/pull/3506)、[#2716](https://github.com/PCL-Community/PCL-CE/issues/2716) | PR 已合并；issue Open | 采用有界资源使用、流式写盘、校验后提交及批量失败隔离原则。Shion 已有部分能力，重点修复实测到的边界。#2716 是模组更新 UX 诉求，下载器改进不等于完整实现模组更新界面。 | `DownloadEngine` 新增有界任务调度、跨批次目标锁、取消 / Dispose 收尾、超限早停；新增单文件 403 不阻断其余项的回归。 | 已实现上述下载器边界修复并通过回归；不移植上游整个动态 Range 下载器，在线模组更新界面仍属部分实现。 |
| [#3535](https://github.com/PCL-Community/PCL-CE/issues/3535) | Closed / completed | 只从列表移除游戏目录，保留磁盘文件。上游评论说明其已有实现，Shion `SettingsViewModel.RemoveFolder` 也仅修改配置列表。 | 保留现有非破坏性行为；迁移不能把“移除”替换成目录删除。 | 基线已具备；不重复实现。 |
| [#3603](https://github.com/PCL-Community/PCL-CE/issues/3603) | Closed / completed | NeoForge 临时目录没有尾分隔符，裸字符串拼接出错误路径。Shion 新的 Forge 安装器主要使用 `Path.Combine`，不应回引 WPF 路径拼接逻辑。 | 保留跨平台路径组合；安装稳定性验证应包含带空格、不带尾分隔符的根目录。 | 基线已具备主要路径组合；本次保留现有行为，不宣称复制了上游补丁。 |
| [#3548](https://github.com/PCL-Community/PCL-CE/issues/3548)、[PR #3710](https://github.com/PCL-Community/PCL-CE/pull/3710) | Issue completed；PR merged，2026-10-24 | 按实例禁止模组更新可以保护整合包。Shion 兼容层的在线更新检查仍有未接入部分，单加一个未强制执行的设置会产生误导。 | 应在所有更新入口一致执行限制，并测试更新单个 / 全部 / 自动依赖路径。 | 延后完整集成；本次不添加没有执行保障的开关。 |
| [#3707](https://github.com/PCL-Community/PCL-CE/issues/3707)、[#3628](https://github.com/PCL-Community/PCL-CE/issues/3628) | 均 Open | 同名更新后元数据陈旧，以及外部改名为 `.disabled` 后状态陈旧。Shion 需基于真实文件重新扫描、避免旧扫描结果覆盖新状态；上游的在线资源更新缓存症状还需其更新链完整后单独验证。 | 模组刷新与取消、只移除末尾禁用后缀、`.old` / `.disabled` 状态一致性；测试外部改名与坏文件仍可管理。 | 部分实现：本地状态修复及相关回归已完成；在线更新问题不标记为已完全修复。 |
| [PR #3627](https://github.com/PCL-Community/PCL-CE/pull/3627) | Merged，2026-09-30 | 上游修复 Windows DPAPI 密钥不可用导致档案写入失败。Shion 新档案存储并不使用同一 DPAPI 代码，不能移植 Windows 专用修复并声称跨平台等价。 | 本次独立加强配置 / 档案的原子写入与错误保护；跨平台凭据保护需单独设计与迁移验证。 | 不合并该 DPAPI 补丁；持久化改进单独记录。 |

## 明确排除

- [#3673](https://github.com/PCL-Community/PCL-CE/pull/3673)、[#3676](https://github.com/PCL-Community/PCL-CE/pull/3676)：地区限制与本仓库公开定位相冲突，不整合。
- [PR #3680](https://github.com/PCL-Community/PCL-CE/pull/3680)：已核实其修复 WPF 下载文件名扩展名位置及空文件名。只有确认 Avalonia 对应命名函数存在同样缺陷时才适合移植，不能当作本次已修复成果。
- [PR #3640](https://github.com/PCL-Community/PCL-CE/pull/3640)：MultiMC / Prism 补丁、Java 版本集合、LWJGL 冲突修复价值较高，但需要独立的格式兼容测试，避免为了正式切换前端而扩大未验证的安装语义变更。

## 验证与完成口径

“已实现”表示本次选择的实现范围已经完成并通过相应自动化验证；“基线已具备”表示原有代码已经存在相应行为；“部分实现”与“延后”明确保留尚未覆盖的上游需求。表内上游状态是调研时的记录，本地实现不代表上游 issue 已关闭。最终验证结果以[验证记录](validation.md)中的命令、测试结果和平台限制为准。六 RID 发布通过不代表 Windows / macOS 手工启动、真实微软登录和所有整合包均已经验证。

### 模组扫描的边界

- 单个 Mod 的 Jar-in-Jar 深度最多 8 层、最多 256 个嵌套 Jar；整棵树共用 64 MiB 解压预算。
- 单份元数据最多 1 MiB，单个嵌套 Jar 最多 32 MiB；按实际读取量检查，不只依赖 ZIP 声明的大小。
- TOML 数组和内联表嵌套最多 64 层。超过预算或损坏的元数据保留文件行并提示信息不完整；不会因单个文件失败清空列表。
- 不完整扫描不产生确定的“缺少前置”结论，防止被跳过的内嵌依赖造成误报。
- 解析关闭时不打开 Jar、不计算 SHA-1。该模式有意隐藏依赖和在线关联信息，启用 / 禁用 / 删除仍可使用。

### 本地审查额外发现的加载器竞态

这些问题来自本仓库代码审查，未冒充任何上游 issue：

- `LoaderTask.Start` 原先在 `Task.Run` 之后才赋值 `lastRunningTask`，极短任务可能把自己误判为已取消。现改为先构造并发布任务，再调度执行。
- 每轮加载有独立输入、输出、取消源与执行上下文。强制刷新后，旧任务只能读取自己的输入和令牌，不能覆盖新任务的结果、进度、错误或状态。
- 任务的取消源在执行结束后释放，`OperationCanceledException` 进入取消状态；失败刷新保留上次成功结果。
- `PreviewFinish` 需要在 UI 读取结果，因此在确认当前任务后先发布结果，再调用预览回调。页面回调再次核对任务身份；皮肤刷新等到该轮成功完成，并在 UI 上再次检查账号对应的任务。

`PCL.Shion.Test/LoaderTaskTest.cs` 覆盖同步快速完成、强制重启隔离、取消与资源释放、取消异常状态、失败后重试，以及跨执行上下文读取预览结果。尚未把真实账号切换的可视化验收宣称为自动测试覆盖。
