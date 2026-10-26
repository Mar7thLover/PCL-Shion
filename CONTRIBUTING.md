# 如何为项目做贡献

## 当前构建入口

Windows、macOS、Linux 统一开发和发布 `PCL.Shion`（Avalonia）。安装 .NET 10 SDK，仓库的 `global.json` 允许使用同一主版本内较新的稳定 SDK：

```bash
dotnet build PCL.Shion.slnx -c Release
dotnet test PCL.Shion.Core.Test/PCL.Shion.Core.Test.csproj -c Release --no-build
dotnet test PCL.Shion.Test/PCL.Shion.Test.csproj -c Release --no-build
```

`legacy/PCL.Wpf.slnx` 保留旧版 WPF 的开发参考，不参与默认 CI 和正式发布。修改 `PCL.Core.SourceGenerators` 或共享语言文件也会触发 Avalonia 的验证流程。

## 发布与瘦身

所有平台使用 **framework-dependent** 发布，依赖普通 .NET 10 Runtime。禁止重新启用 `SelfContained` / `PublishSelfContained`，也不要用未经验证的程序集裁剪换取包体积。单文件中仍需包含应用及 Avalonia/Skia 所需的本机库。

以下以 Linux x64 为例；其他 RID 为 `win-x64`、`win-arm64`、`linux-arm64`、`osx-x64`、`osx-arm64`：

```bash
dotnet publish PCL.Shion/PCL.Shion.csproj -c Release -r linux-x64 \
  --self-contained false -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:DebugType=none -p:DebugSymbols=false -o ./publish
python scripts/verify-publish.py --publish-dir ./publish --rid linux-x64 \
  --runtime-config PCL.Shion/bin/Release/net10.0/linux-x64/PCL.Shion.runtimeconfig.json
```

检查脚本验证单文件打包前的 runtimeconfig 只依赖 `Microsoft.NETCore.App`，且发布目录没有被遗漏的依赖文件。每次发布应使用干净的输出目录；包体积由实际产物记录，不使用历史估算值。

`Shion Build (CI)` 在 Windows、Linux、macOS 上构建、测试并检查发布包。`Release publish` 先通过这些检查，再生成六个 RID 的正式产物和 SHA-256 校验文件，最后发布带签名的版本；不会生成 WPF 资产。实际窗口渲染、账号登录、安装与游戏启动仍需在对应平台做验收，headless 测试不等同于显卡、窗口系统和 Java 环境验证。

MirrorChyan 工作流只有在仓库变量 `MIRRORCHYAN_RID` 配置为本项目独立资源时才启用，并使用本仓库的 Avalonia 资产。不要填写上游 `PCL2-CE` 的资源标识。

## 上游整合与验收

- [上游功能请求与修复整合表](docs/upstream-integration.md)：来源、取舍与实现状态。
- [验证记录](docs/validation.md)：实际测试结果、包体积与未完成的跨平台验收项。

## 上游参考

Wiki 页面（沿用上游 PCL CE）:
[开发指南](https://github.com/PCL-Community/PCL2-CE/wiki/开发指南)
[技术规范](https://github.com/PCL-Community/PCL2-CE/wiki/技术规范)

<details>

<summary>原 CONTRIBUTING.md 内容</summary>

## 开始之前

查看 [Issues](https://github.com/Mar7thLover/PCL-Shion/issues) 寻找可以参与的任务，或创建新 Issue 讨论您的想法。

## 贡献流程

### 报告问题

1. 在提交 Issue 前，请先搜索是否已有相关 Issue
2. 使用提供的 Issue 模板
3. 包含以下信息：
   - 清晰的问题描述
   - 复现步骤（包括环境信息）
   - 预期与实际行为对比
   - 相关日志/截图（如有）

### 提交代码

1. Fork 仓库并克隆到本地

   ```bash
   git clone https://github.com/你的用户名/项目名称.git
   ```

2. 创建你的分支

   ```bash
   git checkout -b feat/your-feat-name
   # 或
   git checkout -b fix/issue-number-desc
   ```

3. 遵循项目代码风格进行编写
4. 提交更改，使用 Angular 规范提交信息

   ```bash
   git commit -m "<type>(scope): <subject>"
   ```

5. 推送分支到你的 Fork

   ```bash
   git push origin your-branch
   ```

6. 创建 Pull Request
   - 指向上游仓库的 `dev` 分支
   - 详细填写 PR 信息
   - 关联 Issue（如有）

## 开发规范

### 测试要求

- 提交前请在本地编译通过确保无误后提交

### Angular 规范

基本格式如下

```commit message
<type>(scope?): <subject>

<body>

<footer>
```

每次提交**必须包含页眉内容**，可以选用正文（`body`）和页脚（`footer`）

每次提交的信息不超过 `100` 个字符

#### 页眉（`header`）

页眉需包含提交类型（`type`）、作用域（`scope`，可选）和主题（`subject`）

##### 提交类型（`type`）

提交类型需指定为下面其中一个：

1. `build`：对构建系统或者外部依赖项进行修改
2. `chore`: 用于对非业务性代码进行修改，例如修改构建流程或者工具配置等
3. `ci`：对 CI 配置文件或脚本进行修改
4. `docs`：对文档进行修改
5. `feat`：增加新的特性
6. `fix`：修复 bug
7. `pref`：提高性能的代码更改
8. `refactor`：既不修复 bug 也不是添加特性的代码重构
9. `style`：不影响代码含义的修改，比如空格、格式化、缺失的分号等
10. `test`：增加缺失的测试或者修正已存在的测试

##### 作用域（`scope`）

范围可以是任何指定提交更改位置的内容

##### 主题（`subject`）

主题包括了对本次修改的简洁描述，有以下准则

1. 使用命令式与现在时态：`改变` 而不是 `已改变`，也不是 `改变了`
2. 不要大写首字母（若使用英文）
3. 不要在末尾添加句号

#### 正文（`body`）

同主题，使用命令式与现在时态

应包含修改的动机以及和之前行为的对比

#### 页脚（`footer`）

##### Breaking Changes

破坏性修改指的是本次提交使用了不兼容之前版本的 API 或者环境变量

所有不兼容修改都必须在页脚中作为破坏性修改提到，以 `BREAKING CHANGE:` 开头，后跟一个空格或者换行符，其余的信息就是对此次修改的描述、理由和注释

##### 引用完成的 Issue

如果本次提交目的是完成 Issue 的话，需在页脚引用该 Issue

以关键字 `Closes` 开头，如

```footer
Closes #1145
```

修改了多个 bug 以半角逗号和空格隔开

```footer
Closes #114, #514, #1919
```

#### 回滚（`revert`）

若此次提交包含回滚（`revert`）操作，那么页眉需以 `revert:` 开头，同时在正文中添加 `本次提交回滚到 commit <hash>`，其中 `<hash>` 值表示被回滚前的提交

```commit message
revert:<type>(<scope>): <subject>

本次提交回滚到 commit <hash>
<body>

<footer>
```

## 提交上游

若你要将他人在 CE 中实现的功能或修复提交到上游，请用 Co-authored 来指明原始贡献者

</details>
