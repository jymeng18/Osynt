# UI/UX checkup — October 29, 2026

Reviewed the request workspace at 1280×840 and 980×640, collection search,
request tabs, body/auth controls, response inspection, cURL import, and garden controls.
The garden scene and atmosphere were kept intact.

## Addressed

| Finding | Change |
| --- | --- |
| Small, faint labels made controls and response metadata hard to scan. | Increased small workspace labels to 11 px, strengthened secondary text and syntax colors, and restored full-size checkboxes. |
| Request actions competed with the title; two different actions were called “Save.” | Grouped cURL actions in a menu and used “Save request” and “Save file.” |
| Timeout steppers consumed most of the input width. | Used a compact numeric field with a persistent seconds label; typing and arrow-key adjustment remain available. |
| Selecting an overflowing tab could leave it offscreen. | Reveal the selected tab and its close button; support wheel scrolling and a horizontal scrollbar. |
| Adding fields required finding and clicking the newly created row. | Focus and scroll to the new parameter, header, or form-field key. |
| Clearing library search required selecting and deleting the text. | Added a clear button that retains search focus. |
| The request/response divider could collapse usable content. | Added minimum pane heights, a larger drag target, and a resize tooltip. |
| Long folder names and empty-response guidance could overflow. | Added truncation/tooltips for names and wrapping for response guidance. |
| Milestones at compact sizes hid later unlocks below the visible panel. | Added a clear dialog title, a separately scrolling list, visible progress text, and a fixed heading/close button and explanatory footer. Escape restores focus to Milestones. |
| Reset camera kept drifting after an orbit. | Clear orbit momentum before restoring the starting view. A regression check reproduced this before the fix. |
| Garden shortcuts could send a request while a modal was open. | Keep dialog keyboard interaction local. Escape closes plant details before leaving the garden. |

The reported Milestones click doing nothing was **not reproduced** in the tested build.
The native Windows WebView2 probe verified that the button is unobstructed and that
activating it opens a visible dialog. This confirms the updated build's behavior,
not the cause of the user's original symptom.

## Remaining priorities

1. **Make request organization discoverable.** Rename, duplicate, move, and delete
   live in context menus. A visible actions menu would make them easier to find.
2. **Remember editor context per request.** Params/Headers/Body/Auth and the response
   pane are shared workspace state; switching tabs can leave the user in the previous
   request's pane. Preserve these choices per tab, alongside scroll position.
3. **Improve save and validation feedback.** Persistence errors and JSON formatting
   errors use the bottom status bar. Show errors beside the action/input, with a
   clear retry path, and make the selected save destination visible.
4. **Support broader display/accessibility needs.** The current supported minimum is
   980×640; smaller available work areas, adjustable text size, and a systematic
   contrast/focus audit still need dedicated work. This pass is not an accessibility
   conformance assessment.
5. **Preserve work between sessions.** Restore open tabs and unsaved drafts, as already
   tracked in the production roadmap.

## Validation

- 138 .NET application, integration, and headless desktop checks pass.
- Garden progression and expanded Playwright checks pass, including zoom/reset,
  compact Milestones scrolling, close/Escape focus, and desktop message contracts.
- The actual Windows WebView2/WebGL probe opens and captures the Milestones dialog.
- Desktop previews: `artifacts/ui-review/after.png` and
  `artifacts/ui-review/osynt-compact-preview.png`.
- Native dialog preview: `artifacts/ui-review/windows-garden.milestones.png`.
