# 参与开发

## 本地验证

BabyToys 需要 Windows 和 .NET 10 SDK。
仓库通过 `global.json` 固定已经验证的 SDK feature band，并允许使用该 feature band 的更新补丁。

```powershell
dotnet restore BabyToys.Tests/BabyToys.Tests.csproj
dotnet build BabyToys.Tests/BabyToys.Tests.csproj -c Release --no-restore
dotnet test --project BabyToys.Tests/BabyToys.Tests.csproj -c Release --no-build
```

涉及输入钩子、遮罩窗口、休眠或托盘的改动还必须执行 README 中的实机验证清单。

## 代码边界

- 窗口和显示逻辑放在 `MainWindow` 或 `Views`。
- 儿童模式生命周期与状态转换放在 `Sessions`。
- Win32、输入钩子、电源、配置、日志等系统能力放在 `Services`。
- 可计算的规则优先写成不依赖窗口和操作系统状态的策略类，并补充单元测试。
- 用户界面使用中文，代码标识符使用英文。
- 不记录具体按键、鼠标内容或用户输入。
- 新增钩子、计时器、窗口和系统资源时必须同时实现幂等清理路径。

## 提交和发布

提交主题采用 Conventional Commits，例如：

```text
feat: add configurable global hotkey
fix: restore input hooks after failed sleep request
test: cover settings normalization
ci: add Windows release workflow
```

发布前：

1. 更新 `CHANGELOG.md` 和 `BabyToys.csproj` 中的版本。
2. 在 Windows 10/11 实机执行 README 验证清单。
3. 确认本地 Release 构建、测试和发布脚本成功。
4. 合并并推送最终提交。
5. 在该提交创建匹配项目版本的标签，例如 `v1.2.0`。
6. 标签推送后由 GitHub Actions 创建 Release、ZIP 和 SHA-256 校验文件。

依赖还原使用仓库内的 `packages.lock.json`。更新测试 SDK 后应重新执行 `dotnet restore`，审阅并提交锁文件变化；CI 使用 locked mode，锁文件过期时会直接失败。

本地生成发布包：

```powershell
./scripts/Publish-Release.ps1
```
