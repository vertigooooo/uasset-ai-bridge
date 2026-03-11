# Roadmap

## Current State (Phase 1 — complete)

**Working:**
- `inspect` — asset type, export/import count, size (type detection via Import scan, no full parse)
- `dump-json` — WidgetBlueprint: full widget tree with position/size (CanvasPanelSlot), text, visibility
- `dump-json` — Blueprint: parentClass, variables (from CDO), custom events + functions
- FallbackExtractor — unknown asset types return exportCount

**Not yet implemented:**
- `modify-property` command
- `create-asset` command
- DataTable / DataAsset extractors

---

## Phase 1 — Read ✓

### 1a. WidgetBlueprint dump-json ✓

- [x] Widget tree via slot indirection (CanvasPanelSlot, OverlaySlot, WrapBoxSlot, etc.)
- [x] `text` from StrPropertyData / TextPropertyData (all HistoryTypes)
- [x] `visibility` from EnumPropertyData
- [x] `position` / `size` from CanvasPanelSlot Offsets (Left/Top/Right/Bottom)
- [x] Custom widget instances extracted as opaque nodes (type = generated class name)

### 1b. BlueprintExtractor ✓

- [x] Detect via Import `Blueprint` (className=Class)
- [x] `parentClass` from BP export's ParentClass property
- [x] Variables from CDO export (`Default__*`) with type + default value
- [x] Custom events from K2Node_CustomEvent (`CustomFunctionName`)
- [x] Functions from FunctionExport, filtering out `ExecuteUbergraph_*`, `BndEvt__*`, system functions

### 1c. inspect ✓

- [x] Type detection via `IAssetExtractor.AssetTypeName` — no full parse needed

---

## Phase 2 — Semantic Enrichment

- [ ] Widget property coverage: color (LinearColor struct), font, padding
- [ ] CanvasPanel slot layout: Anchors (min/max), Offsets (left/top/right/bottom), ZOrder
- [ ] DataTableExtractor — rows from DataTableExport
- [ ] DataAssetExtractor — flat property bag from primary NormalExport
- [ ] Named widget deduplication (WBP has 2 copies of everything — confirm first WidgetTree is always correct)

---

## Phase 3 — Write ✓ (partial)

- [x] `modify-property` command
- [x] Patch schema deserializer (`operation`, `path`, `value`)
- [x] Path resolver: dot-split + `children[name=X]` / `children[0]` selectors
- [x] Widget tree traversal via slot indirection (same logic as extractor)
- [x] `text` mutation (TextPropertyData → RawText, StrPropertyData)
- [x] `visibility` mutation (EnumPropertyData + NameMap update)
- [x] Post-write validation: reload asset, delete output on failure
- [x] `--output` / `--overwrite` flags; default writes `<name>_patched.uasset`

**Not yet implemented:**
- [ ] `position` / `size` mutation (requires finding parent slot, modifying Offsets struct)
- [ ] `add` / `remove` operations
- [ ] Non-WBP asset modification

---

## Phase 4 — Create

- [ ] `create-asset` command
- [ ] WidgetBlueprint creation from JSON template
- [ ] Requires understanding minimal valid export graph — needs research with a blank WBP asset

---

## Known Risks

| Risk | Notes |
|---|---|
| CanvasPanelSlot layout struct format | Offsets/Anchors are nested structs — need to verify field names from a real asset with positioned widgets |
| Blueprint variable extraction | CDO may not contain all variables if they have default values — needs verification with BP_Saiqi |
| Two WidgetTree copies | First WidgetTree assumed correct — verify this holds across more assets |
| Asset write round-trip | UAssetAPI write correctness depends on preserving unknown fields — test before Phase 3 |
