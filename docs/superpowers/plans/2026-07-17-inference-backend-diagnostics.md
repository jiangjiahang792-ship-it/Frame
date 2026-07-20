# AI 推理后端与分段耗时诊断 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 统一主程序与 Demo 的 GPU 后端，并在客户机日志中暴露真实设备、模型和分段推理耗时。

**Architecture:** 检测器封装负责精确测量图像格式准备和 native P/Invoke；运行时汇总 ROI 准备与后处理并保存已加载设备；节点成功日志发布诊断数据。显式 GPU 初始化失败向上传递，AUTO 保留 CPU 容错。

**Tech Stack:** C# 7.3、.NET Framework、OpenCvSharp、native P/Invoke、PowerShell 5.1、MSBuild。

## Global Constraints

- 所有新增注释和用户可见日志使用简体中文。
- 不修改 WinForms 设计器布局。
- 不在本轮删除图像复制或改变推理结果算法。
- 不覆盖工作区内与本任务无关的已有修改。

---

### Task 1: 建立后端与诊断回归测试

**Files:**
- Create: `Tests/InferenceBackendDiagnostics.Tests.ps1`

**Interfaces:**
- Consumes: 两套检测器、运行时和节点源码。
- Produces: 后端映射、回退规则和诊断字段结构检查。

- [ ] **Step 1: 编写失败测试**
- [ ] **Step 2: 运行测试并确认因缺少 GPU_ORT 映射和诊断字段而失败**

### Task 2: 修正后端和失败策略

**Files:**
- Modify: `Node/3-Detection/Unsupervised/UnsupervisedDetectionRuntime.cs`
- Modify: `Node/3-Detection/LargeModel/LargeModelDetectionRuntime.cs`

**Interfaces:**
- Consumes: 模板设备文本和大模型设备模式。
- Produces: 无监督 `GPU_ORT`/`AUTO_ORT` 映射及仅 AUTO 可回退规则。

- [ ] **Step 1: 保留显式 ORT/TensorRT token，并把普通 GPU/AUTO 映射到 ORT**
- [ ] **Step 2: 无监督仅允许 AUTO 后端回退 CPU**
- [ ] **Step 3: 大模型仅允许 AUTO 模式回退 CPU**

### Task 3: 增加分段耗时和实际后端日志

**Files:**
- Modify: `Forms/AiTrainForm/UnsupervisedAnomalibDetector.cs`
- Modify: `Forms/AiTrainForm/LargeModelDinov2Detector.cs`
- Modify: `Node/3-Detection/Unsupervised/UnsupervisedDetectionRuntime.cs`
- Modify: `Node/3-Detection/LargeModel/LargeModelDetectionRuntime.cs`
- Modify: `Node/3-Detection/Unsupervised/NodeUnsupervisedDetection.cs`
- Modify: `Node/3-Detection/LargeModel/NodeLargeModelDetection.cs`

**Interfaces:**
- Produces: `ActualDevice`、`ActualModelPath`、`InputWidth`、`InputHeight`、`ImagePreparationMilliseconds`、`NativeInferenceMilliseconds` 和 `PostprocessMilliseconds`。

- [ ] **Step 1: 检测器重载 `InferBgr` 并精确测量 BGR 准备和 native 调用**
- [ ] **Step 2: 运行时汇总 ROI 准备、检测器准备和结果处理耗时**
- [ ] **Step 3: 模型加载成功时记录实际设备与模型路径**
- [ ] **Step 4: 节点成功日志输出输入尺寸、实际设备和三个阶段耗时**

### Task 4: 验证与记录

**Files:**
- Modify: `任务记录.md`

- [ ] **Step 1: 运行新增及既有专项检查**
- [ ] **Step 2: 编译 `Debug|x64` 与 `Release|x64`**
- [ ] **Step 3: 更新任务记录并给出客户机日志采集说明**
