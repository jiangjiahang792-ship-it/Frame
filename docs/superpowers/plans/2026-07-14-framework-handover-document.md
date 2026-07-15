# TDJS-Vision 框架交接文档实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 基于当前源码输出一份覆盖框架代码分布、运行链路和新增节点完整流程的中文开发交接文档。

**Architecture:** 先从程序入口沿 `Solution → Process → NodeBase` 取证，再核对工具箱、两套节点工厂、订阅、序列化、设计器和工程文件注册点。最终新增独立 Markdown 交接文档，并在既有任务文件追加完成与验证记录，不修改业务代码。

**Tech Stack:** .NET Framework 4.8、C# 7.3、Windows Forms、Newtonsoft.Json、Markdown、PowerShell。

## Global Constraints

- 所有中文内容使用简体中文并以 UTF-8 保存。
- WinForms 控件只能在对应 `.Designer.cs` 中增加；本任务不修改窗体控件。
- 文档必须基于实际源码，不推测不存在的运行时插件或依赖注入能力。
- 不覆盖工作区中已有的未提交修改。
- 本任务只新增文档并追加任务记录，不修改业务源代码和测试代码。

---

### Task 1: 盘点框架代码和节点注册链路

**Files:**
- Read: `Program.cs`
- Read: `FormMain.cs`
- Read: `Solution.cs`
- Read: `Process.cs`
- Read: `ConfigHelper.cs`
- Read: `Node/INode.cs`
- Read: `Node/NodeBase.cs`
- Read: `Node/NodeFactory.cs`
- Read: `Node/NodeSubscription.cs`
- Read: `Forms/ProcessNew/ProcessEditPanel.cs`
- Read: `Forms/ProcessNew/ProcessFlowCanvas.cs`
- Read: `Forms/ProcessNew/FormNewProcessWizard.cs`
- Read: `ToolTreeView.xml`
- Read: `TDJS-Vision.csproj`

**Interfaces:**
- Consumes: 当前工作区源码和 `AGENTS.md` 用户规则。
- Produces: 已核实的启动、持久化、节点创建、订阅、执行和资源释放链路。

- [x] **Step 1: 检索核心类型和方法**

```powershell
rg -n "NodeFactory|CreateNode|NodeType|NodeSubscription|SolSave|SolLoad|RunConnectedNodes" --glob "*.cs"
```

Expected: 找到两套节点创建映射、方案保存加载和流程图执行入口。

- [x] **Step 2: 对比四个节点注册集合**

```powershell
# 对比 NodeType、ToolTreeView.xml、NodeFactory 和 ProcessEditPanel 的类型集合。
```

Expected: 当前为 73 个枚举值、64 个工具箱叶节点、两套各 67 个工厂映射，且两套映射一致。

### Task 2: 编写正式交接文档

**Files:**
- Create: `docs/TDJS-Vision框架代码分布与新增节点开发交接文档.md`

**Interfaces:**
- Consumes: Task 1 的源码取证结果。
- Produces: 后续开发人员可直接使用的代码分布、节点开发步骤、代码骨架、测试门禁和排错表。

- [x] **Step 1: 编写代码分布和运行架构**

覆盖根目录、`Node`、`Forms`、`Device`、资源、测试、启动、方案加载、方案调度和流程调度。

- [x] **Step 2: 编写新增节点完整流程**

覆盖契约设计、文件结构、枚举、参数、结果、参数窗体、Designer、运行类、两套工厂、工具箱、`.csproj`、多语言、适配点和测试。

- [x] **Step 3: 编写风险、性能、故障定位和提交检查表**

明确多态类型名兼容、结果显示名兼容、当前 RunId、防旧结果、资源释放和性能门禁。

### Task 3: 记录并验证文档

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`
- Verify: `docs/TDJS-Vision框架代码分布与新增节点开发交接文档.md`

**Interfaces:**
- Consumes: 正式交接文档。
- Produces: 可追踪的任务记录和无格式错误的最终文档。

- [x] **Step 1: 追加任务记录**

记录任务目标、输出文件、内容范围、代码影响和验证结果。

- [x] **Step 2: 核对关键路径和注册点**

```powershell
rg -n "ProcessEditPanel|NodeFactory|ToolTreeView.xml|TDJS-Vision.csproj|Designer.cs|PolyConverter" "docs/TDJS-Vision框架代码分布与新增节点开发交接文档.md"
```

Expected: 所有强制注册点均在文档中出现。

- [x] **Step 3: 检查 UTF-8、占位词和差异格式**

```powershell
rg -n "TBD|TODO|稍后补充" "docs/TDJS-Vision框架代码分布与新增节点开发交接文档.md"
git diff --check
```

Expected: 无占位词；`git diff --check` 返回 0。
