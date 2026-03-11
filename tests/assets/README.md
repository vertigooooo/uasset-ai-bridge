# Test Assets

Real UE 4.27 `.uasset` files for manual testing and extractor validation.

Each asset typically comes as a pair: `.uasset` + `.uexp`. Copy both.

---

## `widget_blueprint/`

**Priority: highest.** WidgetBlueprint extraction is the most complex.

Need samples that cover:
- A simple widget with only a CanvasPanel + 1–2 children (baseline)
- A widget with nested hierarchy (Button containing TextBlock)
- A widget with a ScrollBox or HorizontalBox (panel with multiple children via Slots)

Ideal source: any `WBP_*.uasset` from your project's UI folder.

---

## `blueprint/`

A regular Actor or Object Blueprint. Used to test variable + function signature extraction.

Need:
- A Blueprint with at least 1 variable and 1 custom function
- Parent class doesn't matter (Character, Actor, Object all fine)

---

## `data_asset/`

> TODO: No sample available yet. Add a `DA_*.uasset` + `.uexp` when available.

---

## `data_table/`

> TODO: No sample available yet. Add a `DT_*.uasset` + `.uexp` when available.

---

## Notes

- Keep files small (simple assets only, not large maps or complex BPs)
- Do not commit assets that contain proprietary game content
- `.uasset` files are binary — do not open or edit them manually
