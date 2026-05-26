# ProcessNew Free Flow Canvas B Plan Task List

## Current Plan

We are currently following **Plan B**:

> Upgrade `Forms/ProcessNew` from the existing ordered node list editor into a free flow-canvas editor with a graph model, and finally make process execution follow node connections.

This means the target is not only a visual style change. The work includes:

- Free node placement on a canvas.
- Node connections and start-node semantics.
- Graph data persistence and restore.
- Compatibility with existing `NodeBase` nodes and parameter forms.
- A later execution-engine migration from ordered list execution to connection-driven execution.

## Current Step

The current implementation step is **Phase 20: Edge Point Finding Measurement Tool**.

### Latest Progress

- Completed through the first **Phase 10: Runtime State On Canvas** code pass.
- Fixed one graph runtime scheduling regression found during Phase 10 checks:
  - [x] For `A -> B` plus standalone `C`, running `A` now completes the `A -> B` component before moving to independent source node `C`.
- Fixed two additional runtime regressions found during Phase 10 checks:
  - [x] Restored/new `If` graph connections now interpret `Right` as True and `Bottom` as False even when older saved connection data still has `Branch = Default`.
  - [x] During a process run, subscriptions can no longer read stale results from an upstream node that has not successfully run in the current run.
- Addressed latest UI feedback with a second Phase 10 runtime pass:
  - [x] Graph-native `If = false` no longer falls back to legacy ordered-list continuation when no matching `Else/EndIf` exists.
  - [x] Runtime subscription freshness now uses the current process run id, so disabled or branch-skipped upstream nodes cannot provide prior-run results.
- Phase 10 UI regression status:
  - [x] Simple, branch, disabled, failed, stopped, and runtime-state-refresh checks passed in the UI.
  - [ ] Passive-triggered graph-flow verification is intentionally deferred.
- Phase 18 composite measurement planning:
  - [x] New request captured: integrate line-to-line angle, point-to-point distance, and point-to-region distance measurement nodes.
  - [x] Use `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2` as the manual-tool algorithm reference.
  - [x] Each new tool should support subscribing upstream measurement results and drawing local ROI geometry.
  - [x] Local drawn measurement should use existing caliper algorithms where applicable instead of only manual coordinate clicks.
  - [x] Each new tool should expose position-correction options for local drawn ROIs.
  - [x] Parameter UI should include Basic Parameters and Run Parameters only; no Result Display page.
- Phase 20 edge point finding planning:
  - [x] New request captured: add a `FindPoint` measurement node under the measurement toolbox category.
  - [x] The tool should subscribe one image input, let the user draw/confirm one or more ROI regions, and find all edge points inside those regions.
  - [x] Edge point extraction should follow/trace edge contours where possible, so the output is ordered edge point groups instead of only an unordered threshold point cloud.
  - [x] The first design should reuse the existing clean image plus `OutputImage.DisplayResult` overlay model; runtime output image pixels stay clean.
  - [x] The result should be reusable by downstream measurement, condition, ROI drawing, and point/region subscription logic.
- Phase 11 undo/redo first implementation pass:
  - [x] `ProcessFlowCanvas` now owns a canvas edit history stack.
  - [x] Undo/redo supports node creation, node deletion, node movement, connection creation, connection deletion, start-node changes, and node enable/disable changes.
  - [x] `Ctrl+Z`, `Ctrl+Y`, and `Ctrl+Shift+Z` were temporarily removed after UI feedback; undo/redo is currently available from the canvas context menu.
  - [x] Canvas context menu now includes undo and redo entries.
  - [x] Node deletion can preserve the node object for undo without releasing node resources.
  - [x] Fixed node-move undo redraw leaving a stale visual residue at the pre-undo location.
  - [x] UI verification for the undo/redo pass passed after removing shortcuts and fixing node-move redraw residue.
- Phase 11 copy/paste first implementation pass:
  - [x] Canvas context menu now includes copy node and paste node entries.
  - [x] Copy/paste supports selected single node and selected multiple nodes.
  - [x] Pasted nodes receive a new node ID and a unique copied node name.
  - [x] Pasted nodes clone parameters, notes, active state, size, and canvas placement.
  - [x] Paste is registered as a node creation edit, so existing undo/redo can remove and restore the pasted node.
  - [x] UI verification for the single-node copy/paste pass passed.
- Phase 11 multi-select and multi-node edit first implementation pass:
  - [x] Canvas supports rubber-band selection with a dashed selection rectangle.
  - [x] Nodes intersecting the selection rectangle become selected as a group.
  - [x] Dragging any selected node moves the selected group together.
  - [x] Deleting selected nodes removes them as a grouped undo/redo edit and restores attached connections on undo.
  - [x] Enabling or disabling selected nodes is grouped in undo/redo.
  - [x] Multi-node copy/paste copies internal connections among the selected nodes.
  - [x] Pasted internal connections are remapped from old node IDs to new node IDs.
  - [x] Pasted parameter text references such as `ID.NodeName` are remapped when they refer to nodes copied in the same batch.
  - [x] UI verification for multi-select, group move, group delete, and multi-node copy/paste passed.
- Phase 11 zoom first implementation pass:
  - [x] Canvas supports zoom from 50% to 200%.
  - [x] Right-click canvas menu includes zoom in, zoom out, and reset zoom commands.
  - [x] `Ctrl + mouse wheel` zooms around the mouse pointer.
  - [x] Drawing, hit-testing, rubber-band selection, dragging, connection interaction, and paste location use zoom-aware coordinate conversion.
  - [x] Canvas shows the current zoom percentage.
  - [x] Scrolling after zoom forces a full repaint so the zoom percentage overlay does not leave duplicate residues.
  - [x] UI verification for zoom interactions passed.
- Phase 11 snap-to-grid first implementation pass:
  - [x] Canvas has a right-click menu toggle for snap-to-grid.
  - [x] Dragged nodes snap to the nearest 24px grid point when the toggle is enabled.
  - [x] Toolbox-created nodes use the snap rule when the toggle is enabled.
  - [x] Multi-node paste snaps the pasted group target while preserving relative node placement.
- Phase 13 UI layout polish planning:
  - [x] Use `D:\MyCode\PublicWook\TDVision\TDVision\TDVision\UI\FlowCanvasPanel.cs` as the visual and connection-routing reference.
  - [x] Split the UI polish work into staged deliverables so the current graph editor remains usable after each step.
  - [x] Phase 13.1: Dark canvas surface, compact node styling, and routed arrow connections first code pass.
  - [x] Phase 13.2: Toolbox layout cleanup and left-side navigation density improvements.
  - [x] Phase 13.3: Process tab/header command bar cleanup.
  - [x] Phase 13.4: Optional right-side image/result preview panel.
  - [x] Phase 13.5: Minimap and viewport polish.
- Phase 13.1 canvas visual and connection polish:
  - [x] Canvas background now uses a dark workbench surface.
  - [x] Grid lines use subtle dark minor/major grid styling.
  - [x] Nodes have a cleaner light-card style with shadow, status color, and selected orange accent.
  - [x] `ProcessFlowCanvas` now uses a routed arrow path based on the `TDVision` `PathRouter` approach.
  - [x] Connection hit-testing now uses the same routed path as drawing.
  - [x] `If` branch labels remain visible on routed true/false connections.
  - [x] UI verification accepted before starting Phase 13.2.
- Phase 13.2 toolbox layout first implementation pass:
  - [x] Toolbox controls are now grouped inside a designer-visible `panelToolbox`.
  - [x] Left toolbox width was reduced from the old fixed 394px layout.
  - [x] Toolbox can collapse to a narrow left rail and expand again.
  - [x] Toolbox tree uses a darker, denser visual style.
  - [x] Toolbox icons are generated from category/node type names at runtime.
  - [x] Drag cursor preview uses the generated toolbox icons.
- Phase 13.2 toolbox feedback pass:
  - [x] Replaced the visible expanded toolbox tree with a left category rail plus a selected-category module grid.
  - [x] Toolbox category clicks rebuild the visible module cards for that category only.
  - [x] Module cards keep the existing `DragData` drag source behavior for canvas node creation.
  - [x] The process editor window now opens maximized and exposes minimize, maximize/restore, close, and border resize behavior.
- Phase 13.2 floating toolbox feedback pass:
  - [x] The left toolbox now defaults to a compact category rail only.
  - [x] Clicking a category opens the module grid as a floating flyout over the editor surface.
  - [x] Starting a module drag closes the floating flyout after the drag operation.
- Phase 13.3 process header first implementation pass:
  - [x] Moved add/delete process actions into a compact top action strip instead of the old right-side empty column.
  - [x] Restyled process tabs with a compact dark owner-drawn tab header.
  - [x] Restyled the per-process run/stop/clean/enable row as a compact dark command bar.
- Phase 13.3 process header feedback pass:
  - [x] The add-process button is now a compact blue `+` positioned beside the process tabs.
  - [x] The tab-adjacent action strip now auto-sizes for add/delete process commands so neither action is clipped.
  - [x] The tab-adjacent `+` placement now uses the actual last tab rectangle to avoid header/action misalignment.
  - [x] Per-process command bars now show only single run, loop run, and stop on the active process page.
  - [x] Added first-pass loop run behavior that repeatedly runs the active process until stop/cancellation.
  - [x] Restyled the process tab strip to match the dark reference header with red status dots, stronger selected-tab text, and darker inactive tabs.
  - [x] Removed the visible numeric prefix from owner-drawn process tab text while keeping the underlying tab text unchanged for existing process lookup.
  - [x] Extended the dark tab-adjacent action surface across the empty header area so the default light tab background no longer shows.
  - [x] Replaced low-resolution run/loop/stop resource images with GDI+ vector-drawn command glyphs.
- Phase 13.4 image and viewport preview first pass:
  - [x] `ProcessFlowCanvas` draws a floating image preview panel at the top-right of the canvas.
  - [x] The preview panel now binds to selected-node `OutputImage` data when available, with a latest image-output fallback.
  - [x] The preview header selector now opens a list of available process image outputs for manual selection.
  - [x] The preview header shows the image node title and live RGB values while hovering over the preview image.
  - [x] `ProcessFlowCanvas` draws a minimap at the bottom-right of the canvas.
  - [x] The minimap supports click/drag viewport navigation.
- Phase 13 performance feedback pass:
  - [x] The preview overlay now caches the converted bitmap instead of converting `Mat` to `Bitmap` on every paint/zoom redraw.
  - [x] AI result drawing no longer clones the entire bitmap before drawing, and reuses the text font within each draw pass.
  - [x] Image preview and minimap now render as designer-visible overlay controls above the canvas instead of being painted into the scrollable node layer.
  - [x] Fixed preview/minimap flicker while scrolling by keeping both overlays out of the `AutoScroll` paint surface.
  - [x] Removed high-frequency overlay invalidation during node dragging and rubber-band selection so canvas interactions stay responsive.
- Phase 14 multi-condition logic node planning:
  - [x] New request captured: create a node and parameter UI based on `Node/6-LogicTool/If` that supports freely adding multiple conditions.
  - [x] Condition source selection should choose a property from an upstream node result; in normal cases this means public `INodeResult` output properties.
  - [x] The circled UI area in the provided reference image is planned as an upstream-node/result-property picker.
  - [x] WinForms controls added for this feature must be declared and laid out in the corresponding `.Designer.cs` files so the designer can display them.
- Phase 14 multi-condition logic node first implementation pass:
  - [x] Added `NodeType.MultiCondition` and a new `Node/6-LogicTool/MultiCondition` node family.
  - [x] Added serializable condition rows with match mode, source node id/text, property path, operator, values, and note.
  - [x] Added a designer-visible parameter form with condition rows plus an upstream node/result-property tree.
  - [x] Added reflection-based evaluation for scalar values, nested result properties, and common OK/NG boolean patterns.
  - [x] Connected the new node to graph-native True/False branch routing.
  - [x] Added toolbox/language entries and copy/paste remapping for internal `SourceNodeId` references.
- Phase 14 multi-condition UI feedback fixes:
  - [x] Added a runtime/designer fallback so `NodeParamFormMultiCondition.InitializeOperatorColumn` can recreate `ColumnOperator` when the designer file is regenerated without the column instance.
  - [x] Adjusted the condition grid columns to use fill layout, larger row/header height, and minimum widths so table content is no longer clipped by narrow cells.
- Phase 15 OpenCV caliper/circle/ellipse integration planning:
  - [x] Read and compared the reference algorithms from `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2`.
  - [x] Confirmed all three tools share the same core pipeline: grayscale input, caliper sampling, 1D gradient edge search, edge polarity/mode selection, parabolic sub-pixel correction, then geometric fitting.
  - [x] Confirmed `CaliperLineTool` fits line results with `Cv2.FitLine`, `CaliperCircleTool` solves circle least squares with `Cv2.Solve(..., SVD)`, and `FindEllipseTool` fits ellipse results with `Cv2.FitEllipse`.
  - [x] Confirmed the current `ShowImageControl` already supports basic line/circle ROI drawing, but does not yet expose the reference solution's circle-caliper and ellipse-caliper ROI types needed for the new tools.
- Phase 15 OpenCV measurement integration first implementation pass:
  - [x] Added shared caliper measurement algorithm code under `Node\4-Measurement\Common`.
  - [x] Added three new measurement nodes under `Node\4-Measurement`: `CaliperLine`, `CaliperCircle`, and `CaliperEllipse`.
  - [x] Added designer-visible WinForms parameter forms for the three nodes, with image subscription, numeric geometry/caliper settings, run preview, and save support.
  - [x] Registered the three node types in `NodeType`, process node creation, toolbox XML, language JSON files, and project compile items.
  - [x] Exposed scalar result properties such as OK flag, edge point count, fitted geometry, and algorithm time so downstream `INodeResult` property pickers can read them.
- Phase 15 measurement ROI interaction feedback pass:
  - [x] Merged the reference `ShowImageControl` circle-caliper and ellipse-caliper ROI APIs/classes into the current project control while keeping existing display compatibility APIs.
  - [x] Replaced the three measurement parameter-form preview `PictureBox` controls with designer-visible `ShowImageControl` controls.
  - [x] The line, circle, and ellipse measurement forms now create draggable caliper ROI overlays and read the latest ROI geometry back before save/run.
  - [x] Left-side line/circle/ellipse ROI parameter edits now immediately refresh the right-side caliper ROI overlay, including caliper count, width, height, position, radius/angle, and direction changes.
  - [x] Changing caliper count/width/height now preserves the current dragged ROI position instead of rebuilding from stale left-side coordinate text.
  - [x] `ShowImageControl.ImageBitmap` no longer calls `ShowFit()` on every image assignment, so run-preview updates keep the current zoom and pan.
- Phase 15.7 fast rotated template matching feedback pass:
  - [x] Reworked `NodeParamFormMatchTemplate` into a three-tab contour-matching layout: Basic Parameters, Feature Template, and Run Parameters, with bottom Continuous Run / Run / OK commands.
  - [x] Removed the Result Display tab; compact run status now stays on the Basic Parameters page with the image preview.
  - [x] Basic Parameters now focuses on image input, search-region setup, display toggles, and run preview. Search-region geometry is saved with the solution and used to crop matching input for faster search.
  - [x] Template creation/editing now opens a dedicated `TemplateCreateForm` dialog again; the dialog owns template ROI generation, contour preview, and brush-based contour/detail erasing.
  - [x] Template images now save into the solution through `NodeParamMatchTemplate.TemplateImageBytes`; external paths are only a legacy/import fallback.
  - [x] The main Feature Template tab now acts as template management and contour preview, while detailed editing happens in the template dialog.
  - [x] Removed the old standalone `TemplateCreate` form from the project file and added the new designer-visible `TemplateCreateForm`.
  - [x] Added explicit confirm-ROI actions for both the main search region and the template creation ROI. Run/generate now requires confirmation and rejects changed ROI geometry until confirmed again.
  - [x] Reworked template brush erasing to use an accumulated erase mask plus OpenCV inpaint, so erased areas are filtered out of template contours and brush patch borders are not counted as new contours.
  - [x] Adjusted ROI interaction so dynamic ROIs are not shown by default; template creation uses Draw ROI -> Create Template, search regions use Draw ROI -> Confirm ROI, and confirmed ROIs are cleared from the image control.
  - [x] Added source-image refresh/fit before opening template creation so the template dialog does not open with an empty source display.
  - [x] Fixed saved search-region reuse after reopening the template-matching parameter window when whole-image search is disabled.
  - [x] Applied the same no-default-ROI behavior to caliper line, circle, and ellipse parameter forms, with designer-visible Draw ROI and Confirm ROI buttons.
  - [x] Hardened embedded-template restore after reopening a solution: PNG byte decode now has an OpenCV fallback, template bitmap replacement is atomic, and contour-preview failures no longer block template restore.
  - [x] Fixed the specific reopened-template restore failure in `FastTemplateMatcher.To24Bpp(...)` by replacing the fragile 24bpp `Bitmap.Clone()` path with a `LockBits` row-copy path plus OpenCV fallback.
- Phase 16 position correction and measurement-following planning:
  - [x] New request captured: add a position-correction/following tool so measurement ROIs can follow a matched product location.
  - [x] First-pass correction data should use X, Y, and angle only.
  - [x] Position correction should subscribe to upstream template-matching location data, store a user-created baseline, and output current offset/angle correction information.
  - [x] Measurement tools should expose designer-visible controls for enabling position correction and subscribing to correction information.
  - [x] When enabled, measurement runtime should transform saved measurement geometry by the subscribed correction before measuring.
  - [x] Implement a new position-correction node/result/parameter form and toolbox/language/project registration.
  - [x] Add correction subscription controls and runtime geometry transform to caliper line, circle, and ellipse tools.
  - [x] Build-verify after first implementation pass.
  - [x] Feedback fix: template-match and caliper parameter-form Refresh Image actions now only reload the current subscribed image result and no longer run upstream flow nodes.
  - [x] Feedback fix: local template-match Run/Continuous Run now publishes MatchX, MatchY, Angle, Score, and display output to the node result so position-correction baseline creation reads the just-previewed match result.
  - [x] Feedback fix: position-correction Create Baseline and local Run now publish the current correction info to the node result so same-image baseline correction remains an identity transform for downstream calipers.
  - [x] Build-verify after the refresh-image/no-upstream-run and baseline-result publishing fixes.
  - [x] Feedback fix: when position correction is enabled, line/circle/ellipse caliper parameter forms now display saved baseline ROI through the current correction for editing, and inverse-transform edited/drawn ROI back to baseline coordinates on confirm/save.
  - [x] This allows adding or editing measurement ROIs on a later corrected image without saving an already-followed ROI and applying the correction a second time.
  - [x] Build-verify after the corrected-image ROI baseline-normalization fix.
  - [ ] Runtime-verify template match -> position correction -> measurement follow on real images.
- Phase 17 parallel branch runtime scheduling design:
  - [x] New request captured: graph branches such as `A -> B` and `A -> C` should be able to run `B` and `C` in parallel instead of sequentially when dependencies allow it.
  - [x] Current behavior confirmed: the graph runtime uses a pending queue and awaits each node one by one, so sibling branches both run but do not run concurrently.
  - [x] Chosen strategy: dependency-aware parallel scheduling, keeping graph order deterministic and batching all nodes whose graph dependencies are ready.
  - [x] First implementation pass added a parallel graph scheduler while preserving the old sequential graph runtime as fallback for preview/skip runs and legacy control-flow graphs.
  - [x] Removed the first-pass safe-node allowlist; all node types can run in parallel when their incoming graph dependencies are satisfied.
  - [x] Conditional branch skips now mark non-selected edges inactive so joins wait only for active upstream paths.
  - [x] Join nodes wait for all active incoming branches to complete; if any node in a parallel batch fails, downstream nodes are not started and the process fails after the already-started batch finishes.
  - [x] Parallel graph process runtime now reports dependency-graph critical-path time, so forked branches contribute the slowest branch instead of branch sums or UI/thread scheduling overhead.
  - [x] Parallel branch failures are isolated: independent ready branches continue running, while descendants of the failed branch are skipped and the first real exception is reported after runnable work finishes.
  - [x] Template-match no-hit and caliper no-geometry results are normal NG outcomes with nullable scalar outputs instead of hard process failures.
  - [x] Multi-condition evaluation treats missing/current-run-null source values as null; every normal comparison returns false on null except explicit IsNull/IsNotNull.
  - [x] Parallel scheduler now forces a failed node runtime status when catching branch exceptions, so the canvas keeps the failed node visibly red while other runnable branches continue.
  - [x] Multi-condition evaluation treats a disabled source node as a satisfied condition before null/property comparison.
  - [x] Feedback change: node result-level NG is now tracked separately from runtime failure; `IsOk = false`, `IsAllOk = false`, or an NG `Result`/`AlgorithmResult`/`SummaryResult` shows as NG on the canvas but keeps `RuntimeStatus = Successful` so downstream IF, ROI drawing, saving, and communication nodes can still run.
  - [x] Feedback fix: failed canvas nodes now keep a red border, red title band, red anchor borders, and red status text even when selected.
  - [x] Feedback fix: QR no-code results and FindCircle radius NG results now publish `AlgorithmResult.IsAllOk = false` so the generic result-status pass can mark those nodes failed on the canvas.
  - [x] Feedback fix: scheduler now treats a node's final `RuntimeStatus = Failed` as a dependency failure, so downstream nodes and joins do not run through failed upstream paths even when another incoming branch is disabled or completed.
  - [x] Feedback fix: `MultiCondition` is now a failure-tolerant condition node; failed upstream paths do not block the multi-condition node itself, while ordinary downstream nodes still keep the existing failed-dependency blocking behavior.
  - [x] Build-verify after the first dependency-aware parallel scheduler implementation pass.
  - [x] Build-verify after branch failure isolation and nullable visual NG result handling.
  - [x] Build-verify after failed-node visual status and disabled-source condition handling.
  - [x] Build-verify after the earlier node-result NG canvas-status feedback pass.
  - [x] Build-verify after failed-runtime-status downstream blocking fixes.
  - [x] Build-verify after separating result-level NG from runtime failure.
  - [ ] Runtime-verify with simple fork, join, condition branch, disabled node, failed node, stop/cancel, image subscription, and device-node cases.
- TDAI stability fix:
  - [x] Added a model-readiness guard before `NodeTDAI` reads `param.Yolo8.ModelType`, so a failed startup model load now reports a clear model/config error instead of throwing a null reference.
  - [x] Made `ParamFormTDAI.LoadModel(...)` clear `param.Yolo8` on load failure and log failures without dereferencing missing AI config data.
- Build verification:
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 17 dependency-aware parallel scheduler first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after branch failure isolation and nullable visual NG result handling.
  - [x] `TDJS-Vision.sln` builds successfully after failed-node visual status and disabled-source condition handling.
  - [x] `TDJS-Vision.sln` builds successfully after the scheduling, branch, and subscription-stale-result fixes.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 undo/redo first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after fixing node-move undo redraw residue and removing undo/redo shortcuts.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 copy/paste first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 multi-select and multi-node copy/paste first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 zoom first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after fixing zoom percentage overlay residue during scroll.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 11 snap-to-grid first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 13.1 canvas visual and routed-connection pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 13.2 toolbox layout first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the toolbox category-rail feedback pass and the Phase 13.3 command-bar first pass.
  - [x] `TDJS-Vision.sln` builds successfully after the floating toolbox, tab-adjacent add button, image preview, and minimap pass.
  - [x] `TDJS-Vision.sln` builds successfully after the per-process run/loop/stop command pass and preview data-binding pass.
  - [x] `TDJS-Vision.sln` builds successfully after the auto-size process action strip, preview selector, preview cache, and AI draw optimization pass.
  - [x] `TDJS-Vision.sln` builds successfully after moving preview/minimap to independent overlay controls and fixing drag/selection overlay refresh cost.
  - [x] `TDJS-Vision.sln` builds successfully after the process-header reference-style pass and vector command button pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 14 multi-condition node first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the TDAI startup model-load guard.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 15 measurement caliper/circle/ellipse first implementation pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 15 measurement ROI interaction feedback pass.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 15 left-parameter-to-ROI realtime sync fix.
  - [x] `TDJS-Vision.sln` builds successfully after preserving dragged ROI position during caliper parameter edits and removing automatic image recentering.
  - [x] `TDJS-Vision.sln` builds successfully after the integrated template-matching UI, embedded-template persistence, and contour-erasing feedback pass.
  - [x] `TDJS-Vision.sln` builds successfully after removing the Result Display tab, restoring template creation/editing to a dialog, and adding saved search-region matching.
  - [x] `TDJS-Vision.sln` builds successfully after adding ROI confirmation and fixing template brush erasing contour filtering.
  - [x] `TDJS-Vision.sln` builds successfully after the ROI default-visibility and template/source refresh feedback pass.
  - [x] `TDJS-Vision.sln` builds successfully after the embedded-template restore hardening pass.
  - [x] `TDJS-Vision.sln` builds successfully after replacing the reopened-template 24bpp clone path.
  - [x] `TDJS-Vision.sln` builds successfully after the Phase 16 refresh-image/no-upstream-run and position-correction result publishing fixes.
  - [x] `TDJS-Vision.sln` builds successfully after the corrected-image caliper ROI baseline-normalization fix.
  - [x] `TDJS-Vision.sln` builds successfully after the node-result NG to failed-canvas-status feedback fixes.
  - [x] `TDJS-Vision.sln` builds successfully after failed-runtime-status downstream blocking fixes.
- Verified subscription regression points:
  - [x] A node with `A -> C` and `B -> C` shows both `A` and `B` as subscription candidates.
  - [x] Deleting a connection removes the disconnected node from subscription candidates.
  - [x] Saving and reopening restores the subscription relationship correctly.
- Current code point:
  - [x] Subscription candidate lookup now walks graph upstream connections.
  - [x] Connection add/delete and node-connection cleanup notify subscription controls to refresh.
  - [x] Multiple incoming paths contribute their upstream nodes to the same subscription candidate list.
  - [x] A disconnected subscription target is cleared when it is no longer upstream.
  - [x] `NodeBase` now exposes runtime status and elapsed-time text for canvas rendering.
  - [x] `ProcessFlowCanvas` refreshes affected node regions when node status changes.
  - [x] Running, success, failed, disabled, selected, and unexecuted states now have distinct canvas styling.
- Next task:
  - [x] Recheck `If = false` graph branch behavior in the UI.
  - [x] Recheck disabled upstream node behavior in the UI: `A -> B -> C`, disable `B`, then `C` should fail instead of reading `B`'s last result.
  - [x] Continue Phase 10 UI regression checks for simple, branch, failed, and stopped graphs.
  - [ ] Defer passive-triggered graph-flow regression check.
  - [x] Begin Phase 11 editor quality features.
  - [x] UI-verify the first undo/redo implementation pass.
  - [x] Continue with copy/paste node support.
  - [x] UI-verify the first copy/paste implementation pass.
  - [x] Continue with multi-node copy/paste and internal connection remapping.
  - [x] UI-verify multi-select, group move/delete, and multi-node copy/paste.
  - [x] Continue with zoom support.
  - [x] UI-verify zoom interactions.
  - [x] Continue with snap-to-grid support.
  - [ ] UI-verify snap-to-grid interactions.
  - [x] Begin Phase 13 flow editor UI layout polish.
  - [x] Implement Phase 13.1 canvas visual and connection polish.
  - [x] UI-verify Phase 13.1 canvas visual and routed-connection interactions.
  - [x] Implement Phase 13.2 toolbox layout first pass.
  - [ ] UI-verify Phase 13.2 toolbox collapse/expand and drag creation.
  - [x] Implement toolbox category rail and selected-category module grid per latest UI feedback.
  - [x] Implement maximized/resizable process editor window behavior.
  - [x] Implement compact process tab/action header and run command bar first pass.
  - [ ] UI-verify category rail switching, module drag creation, window resize, and compact command bar behavior.
  - [ ] UI-verify reference-style process header visuals and vector run/loop/stop command buttons.
  - [x] Convert selected-category modules to a floating flyout.
  - [x] Move the add-process `+` beside the process tabs.
  - [x] Add first-pass right-side image preview and bottom-right minimap overlays.
  - [x] Auto-size the compact process action strip so add/delete commands are not clipped.
  - [x] Bind the right-side preview overlay to selected-node image output data.
  - [x] Add a preview selector menu for process image outputs.
  - [x] UI-verify preview/minimap stay fixed without flicker during canvas scroll and zoom.
  - [x] UI-verify node dragging, multi-node dragging, and rubber-band selection remain responsive after overlay separation.
  - [ ] UI-verify floating module flyout, tab-adjacent process actions, preview overlay, and minimap navigation.
  - [x] Add Phase 14 multi-condition logic node plan to this task file.
  - [ ] Inspect existing `Node/6-LogicTool/If` node, params, result, save/restore, and graph branch behavior before implementation.
  - [ ] Design the new multi-condition node type, parameter model, result model, and toolbox creation entry.
  - [ ] Implement the multi-condition parameter UI with designer-visible controls.
  - [ ] Implement upstream `INodeResult` property discovery and condition source selection.
  - [ ] Implement runtime evaluation and True/False graph branch output.
  - [ ] Build-verify and UI-verify multi-condition node editing, save/load, and graph execution.

- [x] The flow editor has entered graph-aware subscription migration.
- [x] Subscription candidates should come from graph upstream nodes, not from ordered node list position.
- [x] Recalculate whether a subscribed node is still upstream after a connection is deleted or changed.
- [x] Show upstream subscription candidates from every incoming graph path when a node is reached by multiple connected nodes.
- [x] Keep old solution subscription text pending until restored graph connections can validate it.
- [x] Verify old solution subscriptions after opening a migrated flow and after save/load.

## Current Understanding

### Existing `TDJS-Vision` ProcessNew editor

- `ProcessEditPanel` creates and owns a `Process`.
- Nodes are created by dragging toolbox entries from `FormNewProcessWizard`.
- `CreateNode(NodeType, nodeName)` builds concrete `NodeBase` controls with a `switch`.
- Nodes are displayed as `NodeBase : UserControl` rows with `DockStyle.Top`.
- `Process.Run(...)` executes `Process.Nodes` in list order.
- Existing logic nodes use `NodeReturn.NextIndex` for ordered-list jumps.
- Parameters are edited in each node's own `ParamForm`.
- Save/restore uses `ProcessConfig`, `NodeConfig`, and `INodeParam`.
- `NodeSubscription` now builds its subscription candidate list from graph upstream nodes.

### Reference `TDVision` editor

- `FlowCanvasPanel` is a self-drawn graph canvas.
- A `Flow` contains `Nodes` and `Connections`.
- Nodes carry `Location`, `Size`, selection state, start-node state, parameters, and results.
- Dragging, selection, anchoring, connecting, undo/redo, and canvas drawing live in the canvas layer.
- Graph persistence saves the graph model instead of rebuilding a vertical UI list.
- Runtime execution traverses graph connections from explicit or implicit start nodes.

## Main Design Decisions

- [x] Use **Plan B** as the target direction.
- [x] Keep existing node parameter forms working during the canvas migration.
- [ ] Separate canvas display concerns from existing `NodeBase` runtime behavior as much as practical.
- [x] Persist graph layout and connections in the current solution format.
- [x] Keep old `.Sol` files loadable and give old ordered nodes a default canvas layout.
- [x] Use graph-native `If` branches for new flow-canvas condition flows.
- [x] Do not expose `Else` or `EndIf` as new flow-canvas toolbox nodes.
- [x] Keep old ordered `Else` and `EndIf` nodes loadable for existing solution compatibility.

## Phase 1: Graph Models And Compatibility

### Flow model changes

- [x] Extend process configuration to store graph data.
- [x] Add node canvas layout fields:
  - [x] `X`
  - [x] `Y`
  - [x] `Width`
  - [x] `Height`
  - [x] `IsStartNode`
- [x] Add a process connection model:
  - [x] `ConnectionId`
  - [x] `FromNodeId`
  - [x] `ToNodeId`
  - [x] `FromAnchor`
  - [x] `ToAnchor`
  - [x] `Branch`
- [x] Decide whether connection data lives only in `ProcessConfig` or is also kept in runtime `Process`.
- [ ] Define one node ID rule for graph operations inside a process.
- [ ] Define whether graph cycles are forbidden in the first implementation.

### Backward compatibility

- [x] Load old process configs that do not contain layout data.
- [x] Generate default positions for old nodes.
- [x] Decide whether old ordered nodes should be auto-connected in their old order.
- [ ] Preserve existing node parameters during old-solution migration.
- [ ] Preserve existing node subscriptions during old-solution migration.

## Phase 2: Canvas Foundation

### New canvas components

- [x] Add a new canvas control for `ProcessNew`.
- [ ] Add a canvas node display model or wrapper around the current runtime node.
- [ ] Add a canvas connection display model.
- [ ] Add a path routing helper for connection drawing.
- [x] Add hit-testing helpers for nodes, anchors, and connections.

### Basic rendering

- [x] Enable double-buffered drawing.
- [x] Draw a grid background.
- [x] Draw node bodies.
- [x] Draw node titles with node ID and node name.
- [x] Draw active, disabled, selected, running, success, and failed states.
- [x] Draw input/output anchor points.
- [x] Draw directed connection lines and arrow heads.
- [x] Support logical canvas coordinates separate from screen coordinates.
- [x] Support scrolling or panning for a canvas larger than the visible panel.

## Phase 3: Replace The ProcessNew List Editor UI

- [x] Add the new canvas control to `ProcessEditPanel.Designer.cs`.
- [x] Keep WinForms controls that must appear in the designer inside `.Designer.cs`.
- [x] Keep existing process-level buttons and status controls:
  - [x] Run
  - [x] Stop
  - [x] Clean status
  - [x] Enable switch
  - [x] Process priority
  - [x] Process group
  - [x] Process rename
  - [x] Log output switch
  - [x] Passive trigger switch
- [x] Replace the vertical node display panel with the canvas.
- [x] Remove or isolate `_stack` from UI layout responsibilities.
- [ ] Keep `Process.Nodes` available for runtime node ownership during migration.

## Phase 4: Toolbox Drag Creation

- [x] Keep the existing toolbox drag source in `FormNewProcessWizard`.
- [x] Make the canvas accept the existing `DragData`.
- [x] Create a node at the mouse drop position.
- [x] Reuse existing concrete node creation logic at first.
- [x] Synchronize new nodes into:
  - [x] `Solution.Instance.Nodes`
  - [x] `Process.Nodes`
  - [x] Canvas node collection
- [x] Assign default canvas size for newly created nodes.
- [x] Mark the newly created node as selected.
- [x] Refresh the process node count after creation.

## Phase 5: Core Canvas Editing

### Node interactions

- [x] Single-click select a node.
- [x] Drag one node to a new location.
- [x] Drag multiple selected nodes.
- [x] Rubber-band select nodes.
- [x] Delete selected nodes.
- [x] Remove connections attached to deleted nodes.
- [x] Rename a node from canvas interaction.
- [x] Enable or disable a node from canvas interaction.
- [x] Add or edit node remarks.
- [x] Open existing node parameter forms by double-clicking canvas nodes.

### Connection interactions

- [x] Start a connection drag from a node anchor.
- [x] Finish a connection on a target node anchor.
- [x] Draw the temporary connection during drag.
- [x] Select a connection.
- [x] Delete a connection.
- [x] Reject self-connections.
- [x] Reject duplicate connections.
- [ ] Define whether multiple outgoing connections are allowed for all node types.
- [ ] Define whether multiple incoming connections are allowed for all node types.
- [x] Add start-node selection behavior.

## Phase 6: Existing Parameter Form Compatibility

- [x] Keep double-click behavior connected to each node's current `ParamForm`.
- [x] Keep parameter save behavior based on `ParamForm.Params`.
- [x] Keep restore behavior:
  - [x] Assign saved `NodeParam` to `ParamForm.Params`.
  - [x] Call `SetParam2Form()`.
- [ ] Verify parameter forms for device-backed nodes still restore device choices.
- [ ] Verify image source parameter restore.
- [ ] Verify ROI parameter restore.
- [ ] Verify AI parameter restore.
- [ ] Verify script and logic parameter restore.
- [ ] Verify node removal still releases special resources such as AI handles.

## Phase 7: Subscription Migration

### First compatibility pass

- [x] Replace ordered `NodeSubscription` candidate lookup with graph upstream lookup.
- [x] Make renamed upstream canvas nodes refresh subscription display text.
- [x] Make deleted canvas nodes invalidate removed subscription targets.
- [x] Verify subscriptions restore after save/load.

### Graph-aware subscription pass

- [x] Decide the target subscription rule:
  - [x] Upstream graph nodes only.
  - [ ] Any node in the same process.
  - [ ] Node-type-specific rules.
- [x] Add graph upstream lookup for subscription candidates.
- [x] Include upstream nodes from all incoming graph paths when a node has multiple connected predecessors.
- [x] Refresh and validate subscription sources when connections are added, deleted, or changed.
- [x] Prevent invalid subscriptions from silently surviving graph edits.
- [x] Define how subscription data should be stored if visible node names change.
  - [x] Current pass keeps existing `ID.NodeName` text storage for compatibility and updates visible text on upstream node rename.

## Phase 8: Save And Restore

### Save

- [x] Save node canvas locations.
- [x] Save node sizes if sizes become editable.
- [x] Save start-node state.
- [x] Save graph connections.
- [x] Save existing node params without losing concrete parameter types.
- [ ] Keep current devices, global signals, and detect item persistence intact.

### Restore

- [x] Restore process tabs.
- [x] Create all runtime nodes first.
- [x] Restore node active and selected state.
- [x] Restore node parameter objects.
- [x] Push restored params back into parameter forms with `SetParam2Form()`.
- [x] Restore canvas positions.
- [x] Restore graph connections after all nodes exist.
- [x] Restore start-node state.
- [x] Validate broken connection references during load.

## Phase 9: Runtime Execution Migration

### Graph execution rules

- [x] Add runtime connection ownership to `Process`.
- [x] Define start-node execution rules.
- [x] Decide whether a process can have multiple start nodes.
- [ ] Decide how branches run:
  - [x] Sequentially.
  - [ ] In parallel.
- [x] Decide what a failed node does to downstream nodes.
- [x] Decide cancellation behavior across graph branches.
- [x] Decide whether disabled nodes are skipped or block downstream execution.
- [x] Prevent infinite execution when a graph cycle exists.

### Current transition runtime rules

- [x] Prefer explicit `IsStartNode` nodes.
- [x] Also run implicit source nodes outside explicit start-node components.
- [x] Traverse outgoing connections sequentially in connection creation order.
- [x] Complete each start-node component before moving to the next independent source component.
- [x] Visit each node at most once in one run to avoid graph cycles looping forever.
- [x] Skip disabled nodes and continue through their outgoing connections.
- [x] Stop the current graph run when a node throws or cancellation is requested.
- [x] Accept multiple persisted start nodes at runtime even though the current canvas menu keeps one explicit start node selected.
- [x] Treat new `If` connections from the `Right` anchor as True branches.
- [x] Treat new `If` connections from the `Bottom` anchor as False branches.
- [x] Infer canvas `If` branch semantics from restored `Right` and `Bottom` anchors when older saved data still has `Branch = Default`.
- [x] Persist and restore branch type with each connection.
- [x] Prevent disabled or branch-skipped upstream nodes from serving stale subscription results during the current run.
- [x] Track per-process run ids so subscription reads can distinguish current-run results from prior-run cached results.

### Existing logic compatibility

- [x] Review `If`, `Else`, and `EndIf` nodes.
- [x] Review nodes that return `NodeReturn.NextIndex`.
- [x] Define graph-native `If` branching for True/False connections.
- [x] Retire `Else` and `EndIf` from new graph-mode creation; keep old ordered nodes only for compatibility.
- [x] Define migration behavior for old ordered-list conditional flows.
- [x] Keep manual process run working during migration.
- [x] Keep passive-triggered process behavior working during migration.
- [x] Keep `RunForUpdateImages(...)` behavior available for parameter forms that need upstream images.
- [x] Use legacy `NodeReturn.NextIndex` jumps during the transition when an old logic node asks for an ordered-list jump.
- [x] Use `NodeReturn.NextBranch` when a graph-native `If` node selects its True or False connection set.

## Phase 10: Runtime State On Canvas

- [x] Show node execution state on canvas.
- [x] Show per-node execution time on canvas.
- [x] Refresh only affected node regions where practical.
- [x] Keep process run-time text updated.
- [x] Keep process run/stop button state updated.
- [x] Keep clean-status behavior working for all canvas nodes.

## Phase 11: Editor Quality Features

- [ ] Add undo and redo for graph edits.
  - [x] First code pass supports undo/redo for node creation, node deletion, node movement, connection creation/deletion, start-node changes, and node enable/disable changes.
  - [x] Added canvas context-menu entries for undo/redo.
  - [x] Temporarily removed Ctrl+Z/Ctrl+Y/Ctrl+Shift+Z shortcuts after UI feedback.
  - [x] Added context-menu undo/redo commands in `ProcessEditPanel.Designer.cs`.
  - [x] Node-count display refreshes after undo/redo changes that add or remove nodes.
  - [x] Deleted nodes are detached without releasing resources when the action is undoable.
  - [x] Fixed node-move undo redraw residue by invalidating the canvas after applying the restored location.
  - [x] UI-verify undo/redo edit scenarios.
- [ ] Add copy and paste for nodes.
  - [x] First code pass supports copying and pasting one selected node from the canvas context menu.
  - [x] First multi-node pass supports copying and pasting all selected nodes from the canvas context menu.
  - [x] Pasted nodes copy parameter objects through JSON cloning and then restore their parameter forms with `SetParam2Form()`.
  - [x] Pasted nodes copy node notes, active state, display size, and canvas placement offset from the last right-click location.
  - [x] Paste participates in the existing undo/redo node-create flow.
  - [x] UI-verify single-node copy/paste edit scenarios.
  - [x] UI-verify multi-node copy/paste edit scenarios.
- [ ] Remap node IDs and internal graph references during paste.
  - [x] Single-node paste assigns a fresh node ID through the normal `CreateNode` path.
  - [x] Pasted node names are made unique with a `_复制` suffix.
  - [x] Existing subscription text inside pasted parameters is preserved; it can resolve again after valid upstream graph connections are created.
  - [x] Multi-node paste remaps copied internal connections to the new node IDs.
  - [x] Multi-node paste remaps copied parameter string references when they point to nodes in the copied batch.
  - [x] UI-verify internal connection and copied subscription remapping.
- [ ] Add zoom if needed.
  - [x] First code pass supports right-click zoom in, zoom out, reset zoom, and `Ctrl + mouse wheel` zoom.
  - [x] Zoom keeps node canvas coordinates unchanged and scales only rendering, hit-testing, and scrollable canvas size.
  - [x] Current zoom percentage is displayed on the canvas.
  - [x] UI-verify zoom edit scenarios.
- [ ] Add minimap if needed.
- [ ] Add snap-to-grid if needed.
  - [x] First code pass adds a right-click menu toggle for snap-to-grid.
  - [x] Node drag and toolbox-created nodes snap to the canvas grid when enabled.
  - [x] Multi-node paste snaps the pasted group target while keeping copied node spacing intact.
  - [ ] UI-verify snap-to-grid edit scenarios.
- [ ] Improve connection routing around nodes.
- [x] Add context menus for canvas, nodes, and connections.

## Phase 12: Test And Verification Checklist

### Editing

- [ ] Create an empty process.
- [ ] Drag one node onto the canvas.
- [ ] Drag multiple nodes onto the canvas.
- [ ] Move nodes and save/load them.
- [ ] Connect nodes and save/load connections.
- [ ] Delete a node and confirm attached connections are removed.
- [ ] Delete a connection without deleting its nodes.
- [ ] Select and deselect nodes and connections.
- [ ] Double-click nodes and open current parameter forms.
- [ ] Undo/redo node creation.
- [ ] Undo/redo node movement.
- [ ] Undo/redo node deletion and attached connection restore.
- [ ] Undo/redo connection creation and deletion.
- [ ] Undo/redo start-node changes.
- [ ] Undo/redo node enable/disable changes.
- [x] Copy and paste one node.
- [x] Paste repeatedly and confirm unique IDs/names and offset locations.
- [x] Undo/redo pasted node creation.
- [x] Rubber-band select multiple nodes.
- [x] Drag selected nodes as a group.
- [x] Delete selected nodes as a group and undo/redo the deletion.
- [x] Copy and paste multiple connected nodes.
- [x] Confirm pasted internal connections use the new node IDs.
- [x] Confirm pasted internal subscription references use the new node text.
- [x] Zoom in and zoom out from the context menu.
- [x] Zoom in and zoom out with `Ctrl + mouse wheel`.
- [x] At non-100% zoom, verify node click, drag, rubber-band selection, connection creation, connection selection, and paste location.
- [ ] Enable snap-to-grid and drag one node.
- [ ] Enable snap-to-grid and drag selected nodes as a group.
- [ ] Enable snap-to-grid and paste copied nodes.
- [ ] Enable snap-to-grid at non-100% zoom and verify drag/drop placement.

### Parameters

- [ ] Change a simple numeric/string parameter and save/load it.
- [ ] Change image source parameters and save/load them.
- [ ] Change camera/device-backed parameters and save/load them.
- [ ] Change ROI parameters and save/load them.
- [x] Restore node subscriptions after save/load.

### Compatibility

- [ ] Open an old `.Sol` file without graph layout fields.
- [ ] Verify old nodes receive default canvas positions.
- [ ] Verify old node params restore correctly.
- [ ] Verify old devices restore before process nodes that depend on them.
- [ ] Save the migrated solution and reopen it.

### Runtime

- [x] Run a simple graph with one start node.
- [x] Run a graph with a branch.
- [x] Recheck `If = false` only follows False connections and does not continue through True/default downstream nodes.
- [x] Run a graph with disabled nodes.
- [x] Recheck disabled upstream node does not provide stale subscription data to downstream nodes.
- [x] Run a graph with a failed node.
- [x] Verify `A -> B` is not blocked by a failing standalone `C` because the `A -> B` component runs first.
- [x] Stop a running graph.
- [ ] Run passive-triggered graph flows. Deferred for now.
- [x] Verify runtime state refresh on canvas after the Phase 10 code pass.

## Phase 13: Flow Editor UI Layout Polish

Reference direction:

- Use the `TDVision` flow canvas as the near-term visual reference.
- Prefer a darker, denser, workbench-style canvas over the current bright engineering-grid look.
- Reuse the `TDVision` routed arrow connection idea instead of continuing with simple direct Bezier links.
- Keep current `ProcessNew` graph behavior, persistence, undo/redo, copy/paste, zoom, and snap-to-grid working while changing the presentation layer.

### Phase 13.1: Canvas Visual And Connection Baseline

- [x] Change the canvas background to a dark workbench surface.
- [x] Draw a subtle dark grid with enough contrast for node alignment.
- [x] Restyle nodes to be cleaner and less bulky while keeping runtime status text visible.
- [x] Port the `TDVision` routed arrow path approach into `ProcessFlowCanvas`.
- [x] Update connection hit-testing to match routed paths.
- [x] Keep branch labels visible on `If` true/false connections.
- [x] Build-verify after the first visual pass.
- [x] UI-verify simple connections, branch connections, selected connection highlighting, and temporary connection dragging.

### Phase 13.2: Toolbox Density And Navigation

- [x] Reduce the current left tree's visual weight.
- [x] Evaluate whether to keep tree categories or switch to a compact icon/category rail similar to the reference.
- [x] Replace the visible tree with a compact category rail and selected-category module grid.
- [x] Convert the selected-category module grid to a floating flyout instead of a fixed layout column.
- [x] Keep existing drag source behavior and `DragData` compatibility.
- [x] Ensure icons and labels remain readable in Chinese.
- [x] Add a left-side hide/show interaction for the toolbox.
- [x] Generate toolbox icons from category/node type names.
- [x] Build-verify after the first toolbox pass.
- [ ] UI-verify toolbox drag creation after layout changes.
- [ ] UI-verify toolbox collapse and expand.

### Phase 13.3: Process Header And Command Bar

- [x] Make process tabs and run/stop/clean/enable commands feel like one editor header.
- [x] Reduce empty space around the command buttons.
- [x] Place the add-process `+` beside the process tab strip.
- [x] Keep existing run status, elapsed time, node count, and enable switch behavior.
- [x] Embed the active process controls as single run, loop run, and stop in the per-process top-right command bar.
- [x] Restyle process tabs with reference-style red status dots and dark selected/inactive tab states.
- [x] Replace low-resolution run/loop/stop images with vector-drawn command glyphs.
- [ ] UI-verify process switching, add button alignment, single run, loop run, and stop.
- [ ] UI-verify top header matches the provided reference closely enough.

### Phase 13.4: Image And Result Preview Area

- [x] Decide whether the right-side preview belongs in `ProcessNew` now or should remain a later integration.
- [x] Add a first-pass floating right-side preview panel inside the canvas.
- [x] Bind the preview panel to selected-node image/result data.
- [x] Add a selector for manually choosing available process image outputs.
- [ ] Keep preview panel optional or collapsible so small screens still prioritize the flow canvas.
- [ ] UI-verify selected-node result display and fallback empty states.

### Phase 13.5: Minimap And Viewport Polish

- [x] Add a minimap inspired by `TDVision.UI.MinimapPanel`.
- [ ] Keep minimap optional from the canvas context menu.
- [x] Ensure minimap works with zoom and scroll viewport navigation in the first pass.
- [ ] Ensure minimap works with selection and snap-to-grid.
- [ ] UI-verify large graphs with many nodes and connections.

## Phase 14: Multi-Condition Logic Node

Reference direction:

- Use the existing `Node/6-LogicTool/If` node as the behavior and branch-output reference.
- Add a new logic node that can evaluate multiple user-defined conditions instead of a single fixed `If` expression.
- The condition editor should let the user pick an upstream node and one of that node result object's public properties, usually from `INodeResult`.
- The UI reference is the provided condition-checking panel: the condition cell should support opening a picker tree that lists upstream nodes and selectable result properties.
- Keep graph-native branch behavior aligned with the current flow-canvas `If`: True should continue through the True branch, False through the False branch.

### Phase 14.1: Requirement And Model Design

- [x] Inspect current `NodeIf`, `NodeParamIf`, `NodeResultIf`, and parameter form implementation.
- [x] Decide the new node name and `NodeType` entry, for example `MultiCondition`, `ConditionCheck`, or another project-appropriate Chinese display name.
- [x] Define a serializable condition parameter model:
  - [x] Condition name.
  - [x] Optional note/comment.
  - [x] Source node ID and display text.
  - [x] Source result property path.
  - [x] Comparison operator.
  - [x] Expected value or expected range.
  - [x] Value type metadata for restore and UI editing.
- [x] Define the whole-node match mode:
  - [x] All conditions must pass.
  - [x] Any condition may pass.
  - [x] Reserve custom expression/grouping for a later pass if needed.
- [x] Define the result model so downstream nodes can read:
  - [x] Overall boolean result.
  - [x] Per-condition pass/fail details.
  - [ ] Optional display text for logs/result tab.

### Phase 14.2: Upstream Result Property Picker

- [x] Reuse graph upstream lookup rules so the picker only lists valid upstream nodes.
- [x] List upstream nodes by `ID.NodeName`, with a `None` option.
- [x] Reflect over each upstream node's `Result` object and list public readable properties from `INodeResult` implementations.
- [x] Support common scalar property types first: `bool`, numeric types, `string`, enum, and nullable versions.
- [x] Decide first-pass behavior for complex properties such as images, collections, and custom result objects.
- [x] Persist source references by stable node ID plus property path, not only by visible text.
- [x] Refresh or invalidate source selections when graph connections change, upstream nodes are deleted, or upstream nodes are renamed.

### Phase 14.3: Parameter UI

- [x] Build the parameter form based on the existing `If` parameter UI style.
- [x] Add designer-visible controls in `.Designer.cs` files, following the project WinForms rule.
- [x] Support add, delete, and edit condition rows.
- [x] Provide a condition source picker opened from the condition cell/link button.
- [x] Provide operator and value editors appropriate to the selected source property type.
- [x] Keep Chinese labels readable and compact.
- [ ] Add a result display tab or area for overall result and per-condition evaluation details.

### Phase 14.4: Runtime Evaluation And Flow Branching

- [x] On run, validate all selected source nodes are upstream and have successful results for the current process run.
- [x] Read the selected `INodeResult` property value by reflection or a small typed accessor helper.
- [x] Evaluate each condition according to its operator and value type.
- [x] Apply the selected match mode to produce the overall boolean result.
- [x] Return `NodeReturn.NextBranch` as True or False so existing graph-native branch routing can execute the correct outgoing connection.
- [x] Log clear failure messages when a source node/result/property is missing or has not run in the current process run.
- [x] Avoid falling back to legacy ordered-list continuation for graph-native False paths.

### Phase 14.5: Save/Restore And Compatibility

- [x] Save and restore all condition rows through the existing node parameter persistence path.
- [x] Keep old `.Sol` files loadable when they do not contain the new node type.
- [x] Ensure renamed upstream nodes keep stable node-ID based references and update visible display text.
- [x] Ensure copied/pasted multi-condition nodes remap internal upstream references when the source nodes are copied in the same batch.
- [x] Ensure deleting graph connections clears or marks now-invalid condition sources.

### Phase 14.6: Verification

- [x] Build-verify after adding the node type and UI.
- [ ] UI-verify adding, deleting, and editing multiple condition rows.
- [ ] UI-verify upstream result property picker with one upstream node and multiple upstream nodes.
- [ ] UI-verify save/load restores condition rows and selected properties.
- [ ] Runtime-verify All mode True and False outcomes.
- [ ] Runtime-verify Any mode True and False outcomes.
  - [ ] Runtime-verify missing/stale upstream results fail clearly instead of reading old results.
  - [ ] Runtime-verify True and False outgoing graph branches from the new node.

## Phase 15: OpenCV Caliper, Circle, And Ellipse Tool Integration

Reference source:

- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\卡尺工具\CaliperLineTool.cs`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\找圆工具\CaliperCircleTool.cs`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\找椭圆工具\FindEllipseTool.cs`
- UI/ROI reference: `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\ShowImageControl.cs`

### Phase 15.1: Algorithm Understanding

- [x] `CaliperLineTool` samples multiple rectangular calipers along a base line, searches each caliper profile for an edge point, and fits the final line from valid edge points.
- [x] `CaliperCircleTool` samples radial calipers around a base circle or arc, searches edges along each radial profile, and fits the final circle from at least three edge points.
- [x] `FindEllipseTool` samples normal-direction calipers around a rotated base ellipse, searches edges along each profile, and fits the final ellipse from at least five edge points.
- [x] Shared parameters include caliper count, caliper width/height, edge strength threshold, edge polarity, and edge find mode.
- [x] Shared edge logic supports `DarkToLight`, `LightToDark`, and `Both`, plus `First`, `Last`, and `Best` candidate selection.
- [x] Shared precision path uses local gradient peak interpolation for sub-pixel edge coordinates.

### Phase 15.2: Integration Design

- [x] Decide to add three new measurement nodes instead of changing existing `FindLine` and `FindCircle` detection nodes.
- [x] Extract the reusable caliper edge-search code into a project namespace shared by line, circle, and ellipse tools.
- [x] Normalize common enums and result types so all three tools expose consistent output to downstream `INodeResult` consumers.
- [x] Keep algorithm input based on current project `OutputImage`/`Mat` conventions instead of repeatedly converting through `Bitmap` when a `Mat` is already available.
- [x] Define result display objects: fitted geometry, edge points, point count, success flag, message, and annotated output image.

### Phase 15.3: ROI And Parameter UI

- [x] Add missing circle-caliper and ellipse-caliper ROI support to the current `Forms\DispShowImage\ShowImageControl.cs`, using the reference control as the behavior guide.
- [x] Add all WinForms controls for new/changed parameter forms in `.Designer.cs` files so the Visual Studio designer can display them.
- [x] Provide line, circle, and ellipse ROI editing with caliper count, width, height, edge strength, polarity, and find mode controls.
- [x] Preserve save/restore compatibility for algorithm parameters through the existing node parameter persistence path.
- [x] Ensure parameter forms can preview edge points and fitted results on the preview image.

### Phase 15.4: Node Runtime

- [x] Implement or upgrade the line caliper node runtime.
- [x] Implement or upgrade the circle caliper node runtime.
- [x] Implement the ellipse caliper node runtime.
- [x] Register any new node types in toolbox XML, language JSON files, `NodeType`, node creation switches, and project file entries.
- [x] Ensure result properties are discoverable by the new multi-condition node's upstream `INodeResult` picker.
- [x] Add clear failure messages for missing images, insufficient edge points, and failed fitting.

### Phase 15.5: Verification

- [x] Build-verify after adding the shared algorithm layer.
- [x] Build-verify after adding ROI/control support.
- [x] Build-verify after adding each node family.
- [ ] Runtime-verify line, circle, and ellipse tools on synthetic images with known geometry.
- [ ] UI-verify ROI editing, preview rendering, save/load, and graph execution.

### Phase 15.6: Image/Overlay Layer Separation

- [x] Add `OutputImage.DisplayResult` so node output images can remain clean while result geometry/text is rendered by `ShowImageControl`.
- [x] Add `ShowImageControl.SetImage(Bitmap, AlgorithmResult)` and `SetDisplayResult(...)` to draw result rectangles, lines, circles, ellipses, and text as static overlay ROI objects.
- [x] Route `ImageShow` and `FrmSingleImage` through the new image-plus-overlay display path.
- [x] Change caliper line, caliper circle, and caliper ellipse nodes so runtime output keeps the original image clean and sends edge points/fitted geometry/text through `DisplayResult`.
- [x] Change template matching, color discern, binary analysis, find line, find circle, and battery-ear runtime outputs toward clean image plus display overlay.
- [x] Keep parameter-form preview drawing behavior where it is only used for local UI tuning and does not become the node's runtime output image.
- [x] Build-verify after display-layer migration.
- [ ] Runtime-verify full-process display on real solution images after zooming/panning, confirming result shapes/text no longer pollute source pixels.
- [ ] Review explicit result-image tools such as `ImageDraw`; keep them as intentional bitmap writers or split them into display overlay mode if needed.

### Phase 15.7: Fast Rotated Template Matching Tool

- [x] Trace old template creation flow from `WookTwoParamsWindow.button13_Click`: rotated ROI crop, 24bpp template normalization, and template model persistence intent.
- [x] Trace old runtime matching flow through `OpenCvVisionAdapter.MatchTemplate(...)` and the `Fastest_Image_Pattern_Matching` `MatchTool.dll` P/Invoke API.
- [x] Add `FastTemplateMatcher` wrapper for `CreateMatchEngine`, `LearnPatternMem`, and `MatchMem`, including 24bpp conversion, rotated template crop, match box conversion, score filtering, and optional outline extraction.
- [x] Update the current `MatchTemplate` parameter form to expose fast-match parameters: score, max result count, angle tolerance, angle step, max overlap, coarse match, outline display, and match-box display.
- [x] Update `TemplateCreate` to use `ShowImageControl` dynamic rotated rectangles so template ROI creation is visible in the WinForms designer/control layout path and supports rotation.
- [x] Route runtime template-match output through clean `OutputImage.Bitmaps` plus `OutputImage.DisplayResult`, including rotated boxes, outline contours, text, score, angle, and elapsed time.
- [x] Add native runtime dependencies under `Native/`: `MatchTool.dll` and `opencv_world4100.dll`, with project copy-to-output configuration.
- [x] Build-verify after fast template matching migration (`MSBuild`, 0 errors; existing warnings remain).
- [x] Feedback pass: merged template creation into `NodeParamFormMatchTemplate` so template import, rotated-ROI creation, matching preview, and template editing live in one designer-visible form.
- [x] Feedback pass: changed `NodeParamFormMatchTemplate` to a tabbed layout matching the requested contour-match UI structure: Basic Parameters, Feature Template, Run Parameters, and Result Display.
- [x] Feedback pass: template images are now saved into `NodeParamMatchTemplate.TemplateImageBytes` with the solution file; external template paths remain only as legacy/import fallback.
- [x] Feedback pass: template contour preview is shown directly in the parameter form, and brush-based detail erasing edits the template image so simplified templates can reduce learned contour/detail cost.
- [x] Removed the old standalone `TemplateCreate` form from the project after integrating creation into the main template-matching UI.
- [x] Build-verify after integrated template UI and embedded-template persistence (`MSBuild`, 0 errors; existing warnings remain).
- [x] Feedback pass: dynamic ROI overlays are now hidden by default, template creation opens with a refreshed fitted source image, template ROI creation no longer requires a separate confirm step, confirmed search ROIs are cleared, and saved search regions run after reopening without needing a visible ROI.
- [x] Feedback pass: hardened solution reload for embedded templates so GDI+ decode/preview failures do not surface as template restore failure when the template bytes are still recoverable.
- [ ] Runtime-verify on real product images with rotated targets, confirming match center, angle, score, overlap filtering, and outline display are correct.
- [ ] Runtime-verify deployed/release output folder contains native dependencies and can load `MatchTool.dll` without `DllNotFoundException` or OpenCV native dependency errors.

## Recommended Implementation Order

## Phase 18: Composite Geometry Measurement Tools

Reference source:

- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\线到线夹角工具`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\点到点距离工具`
- `D:\MyCode\PrivateMyComputerCoder\Test\_OpenCv颜色2\点到区域距离工具`

### Phase 18.1: Algorithm And Data Source Design

- [x] Add shared geometry measurement helpers for line-line angle, point-point distance, and point-region min/max distance.
- [x] Add reflection-based result readers so composite tools can subscribe upstream line, point, and region/contour outputs.
- [x] Keep pixel/sub-pixel precision modes from the reference tools.
- [x] Use existing caliper algorithms for local drawn line and point measurement: line calipers for line-line angle, circle calipers for point extraction.

### Phase 18.2: Node And UI Integration

- [x] Add `LineLineAngle`, `PointPointDistance`, and `PointRegionDistance` node families under `Node\4-Measurement`.
- [x] Add designer-visible WinForms parameter forms with Basic Parameters and Run Parameters tabs only.
- [x] Add source mode selection between subscribe and draw.
- [x] Add position-correction controls for draw-mode baseline ROI geometry.
- [x] Register the new nodes in `NodeType`, process node creation, toolbox XML, language JSON files, and project compile items.

### Phase 18.3: Verification

- [x] UTF-8 parse-verify language JSON and toolbox XML after adding the new node entries.
- [x] Static registration sweep confirmed project compile items, toolbox tags, language keys, node icons, and node factory cases.
- [x] Feedback pass: subscribe mode hides Run Parameters for line-line angle, point-point distance, and point-region distance tools.
- [x] Feedback pass: draw-mode line-line and point-point tools now keep independent per-ROI caliper run parameters, with an ROI selector for tuning the active ROI.
- [x] Feedback fix: Confirm ROI now preserves edge threshold, blur size, edge polarity, edge pick mode, and search direction instead of reverting non-geometry run parameters to defaults.
- [x] Build-verify after registration.
  - Passed on 2026-05-25 with MSBuild, 0 errors; existing warnings remain.
- [ ] UI-verify parameter forms can refresh images, draw/confirm ROIs, and run local previews.
- [ ] Runtime-verify subscribed upstream results and draw-mode caliper measurement on real images.

## Phase 19: Configurable ROI And Result Drawing Tool

Reference source:

- `D:\MyCode\PublicWook\TDJS-Vision\Node\7-ResultProcessing\ImageDraw`

### Phase 19.1: Requirement Capture

- [x] New request captured: add a drawing tool that can subscribe to a selected module's image, text, fitted line, or region result and draw it onto/over an output image.
- [x] The tool should support choosing the source module/result item instead of being limited to AI/color detection results.
- [x] Text drawing should support font size and placement: top-left, top-right, bottom-left, and bottom-right.
- [x] Geometry drawing should support at least rectangle line width, and should be extendable to lines and regions/contours.
- [x] Draw items should support user-selectable colors, reusing the current `ColorText`, `ColorLine`, `ColorContour`, and rectangle color conventions where possible.
- [x] Use the existing `ImageDraw` module's subscription and bitmap-drawing behavior as the first reference.
- [x] Preferred first direction: keep the input image pixels clean and emit non-destructive `OutputImage.DisplayResult` overlays for display.
- [ ] Decide whether a later optional mode should also write drawings into image pixels for image export/archiving.

### Phase 19.2: Framework Fit Discussion

- [x] Review current result data surfaces: `OutputImage`, `AlgorithmResult.Texts`, `AlgorithmResult.Lines`, rectangles, circles, ellipses, contours, and measurement result objects.
- [x] Decide the source picker design: one image subscription plus per-item result subscriptions stored on each draw item.
- [x] Decide how to represent manually configured draw items: text item, line item, rectangle item, and region/contour item in the first version.
- [x] Decide the style model for draw items: color, line width, text font size, text corner placement, and margins.
- [x] Decide save/restore model for multiple draw items and per-item style settings.
- [x] Decide the node belongs under `7-ResultProcessing` as a new overlay-only successor beside the old `ImageDraw`.

### Phase 19.3: Planned Implementation Items

- [x] Add a new `ResultOverlayDraw` node family for configurable ROI/result drawing.
- [x] Add designer-visible WinForms controls in `.Designer.cs` for image source, result source, draw-item list, style settings, and preview.
- [x] Support text placement in all four corners with safe margins.
- [x] Support configurable color and line width for rectangles, lines, and regions/contours, and extend the shared display-result style model for other geometry.
- [x] Support drawing subscribed fitted lines and subscribed regions/contours.
- [x] Preserve current `ImageDraw` compatibility unless explicitly replacing it.
- [x] Feedback fix: `ResultOverlayDraw` is failure-tolerant in graph runtime; failed per-item ROI/text sources are skipped, while the input image subscription remains strict.
- [x] Feedback fix: sequential graph runtime now records upstream node exceptions and continues scheduling failure-tolerant nodes such as `ResultOverlayDraw` before surfacing the original failure.
- [x] Feedback fix: `ResultOverlayDraw` draw items now skip missing/invalid subscribed geometry or throwing result-property getters instead of failing the ROI drawing node.
- [x] Feedback pass: `ResultOverlayDraw` supports applying current color, line width, and text size to all draw items at once.
- [x] Feedback pass: subscribed text draw items support a text prefix so scalar values such as caliper length can be displayed with a label.
- [x] Build-verify after implementation.
- [x] Build-verify after the `ResultOverlayDraw` failure-tolerant runtime fix.
- [ ] UI-verify the new form inside the running application.

## Phase 20: Edge Point Finding Measurement Tool

### Phase 20.1: Requirement And Scope

- [x] Add a new measurement toolbox node named `FindPoint` / `找点`.
- [x] Keep it as a new node under `Node\4-Measurement` instead of changing existing `LineFind`, `CircleFind`, or caliper tools.
- [x] The node input is one subscribed image.
- [x] The user draws and confirms one or more ROI regions on the image.
- [x] Runtime finds edge points inside the confirmed ROI regions by tracing edge contours along the material edge.
- [x] Runtime output should keep image pixels clean and publish overlay geometry through `OutputImage.DisplayResult`.
- [x] The tool should expose point/contour results so downstream `PointPointDistance`, `PointRegionDistance`, `ResultOverlayDraw`, `MultiCondition`, and future tools can subscribe to them.

### Phase 20.2: Algorithm Design

- [ ] Reuse OpenCV primitives already available in the project: grayscale conversion, optional smoothing, gradient/Canny-style edge extraction, ROI masking, and contour tracing.
- [ ] Treat the ROI as a mask. Edge extraction should ignore pixels outside the confirmed ROI.
- [ ] Provide first-pass run parameters: smoothing kernel, edge threshold or low/high threshold, minimum contour length, maximum point count or sampling step, and contour selection mode.
- [ ] Support edge polarity where practical: dark-to-light, light-to-dark, and both. If the contour-tracing path cannot directly encode polarity, use gradient sign filtering before contour extraction.
- [ ] Return ordered contour point groups from `Cv2.FindContours`/edge-following, plus a flattened point list for simple point consumers.
- [ ] Add optional point simplification/sampling to avoid producing huge point lists on noisy edges.
- [ ] Reject empty ROI, missing image, no edge found, or too-few-point cases with clear messages.

### Phase 20.3: Parameter UI

- [ ] Add designer-visible WinForms controls in `.Designer.cs`.
- [ ] Use the same general structure as recent measurement tools: Basic Parameters and Run Parameters.
- [ ] Basic Parameters: image subscription, Refresh Image, Draw ROI, Confirm ROI, Clear ROI, Run, and preview image.
- [ ] Run Parameters: smoothing, threshold, polarity, contour mode, minimum contour length, point sampling/simplification, and display toggles.
- [ ] If multiple ROIs are supported in the first pass, add an ROI selector so each ROI can keep independent run parameters when needed.
- [ ] Add position-correction controls only if the first implementation uses saved baseline ROI geometry; otherwise reserve this for a follow-up pass after the core tool is stable.

### Phase 20.4: Result Model

- [ ] Add `NodeResultFindPoint` with `IsOk`, `RunTime`, `PointCount`, `ContourCount`, `Points`, `Contours`, selected/best contour, message, and `OutputImage`.
- [ ] Add small result value types when useful, for example `MeasuredPointSet` or `MeasuredContour`, under `Node\4-Measurement\Common`.
- [ ] Extend `MeasurementResultReader` so downstream measurement/overlay tools can read this node as a point set and as a region/contour source.
- [ ] Display found points/contours in `OutputImage.DisplayResult` using existing `ColorContour`/point overlay conventions where possible.

### Phase 20.5: Registration And Integration

- [ ] Add `NodeType.FindPoint`.
- [ ] Add the `Node\4-Measurement\FindPoint` node family: node, parameter, parameter form, result, and any helper classes.
- [ ] Register the node in `ProcessEditPanel.CreateNode`.
- [ ] Register the node in `ToolTreeView.xml` under `测量工具`.
- [ ] Add language keys in `Languages\zh-CN.json` and `Languages\en-US.json`.
- [ ] Add project compile items and icon/color mapping if required by the current toolbox generation path.

### Phase 20.6: Verification

- [ ] Build-verify after first implementation.
- [ ] UI-verify image refresh, draw ROI, confirm ROI, clear ROI, save/load, and preview run.
- [ ] Runtime-verify on a simple synthetic image with known straight/curved edges.
- [ ] Runtime-verify on real product images that edge points are found along the material edge and do not include unrelated texture/noise.
- [ ] Runtime-verify downstream subscription from `PointRegionDistance`, `ResultOverlayDraw`, and `MultiCondition`.

1. [x] Add graph layout and connection models with backward-compatible save/load defaults.
2. [x] Add the `ProcessNew` canvas control and draw nodes from current runtime nodes.
3. [x] Replace the vertical node list in `ProcessEditPanel` with the canvas.
4. [x] Support toolbox drag creation and node dragging.
5. [x] Preserve parameter form open/save/restore behavior.
6. [ ] Add connection editing and start-node state.
7. [x] Persist and restore canvas nodes plus connections.
8. [ ] Migrate subscription rules from ordered nodes toward graph-aware upstream nodes.
9. [ ] Migrate `Process.Run(...)` from ordered execution to connection-driven execution.
10. [ ] Add editor productivity features and broad regression verification.

## First Deliverable

The first usable milestone should include:

- [x] `ProcessEditPanel` shows a free canvas instead of a vertical node list.
- [x] Toolbox drag creates nodes at the drop position.
- [x] Nodes can be selected and moved.
- [x] Existing parameter forms still open from canvas nodes.
- [x] Nodes can be connected and disconnected.
- [x] Node positions and connections survive save/load.
- [x] Old solutions load into an auto-laid-out canvas.

After this milestone, the editor will visibly become a flow-canvas editor while the deeper graph execution migration continues under Plan B.
