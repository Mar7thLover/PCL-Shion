# WPF 历史代码

`PCL.Wpf.slnx` 仅用于查阅、比较和迁移历史 WPF 实现，不参与 Shion 的默认构建、CI 或发布。正式入口是根目录 `PCL.Shion.slnx`，Windows、macOS、Linux 统一使用 Avalonia。

保留旧源码及其许可证以便追溯上游实现。`PCL.Core/App/Localization/Languages/` 的语言资源仍由 Avalonia 项目作为 XML 数据复用；这不引入 WPF 程序集或 Desktop Runtime 依赖。
