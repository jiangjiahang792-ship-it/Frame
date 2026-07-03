# Unsupervised Train Form Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build the first-stage unsupervised training UI and training/template packaging path in `UnsupervisedTrainForm`.

**Architecture:** Keep the WinForms layout in the designer file, put image loading/training/runtime/template work into small helper classes under `Forms/AiTrainForm`, and delay-load native unsupervised runtime from `UnsupervisedDll` only when training starts.

**Tech Stack:** .NET Framework 4.8 WinForms, OpenCvSharp, Newtonsoft.Json, System.IO.Compression, PowerShell regression tests.

---

### Task 1: Regression Tests

**Files:**
- Create: `Tests/UnsupervisedTrainForm.Tests.ps1`
- Create: `Tests/UnsupervisedRuntimeIsolation.Tests.ps1`
- Create: `Tests/UnsupervisedTemplatePackage.Tests.ps1`

- [ ] Write tests that fail while the form is still empty.
- [ ] Run each test and confirm it fails for missing controls/classes.

### Task 2: Designer Layout

**Files:**
- Modify: `Forms/AiTrainForm/UnsupervisedTrainForm.Designer.cs`
- Modify: `Forms/AiTrainForm/UnsupervisedTrainForm.cs`
- Modify: `Forms/ShapeDraw/ImageROIEditControl.cs`

- [ ] Add tabs `检查` and `训练`.
- [ ] Add check page controls for image path, category buttons, thumbnail list, preview ROI control, and status labels.
- [ ] Add train page controls for parameters, device, model path, progress, and logs.
- [ ] Add public ROI draw helper on `ImageROIEditControl`.

### Task 3: Data And Runtime Helpers

**Files:**
- Create: `Forms/AiTrainForm/UnsupervisedTrainingModels.cs`
- Create: `Forms/AiTrainForm/UnsupervisedImageLoader.cs`
- Create: `Forms/AiTrainForm/UnsupervisedRuntimeBootstrapper.cs`
- Create: `Forms/AiTrainForm/UnsupervisedAnomalibDetector.cs`
- Create: `Forms/AiTrainForm/UnsupervisedTemplatePackage.cs`
- Create: `Forms/AiTrainForm/UnsupervisedTrainingService.cs`
- Modify: `TDJS-Vision.csproj`

- [ ] Add image item/category/request/result/manifest models.
- [ ] Add recursive image loader with thumbnail generation and OK/NG classification.
- [ ] Add runtime bootstrapper that checks `UnsupervisedDll`.
- [ ] Add native wrapper copied into TDJS namespace with delayed `DllImport`.
- [ ] Add template packager using `ZipArchive`.
- [ ] Add training service that builds full-image or ROI-cropped datasets and packages the model.

### Task 4: Wire UI Behavior

**Files:**
- Modify: `Forms/AiTrainForm/UnsupervisedTrainForm.cs`

- [ ] Load images asynchronously from selected folder.
- [ ] Render All/OK/NG thumbnails with green/red border.
- [ ] Preview selected image and keep optional single ROI.
- [ ] Validate training request.
- [ ] Run training on a background task, update progress and logs, and support cancel request.

### Task 5: Project Record And Verification

**Files:**
- Modify: `FLOW_CANVAS_B_PLAN_TASKS.md`

- [ ] Record the unsupervised training UI change.
- [ ] Run new regression tests.
- [ ] Run existing focused regression tests.
- [ ] Run `git diff --check`.
- [ ] Run MSBuild `TDJS-Vision.sln`.
