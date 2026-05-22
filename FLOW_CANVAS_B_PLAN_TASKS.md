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

The current implementation step is **Phase 10: Runtime State On Canvas**.

### Latest Progress

- Completed through the first **Phase 10: Runtime State On Canvas** code pass.
- Fixed one graph runtime scheduling regression found during Phase 10 checks:
  - [x] For `A -> B` plus standalone `C`, running `A` now completes the `A -> B` component before moving to independent source node `C`.
- Build verification:
  - [x] `TDJS-Vision.sln` builds successfully after the scheduling fix.
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
  - [ ] Run Phase 10 UI regression checks for simple, branch, failed, stopped, and passive-triggered graphs.

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
- [ ] Drag multiple selected nodes.
- [ ] Rubber-band select nodes.
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
- [x] Persist and restore branch type with each connection.

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
- [ ] Add copy and paste for nodes.
- [ ] Remap node IDs and internal graph references during paste.
- [ ] Add zoom if needed.
- [ ] Add minimap if needed.
- [ ] Add snap-to-grid if needed.
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

- [ ] Run a simple graph with one start node.
- [ ] Run a graph with a branch.
- [ ] Run a graph with disabled nodes.
- [ ] Run a graph with a failed node.
- [x] Verify `A -> B` is not blocked by a failing standalone `C` because the `A -> B` component runs first.
- [ ] Stop a running graph.
- [ ] Run passive-triggered graph flows.
- [ ] Verify runtime state refresh on canvas after the Phase 10 code pass.

## Recommended Implementation Order

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
