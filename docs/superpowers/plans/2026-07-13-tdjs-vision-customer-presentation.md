# TDJS-Vision 客户介绍演示文稿实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 生成一份面向企业管理层与自动化、视觉工程师的 23 页简体中文 TDJS-Vision 客户介绍 PowerPoint，并完成逐页渲染验收。

**Architecture:** 以项目代码和配置文件建立事实清单；从可运行程序或 WinForms 设计器获取真实界面素材；使用 `@oai/artifact-tool` 的 JavaScript ES 模块生成可编辑 PPTX。制作过程在系统临时目录完成，仅将最终 PPTX、设计说明、实施计划和任务记录保留在项目中。

**Tech Stack:** PowerShell、.NET Framework 4.8、WinForms、JavaScript ES Modules、`@oai/artifact-tool`、演示文稿渲染与越界检查脚本。

## Global Constraints

- 所有客户可见文字必须使用简体中文。
- 不修改 TDJS-Vision 现有业务代码、WinForms 控件或方案文件。
- 内容以 `README.md`、`ToolTreeView.xml`、`FormMain*`、`Solution.cs`、`Process.cs`、`Device/**` 和 `Node/**` 为事实依据。
- 不承诺未经验证的精度、节拍、兼容型号数量、投资回报率或现场效果。
- 标题字号不低于 35 磅，正文不低于 16 磅；出现拥挤时优先删减或拆页。
- 最终 PPTX 输出到 `outputs/TDJS-Vision工业视觉应用框架客户介绍.pptx`。
- 制作工作区固定为 `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck`。

---

### Task 1: 建立事实与素材清单

**Files:**
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/source-notes.txt`
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/asset-ledger.txt`

**Interfaces:**
- Consumes: 项目源代码、配置和资源文件。
- Produces: 按“框架、相机、设备通信、视觉算法、流程、结果、应用”分类的事实清单和素材来源清单。

- [ ] **Step 1: 创建制作目录**

  创建 `tmp/slides`、`tmp/preview`、`tmp/layout`、`tmp/assets` 和 `tmp/qa` 目录。

- [ ] **Step 2: 提取已实现能力**

  从 `ToolTreeView.xml` 提取工具分类与节点名称，从设备目录提取相机、PLC、Modbus、TCP、串口和光源实现，从运行代码提取方案、流程、触发、并行和结果输出能力。

- [ ] **Step 3: 写入事实边界**

  在 `source-notes.txt` 中逐项区分“当前已实现”“需要现场型号与 SDK 联调”“典型应用方向”，并记录对应项目文件。

- [ ] **Step 4: 检查敏感信息**

  确认素材中没有客户名称、生产数据、相机序列号、IP 地址、账号或密码。

### Task 2: 获取软件真实界面素材

**Files:**
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/assets/software-main.png`
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/assets/process-canvas.png`
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/assets/device-management.png`
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/assets/result-view.png`

**Interfaces:**
- Consumes: 项目当前可执行程序、`FormMain.Designer.cs`、设备管理窗体和结果窗体。
- Produces: 不含敏感信息的 16:9 或可裁切界面截图；若程序无法运行，产出等价的设计器界面证据截图。

- [ ] **Step 1: 构建当前解决方案**

  Run: `C:/Program Files/Microsoft Visual Studio/2022/Community/MSBuild/Current/Bin/MSBuild.exe D:/MyCode/PublicWook/TDJS-Vision/TDJS-Vision.sln /t:Build /p:Configuration=Debug /p:Platform=x64 /p:RestorePackages=false /m`

  Expected: 构建成功并生成 TDJS-Vision 可执行程序；若因当前工作区既有修改失败，仅记录失败原因，不修改业务代码。

- [ ] **Step 2: 启动并截取主界面**

  使用空方案或仓库内演示方案启动软件，截取主界面、工具箱、流程画布、图像与结果区域。

- [ ] **Step 3: 截取设备与流程界面**

  获取相机、光源、PLC、Modbus、TCP 管理界面和节点连线流程；无法无设备打开的界面使用设计器预览或结构示意替代。

- [ ] **Step 4: 裁切并核对素材**

  确保截图清晰、无敏感信息、无调试弹窗，记录每张截图对应的页面用途。

### Task 3: 编写演示文稿内容与页面映射

**Files:**
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/slide-plan.txt`
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/copy-ledger.txt`

**Interfaces:**
- Consumes: 设计说明、事实清单和界面素材。
- Produces: 23 页逐页标题、主结论、正文、视觉类型和素材映射。

- [ ] **Step 1: 写出沟通任务**

  固定为：“演示结束后，管理层和工程师应理解 TDJS-Vision 能以可视化、可扩展的统一框架连接视觉算法与现场设备，从而更快构建、复用和维护工业视觉项目。”

- [ ] **Step 2: 完成 23 页逐页文案**

  每页只保留一个主要结论；标题采用结论式表达，正文控制为 3–6 个短要点或一张能力矩阵。

- [ ] **Step 3: 完成真实性复核**

  对相机厂商、3D、AI、无监督、大模型、并行流程和通信协议相关页面逐项对照 `source-notes.txt`。

- [ ] **Step 4: 完成素材映射**

  为每页指定真实截图、原生 PowerPoint 简图、能力矩阵或纯文字构图，禁止使用无来源的客户案例数据。

### Task 4: 生成可编辑 PPTX

**Files:**
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/build-deck.mjs`
- Create: `D:/MyCode/PublicWook/TDJS-Vision/outputs/TDJS-Vision工业视觉应用框架客户介绍.pptx`

**Interfaces:**
- Consumes: `slide-plan.txt`、截图资产、`Logo.png` 和 `@oai/artifact-tool`。
- Produces: 1280×720、23 页、可编辑的 PowerPoint 文件及每页 PNG、布局 JSON 和整套蒙太奇图。

- [ ] **Step 1: 初始化 artifact-tool 工作区**

  Run: `C:/Users/34652/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/bin/node.exe C:/Users/34652/.codex/plugins/cache/openai-primary-runtime/presentations/26.709.11516/skills/presentations/container_tools/setup_artifact_tool_workspace.mjs --workspace C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp`

  Expected: `tmp/node_modules/@oai/artifact-tool` 可被 ES 模块解析。

- [ ] **Step 2: 实现主题与公共组件**

  在 `build-deck.mjs` 定义 1280×720 画布、绿色工业科技配色、中文字体、页码、页眉规则、标题、正文、图片框和简图辅助函数。

- [ ] **Step 3: 实现 23 页内容**

  依次创建封面、问题、价值、主界面、架构、闭环、2D 相机、3D 相机、设备、通信、预处理、传统视觉、测量、AI、无监督与大模型、流程、扩展、结果、应用全景、行业场景、实施流程、合作价值和结束页。

- [ ] **Step 4: 导出预览与 PPTX**

  每页导出 PNG 和 layout JSON，同时导出 montage，最后导出 `TDJS-Vision工业视觉应用框架客户介绍.pptx`。

### Task 5: 逐页质量检查与修正

**Files:**
- Create: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/qa/qa-ledger.txt`
- Modify: `C:/Users/34652/AppData/Local/Temp/codex-presentations/manual-20260713-tdjs/customer-deck/tmp/build-deck.mjs`
- Modify: `D:/MyCode/PublicWook/TDJS-Vision/outputs/TDJS-Vision工业视觉应用框架客户介绍.pptx`

**Interfaces:**
- Consumes: 初版 PPTX、逐页 PNG、布局 JSON 和蒙太奇图。
- Produces: 无越界、无截断、无异常换行、无非预期重叠的最终 PPTX。

- [ ] **Step 1: 运行越界检查**

  Run: `C:/Users/34652/.cache/codex-runtimes/codex-primary-runtime/dependencies/python/python.exe C:/Users/34652/.codex/plugins/cache/openai-primary-runtime/presentations/26.709.11516/skills/presentations/container_tools/slides_test.py D:/MyCode/PublicWook/TDJS-Vision/outputs/TDJS-Vision工业视觉应用框架客户介绍.pptx`

  Expected: 不报告超出画布的对象。

- [ ] **Step 2: 检查整套节奏**

  查看 montage，确认配色、页眉、页码、标题层级和构图变化一致，且不存在连续多页重复版式。

- [ ] **Step 3: 逐页全尺寸检查**

  打开全部 23 张 PNG，逐页记录文字截断、标题换行、图片裁切、对齐、重叠和可读性问题。

- [ ] **Step 4: 修正并重新导出**

  修改 `build-deck.mjs`，重新生成 PPTX 和预览，直到 `qa-ledger.txt` 中所有问题关闭。

### Task 6: 记录任务并验收交付

**Files:**
- Create: `D:/MyCode/PublicWook/TDJS-Vision/PPT_CUSTOMER_PRESENTATION_TASKS.md`
- Verify: `D:/MyCode/PublicWook/TDJS-Vision/outputs/TDJS-Vision工业视觉应用框架客户介绍.pptx`

**Interfaces:**
- Consumes: 最终 PPTX 和 QA 记录。
- Produces: 项目任务记录和可交付文件。

- [ ] **Step 1: 写入任务记录**

  记录日期、目标、资料来源、输出路径、页数、已完成检查和未修改业务代码的说明。

- [ ] **Step 2: 最终文件检查**

  确认 PPTX 存在、文件大小大于 0、可重新渲染且页数为 23。

- [ ] **Step 3: 检查 Git 变更范围**

  确认只新增设计说明、实施计划、PPTX 和任务记录，不覆盖用户现有未提交修改。

- [ ] **Step 4: 交付**

  向用户提供最终 PPTX 的单个可点击链接，并简要说明内容范围和验证结果。
