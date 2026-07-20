# FormMain 授权程序启动设计

## 目标

用户点击 `FormMain` 帮助菜单中的“授权”菜单项后，立即启动软件运行目录下的 `activate.exe`。当文件不存在或进程启动失败时，界面必须显示简体中文错误提示。

## 范围

- 保留现有 `授权ToolStripMenuItem` 控件，不新增 WinForm 控件。
- 在 `FormMain.Designer.cs` 中绑定菜单点击事件，确保设计器中的控件和事件关系可见。
- 新增轻量外部程序启动接口及默认实现，供 `FormMain` 调用。
- 增加自动化回归测试并更新项目任务记录。
- 不修改 `activate.exe`，不负责授权程序内部逻辑，也不等待授权程序退出。

## 设计

### 外部程序启动边界

新增 `IExternalProgramLauncher` 接口，仅负责启动指定可执行文件。新增 `ExternalProgramLauncher` 默认实现，内部使用 `ProcessStartInfo` 和 `Process.Start`，并把工作目录设置为可执行文件所在目录。

该接口将进程启动细节从窗体事件中隔离。后续若需要替换启动策略或增加其他外部工具，可以在不改动窗体业务逻辑的情况下扩展实现。

### 菜单点击流程

`授权ToolStripMenuItem` 的 `Click` 事件绑定到 `授权ToolStripMenuItem_Click`。处理流程如下：

1. 使用 `AppDomain.CurrentDomain.BaseDirectory` 获取当前软件运行目录。
2. 使用 `Path.Combine` 生成 `activate.exe` 的绝对路径。
3. 文件不存在时，显示“未找到授权程序：activate.exe”。
4. 文件存在时，通过 `IExternalProgramLauncher` 启动程序。
5. 捕获启动异常，显示“启动授权程序失败：{异常信息}”。

路径解析不使用进程当前工作目录，避免软件通过快捷方式或其他程序启动时找错文件。

### 生命周期与性能

`FormMain` 只持有一个不可变的启动器字段，不为每次点击重复创建服务对象。启动操作不等待子进程退出，点击事件可以立即返回，不阻塞主界面。

### 注释与界面约束

新增接口、类、字段、构造方法和事件处理方法均添加简体中文 XML 注释或必要的思路注释。所有错误提示使用简体中文。本次不新增控件，现有菜单控件继续保留在 `.Designer.cs` 文件中。

## 错误处理

- `activate.exe` 不存在：显示明确的中文缺失提示，不调用进程启动器。
- 进程启动异常：捕获 `Exception` 并显示中文失败提示及异常信息，避免异常终止主程序。
- 启动成功：不显示额外提示，不阻塞主窗体。

## 测试与验证

新增 PowerShell 回归测试，检查以下约束：

- “授权”菜单点击事件已在 `FormMain.Designer.cs` 中绑定。
- 点击处理方法使用运行目录和 `activate.exe` 组合绝对路径。
- 文件不存在和启动异常均有简体中文提示。
- `FormMain` 通过接口调用外部程序启动器。
- 默认实现使用 `ProcessStartInfo`，设置工作目录且不等待子进程退出。

实施时先运行新增测试并确认其因功能缺失而失败，再编写最小实现使其通过。最后运行相关脚本测试、`git diff --check` 和解决方案构建。
