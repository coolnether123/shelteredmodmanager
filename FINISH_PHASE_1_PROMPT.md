# Cortex.Host.ImGui - Phase 1 Completion Prompt

## Context

You've built the Phase 1 foundation for Cortex.Host.ImGui in the past 20 minutes. The scaffolding is solid:
- Project structure created
- ImGui windowing and rendering initialized
- Panel renderers for explorer, editor, output, search
- Bridge client integration
- Input dispatcher
- State management

## Current State Assessment

✅ **Complete:**
- Project structure and dependencies
- ImGuiDesktopHost with game loop and event handlers
- ImGuiController for OpenGL rendering
- ImGuiWorkbenchRenderer with main menu and docking
- ImGuiPanelRenderer with syntax highlighting
- ImGuiExplorerRenderer with file tree navigation
- ImGuiInputDispatcher with intent queueing
- State persistence (ImGuiShellStateStore)
- All major event handlers (snapshot, connection, lifecycle)

⚠️ **Needs Verification/Completion:**
1. **Compilation** - Code compiles without errors
2. **Keyboard input** - ImGuiController properly handles keyboard events
3. **Code syntax highlighting** - Verify DrawHighlightedLine() is fully implemented
4. **Search results interaction** - Clicking search results sends to game
5. **Tooltip rendering** - Hover tooltips display properly
6. **Layout persistence** - Dock layout saves/loads correctly
7. **Output scrolling** - Auto-scroll works in output panel
8. **Bridge integration** - All event subscriptions fire correctly

## Phase 1 Definition of Done

Phase 1 is complete when you can:
1. ✅ Build the project without compilation errors
2. ✅ Launch the executable
3. ✅ See a window with ImGui interface
4. ✅ Connect to a game process via named pipes
5. ✅ Receive and display workbench snapshots
6. ✅ Click files in explorer and send to game
7. ✅ Type in search box and run search
8. ✅ Navigate code editor with keyboard/mouse
9. ✅ See status messages and logs in output
10. ✅ Professional appearance (dark theme applied)

## Your Tasks - Priority Order

### Task 1: Verify & Fix Compilation (Critical)
**Goal:** Cortex.Host.ImGui builds successfully with no errors

**What to do:**
1. Build the project: `dotnet build Cortex.Host.ImGui -c Debug`
2. If compilation errors occur, identify them:
   - Missing method implementations
   - Incorrect type references
   - Missing using statements
   - API mismatches (ImGui.NET version, OpenTK version)
3. Fix errors systematically
4. Ensure no warnings about unused code

**Success criteria:** Build succeeds with 0 errors, 0 warnings

**Files to check if errors occur:**
- ImGuiDesktopHost.cs - Check BuildNativeWindowSettings() exists
- ImGuiController.cs - Check CreateDeviceResources() and RenderDrawData() are implemented
- ImGuiPanelRenderer.cs - Check DrawHighlightedLine() is implemented
- All Bridge/*.cs files - Verify they're complete

---

### Task 2: Complete Missing Method Implementations
**Goal:** All methods called throughout the code are fully implemented

**What to do:**
1. Search for TODO comments or incomplete methods (if any exist)
2. Check these specific methods are fully implemented:
   - `ImGuiController.CreateDeviceResources()` - Must create GL buffers and shader
   - `ImGuiController.RenderDrawData()` - Must render ImGui draw list
   - `ImGuiController.UpdateImGuiInput()` - Must handle keyboard/mouse state
   - `ImGuiPanelRenderer.DrawHighlightedLine()` - Must highlight code with syntax colors
   - `ImGuiWorkbenchRenderer.ApplyDefaultWindowLayout()` - Must set default dock layout
   - `ImGuiDesktopHost.BuildNativeWindowSettings()` - Must create window config

3. If any methods are incomplete/stubbed, implement them fully

**Success criteria:** All method calls resolve to complete implementations

---

### Task 3: Verify Keyboard Input Flow
**Goal:** Keyboard input works end-to-end from window to game

**What to do:**
1. Verify `ImGuiController.UpdateImGuiInput()` handles:
   - `KeyboardState.IsKeyDown(Keys.*)` for key state
   - Mouse position from `MouseState`
   - Mouse button state
   - Scroll wheel
   - Modifier keys (Ctrl, Shift, Alt)

2. Verify `ImGuiDesktopHost.OnTextInput()` passes text to controller

3. Verify ImGui keyboard state is properly set in controller Update method:
   ```csharp
   io.KeyCtrl = KeyboardState.IsKeyDown(Keys.LeftControl) || KeyboardState.IsKeyDown(Keys.RightControl);
   io.KeyShift = KeyboardState.IsKeyDown(Keys.LeftShift) || KeyboardState.IsKeyDown(Keys.RightShift);
   io.KeyAlt = KeyboardState.IsKeyDown(Keys.LeftAlt) || KeyboardState.IsKeyDown(Keys.RightAlt);
   ```

4. Test: Launch and verify you can type in search box, select items with arrow keys

**Success criteria:** Keyboard input appears in ImGui widgets (text input, buttons respond to clicks)

---

### Task 4: Verify Code Highlighting
**Goal:** Syntax highlighting renders correctly with distinct colors

**What to do:**
1. Check `ImGuiPanelRenderer.DrawHighlightedLine()` implementation:
   - Should tokenize code (keywords, strings, comments, numbers, symbols)
   - Should color each token appropriately
   - Should preserve whitespace/indentation
   - Should handle multi-line strings

2. Verify colors are readable:
   - Text: Light gray (0.89, 0.92, 0.95)
   - Keywords: Blue (0.35, 0.62, 0.91)
   - Types: Cyan (0.32, 0.79, 0.69)
   - Strings: Orange (0.84, 0.58, 0.42)
   - Numbers: Green (0.71, 0.83, 0.64)
   - Comments: Green (0.45, 0.69, 0.40)

3. Test: Open a code file and verify colors are applied

**Success criteria:** Code displays with readable syntax highlighting, colors are distinct

---

### Task 5: Verify Search Results Interaction
**Goal:** Clicking search results sends selection to game

**What to do:**
1. In `ImGuiWorkbenchRenderer.DrawSearch()`, after displaying search results:
   - Add clickable items for each search result
   - On click, call `inputDispatcher.EnqueueOpenSearchResult(resultIndex)`
   - Results should highlight on hover

2. Verify the search results display the correct information:
   - File path
   - Line number with highlighted line
   - Context text (surrounding code)

3. Test: Run a search and click a result

**Success criteria:** Clicking search results triggers intent to game, status shows "Queued open search result"

---

### Task 6: Verify Bridge Integration
**Goal:** Bridge client properly connects and receives snapshots

**What to do:**
1. Check `ImGuiDesktopHost` constructor properly initializes bridge:
   ```csharp
   _bridgeClient = new NamedPipeDesktopBridgeClient(_options.BridgeClient, _options.LaunchToken);
   _bridgeClient.ConnectionStatusChanged += OnConnectionStatusChanged;
   _bridgeClient.SnapshotReceived += OnSnapshotReceived;
   _bridgeClient.OperationStatusReceived += OnOperationStatusReceived;
   _bridgeClient.OverlayLifecycleReceived += OnOverlayLifecycleReceived;
   ```

2. Verify `ImGuiDesktopHost.OnLoad()` calls `_bridgeClient.Start()`

3. Test:
   - Launch and watch for "Connecting" status
   - Connect to running Sheltered game with Cortex
   - Verify status changes to "Connected" when game connects
   - Verify workspace snapshot appears in explorer

**Success criteria:** Status changes from "Connecting" to connected state, explorer shows files

---

### Task 7: Performance & Polish
**Goal:** 60fps stable rendering with no stutters

**What to do:**
1. Monitor frame timing in `OnRenderFrame()`:
   - Verify `args.Time` delta is ~16.6ms at 60fps
   - Check no frame drops when scrolling or typing

2. Verify render pipeline efficiency:
   - ImGuiController.Update() and Render() are called in correct order
   - GL buffers are not recreated each frame
   - No excessive allocations in tight loops

3. Check visual polish:
   - Window has proper title bar
   - Panels have proper borders and padding
   - Font is readable (default ImGui font is fine for now)
   - Menu bar is visible with status
   - Docking regions are visually clear

4. Test: Launch and verify smooth 60fps rendering

**Success criteria:** Window renders at 60fps with no visual stutters or lag

---

### Task 8: Testing Checklist
**Goal:** Phase 1 meets all success criteria

**Test the following:**

| Feature | Test | Expected Result |
|---------|------|-----------------|
| Window opens | Launch exe | 1280x720 window appears |
| Bridge connects | Run Cortex in Sheltered | Status shows "Connected" |
| Explorer shows files | Files in workspace | File tree populated |
| File selection | Click file in explorer | File path highlighted, status updated |
| Code display | Open file preview | Code renders with colors |
| Search box | Type in search | Text appears in search input |
| Run search | Click Search button | Results appear below |
| Output panel | Watch logs | Connection and operation messages appear |
| Keyboard | Type in search | Characters appear |
| Docking | Drag window headers | Windows can be docked/undocked |
| Close | Click window X | Window closes, state persists |

---

## Known Limitations (Expected for Phase 1)

These are OK to leave incomplete:
- Keyboard shortcuts (Ctrl+S, F5) - Reporting as unsupported is fine
- Syntax highlighting edge cases (nested quotes, Unicode escapes)
- Code completion dropdown
- Find/replace
- Advanced settings
- Extensions panel

These should work:
- Basic file navigation
- Basic search
- Code display and scrolling
- Input dispatch to game
- State persistence

---

## Debugging Tips

If things don't work:

1. **"Bridge not connecting"**
   - Check pipe name is correct in options
   - Check Cortex.Host.Avalonia is not running (conflicts)
   - Check launch token is being passed correctly

2. **"No files in explorer"**
   - Check Sheltered workspace was imported
   - Check game sent workspace snapshot
   - Check output for errors

3. **"Keyboard not working"**
   - Check OpenTK keyboard state is available
   - Verify KeyboardState.IsKeyDown works in Update method
   - Check ImGui keyboard flags are set correctly

4. **"Rendering flickers/crashes"**
   - Check OpenGL context is created once at startup
   - Verify GL.Clear and buffer swaps happen each frame
   - Check shader compilation succeeded

5. **"Performance is slow"**
   - Check layout isn't being recomputed every frame
   - Verify no excessive string allocations
   - Check frame time in logs

---

## Success Definition

**Phase 1 is DONE when:**

✅ Compiles without errors
✅ Launches a window
✅ Connects to game via bridge
✅ Displays workspace files
✅ Renders code with colors
✅ Responds to keyboard/mouse input
✅ Shows status messages
✅ Persists state on close
✅ Runs at 60fps smooth
✅ Professional appearance

**You don't need:**
- Perfect syntax highlighting
- Full keyboard shortcut support
- Settings UI
- Advanced features

Just a solid foundation that proves the architecture works.

---

## Delivery

When Phase 1 is complete:
1. Create a commit: "Cortex.Host.ImGui: Phase 1 foundation complete"
2. Include message: "Functional desktop host with ImGui, bridge integration, and basic panels"
3. Note any known issues or TODOs for Phase 2 in commit message

Then report back with:
- Screenshot of running window
- Description of what's working
- Any issues encountered and how they were fixed
- Estimated time for Phase 2 based on what you learned
