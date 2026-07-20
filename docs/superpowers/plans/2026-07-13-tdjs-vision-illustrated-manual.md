# TDJS-Vision 图文增强版功能说明书实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 运行指定 Release 软件，逐项采集实际界面，为二维视觉、设备通信、逻辑工具和结果处理模块补充截图、操作方法与参数说明，并生成独立的图文增强版 Word 说明书。

**Architecture:** 使用 Windows UI Automation 和前台窗口截图完成软件遍历，原始截图按分类保存在文档素材目录，并维护截图清单。基于现有 Word 生成脚本插入“实际界面、图中区域说明、操作步骤和离线限制”，最后通过 Microsoft Word 导出 PDF 并逐页检查。

**Tech Stack:** C#/.NET Framework 4.8 WinForms 应用、PowerShell、Windows UI Automation、System.Drawing、Python 3、python-docx、Microsoft Word PDF 导出、Poppler。

## Global Constraints

- 软件从 `D:\MyCode\PublicWook\TDJS-Vision\bin\x64\Release\机器视觉AI检测系统V1.0.exe` 启动。
- 不绕过登录、授权、密码或硬件安全机制。
- 不伪造相机采集成功、PLC通信成功、AI推理成功或检测结果。
- 三维内容只覆盖三维相机连接、`3D图像源`和`3D图像显示`，并明确三维算法尚未集成。
- 保留原说明书，输出独立的图文增强版。
- 所有中文使用简体中文。
- 不修改现有业务源代码和窗体设计器。
- 每个执行批次更新 `TDJS_VISION_MANUAL_TASKS.md`。

---

### Task 1: 建立截图运行环境与清单

**Files:**
- Create: `docs/manual-assets/screenshots/README.md`
- Create: `docs/manual-assets/screenshots/screenshot-manifest.csv`
- Create: `C:\Users\34652\AppData\Local\Temp\codex-documents\tdjs-vision-illustrated\Capture-Window.ps1`
- Modify: `TDJS_VISION_MANUAL_TASKS.md`

**Interfaces:**
- Consumes: 指定 Release 可执行文件路径。
- Produces: `Capture-Window.ps1`、截图目录和包含 `编号,分类,模块,窗口标题,文件名,状态,备注` 的截图清单。

- [ ] **Step 1: 创建截图目录说明和清单表头**

  使用 `apply_patch` 创建目录说明文件和 CSV，明确文件名格式为 `序号-分类-模块-界面.png`。

- [ ] **Step 2: 创建窗口截图辅助脚本**

  脚本接收进程标识、输出路径和可选窗口标题，通过 `GetWindowRect` 获取实际窗口边界，并使用 `System.Drawing.Graphics.CopyFromScreen` 保存 PNG；脚本不得自动输入密码或关闭未知弹窗。

- [ ] **Step 3: 验证截图脚本的参数和输出目录**

  运行 PowerShell 语法检查，预期无解析错误；创建一张当前前台窗口测试图并使用图像查看工具确认尺寸和清晰度。

- [ ] **Step 4: 记录任务开始状态**

  在任务记录中写明图文增强版目标、输出文件、三维范围和截图目录。

### Task 2: 启动软件并记录入口界面

**Files:**
- Create: `docs/manual-assets/screenshots/001-平台基础-软件启动-主窗口.png`
- Create: `docs/manual-assets/screenshots/002-平台基础-登录权限-登录窗口.png`（仅当实际出现）
- Modify: `docs/manual-assets/screenshots/screenshot-manifest.csv`

**Interfaces:**
- Consumes: Release 程序、截图辅助脚本。
- Produces: 主窗口截图、登录或授权状态说明、可访问窗口标题清单。

- [ ] **Step 1: 启动 Release 程序**

  使用可见窗口方式启动程序，工作目录设置为 Release 目录；等待主窗口、登录窗口或授权提示出现。

- [ ] **Step 2: 检查进程和顶层窗口**

  读取进程标识、主窗口标题和响应状态；如出现登录或授权限制，只记录界面和限制，不尝试绕过。

- [ ] **Step 3: 截取启动与主界面**

  保存完整窗口截图，检查标题栏、菜单、工具箱、画布、图像区、结果区和日志区是否清晰。

- [ ] **Step 4: 更新截图清单**

  记录截图文件、实际窗口标题、访问状态和需要后续补拍的区域。

### Task 3: 遍历平台基础与设备管理界面

**Files:**
- Create: `docs/manual-assets/screenshots/01-platform/*.png`
- Create: `docs/manual-assets/screenshots/02-devices/*.png`
- Modify: `docs/manual-assets/screenshots/screenshot-manifest.csv`

**Interfaces:**
- Consumes: 可访问的主窗口、菜单和设备管理入口。
- Produces: 方案、流程、运行、设置、日志、权限、相机、光源、PLC、Modbus、TCP、串口和三维取图入口截图。

- [ ] **Step 1: 枚举主窗口自动化元素**

  导出按钮、菜单、选项卡、树节点和窗口标题，按可见文本定位功能入口。

- [ ] **Step 2: 截取平台基础界面**

  依次打开方案管理、流程管理、系统设置、全局信号、检测项、日志和权限界面；每次打开后校验窗口标题再截图。

- [ ] **Step 3: 截取二维设备配置界面**

  打开二维相机、光源、PLC、Modbus、TCP和串口配置窗口，保留完整参数区并记录需要连接硬件验证的内容。

- [ ] **Step 4: 截取三维取图入口**

  仅记录三维相机连接、`3D图像源`和`3D图像显示`界面，不生成三维算法说明或应用结果。

- [ ] **Step 5: 核对截图清单**

  检查每个设备模块至少有入口图或参数图；无法打开的模块记录实际原因。

### Task 4: 建立离线演示流程并遍历节点参数窗口

**Files:**
- Create: `docs/manual-assets/screenshots/03-image-acquisition/*.png`
- Create: `docs/manual-assets/screenshots/04-image-processing/*.png`
- Create: `docs/manual-assets/screenshots/05-detection/*.png`
- Create: `docs/manual-assets/screenshots/06-measurement/*.png`
- Create: `docs/manual-assets/screenshots/07-graphics/*.png`
- Create: `docs/manual-assets/screenshots/08-communication/*.png`
- Create: `docs/manual-assets/screenshots/09-logic/*.png`
- Create: `docs/manual-assets/screenshots/10-results/*.png`
- Modify: `docs/manual-assets/screenshots/screenshot-manifest.csv`

**Interfaces:**
- Consumes: 工具箱分类、流程画布和各节点参数窗口。
- Produces: 八类节点的工具箱总览图和各模块参数界面图。

- [ ] **Step 1: 创建专用离线演示方案与流程**

  新建不连接现场设备的演示方案，按工具箱分类创建流程，避免修改客户方案或现有生产配置。

- [ ] **Step 2: 截取八类工具箱总览**

  展开每个分类，截图完整节点列表，并在清单中建立工具箱文本与说明书模块名称的映射。

- [ ] **Step 3: 遍历图像采集、图像处理和检测识别节点**

  逐个添加节点、打开参数窗口、截图并关闭参数窗口；遇到模型、图像或设备前置条件时保留实际提示并记录限制。

- [ ] **Step 4: 遍历测量工具和图形创建节点**

  逐个截图卡尺、找点、位置修正、距离、夹角和线组合拟合参数界面，记录需要标定、ROI或上游几何结果的节点。

- [ ] **Step 5: 遍历设备通信和逻辑工具节点**

  截取光源、相机IO、PLC、TCP、Modbus、串口、全局信号、分支、流程同步、组合模块、脚本和弹窗参数界面。

- [ ] **Step 6: 遍历结果处理节点**

  截取绘制、发送、保存、清理、汇总、显示和导出参数界面，重点记录保存条件、路径、格式和结果汇总规则。

- [ ] **Step 7: 完成截图覆盖检查**

  以 `ToolTreeView.xml` 和节点工厂为基准核对清单；每个模块状态必须为“已截图”“共用分类图”或“受实际前置条件限制”。

### Task 5: 整理截图与模块说明映射

**Files:**
- Create: `docs/manual-assets/screenshots/module-figure-map.json`
- Modify: `docs/manual-assets/screenshots/screenshot-manifest.csv`

**Interfaces:**
- Consumes: 全部实际截图和模块清单。
- Produces: 文档生成器使用的模块到截图、图题、区域说明和限制说明映射。

- [ ] **Step 1: 检查每张截图**

  使用图像查看工具确认无截断、无其他应用窗口遮挡、无敏感数据和可读性问题。

- [ ] **Step 2: 删除重复截图引用而不删除原始文件**

  清单中将同类界面指向同一截图，保留不同模块的独立图题和差异说明。

- [ ] **Step 3: 生成模块图片映射**

  JSON 每项包含 `module_name`、`category`、`images`、`caption`、`regions`、`offline_limit` 和 `source_window_title`。

- [ ] **Step 4: 校验覆盖率**

  读取工具箱模块清单并与 JSON 模块名称比较，输出缺失、重复和未识别项；预期缺失项为 0，三维未实现算法不纳入检查范围。

### Task 6: 生成图文增强版 Word

**Files:**
- Create: `C:\Users\34652\AppData\Local\Temp\codex-documents\tdjs-vision-illustrated\build_illustrated_manual.py`
- Create: `outputs/TDJS-Vision工业视觉应用框架功能与应用说明书-图文增强版.docx`
- Modify: `TDJS_VISION_MANUAL_TASKS.md`

**Interfaces:**
- Consumes: 现有纯说明版生成数据、实际截图、模块图片映射。
- Produces: 独立图文增强版 DOCX。

- [ ] **Step 1: 复制现有文档结构与样式配置**

  保留封面、导航、标题层级、页眉页脚、模块参数表、操作步骤、算法逻辑和附录索引。

- [ ] **Step 2: 插入平台与设备截图**

  在对应章节插入实际界面、简体中文图题、区域说明表和“需连接设备验证”提示。

- [ ] **Step 3: 插入节点参数截图**

  每个模块按映射插入一至两张图片，统一正文宽度，保持图片、图题和区域说明不分离。

- [ ] **Step 4: 修订三维内容**

  删除原说明书中高度、平面度、体积、焊缝等未实现三维算法描述，只保留取图、显示和当前能力边界。

- [ ] **Step 5: 保存图文增强版并执行结构检查**

  验证 DOCX 压缩包完整、图片关系有效、标题数量合理、所有映射图片均嵌入且原纯说明版未被覆盖。

### Task 7: 渲染、逐页检查与交付

**Files:**
- Create: `C:\Users\34652\AppData\Local\Temp\codex-documents\tdjs-vision-illustrated\rendered/*.png`
- Modify: `TDJS_VISION_MANUAL_TASKS.md`

**Interfaces:**
- Consumes: 图文增强版 DOCX。
- Produces: 通过视觉和结构验证的最终文档。

- [ ] **Step 1: 使用 Microsoft Word 导出临时 PDF**

  后台打开最终 DOCX 并导出 PDF，确认 Word 能正常解析所有图片、标题和表格。

- [ ] **Step 2: 生成逐页 PNG**

  使用 Poppler 将 PDF 转为 PNG，记录页数并生成联系表。

- [ ] **Step 3: 检查全部页面**

  检查图片清晰度、缩放比例、图题位置、跨页关系、表格、页眉页脚、空白页和文字截断；任何异常都回到生成器修正并重新渲染。

- [ ] **Step 4: 执行结构与可访问性审计**

  运行标题、分节、图片和可访问性检查；Logo和实际截图都必须有替代文字，数据表首行必须正确标记。

- [ ] **Step 5: 更新任务记录并核对工作区**

  记录实际截图数量、受限模块、最终页数、审计结果和未修改业务代码的事实。

- [ ] **Step 6: 交付图文增强版 DOCX**

  最终回复只提供图文增强版 Word 的单独下载链接，不交付临时 PDF、PNG 或构建脚本。
