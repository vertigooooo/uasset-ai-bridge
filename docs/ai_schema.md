# AI Asset JSON Schema v1

Schema specification for `uasset-ai-bridge`. Target engine: **UE 4.27**.

---

## Output Contract

All tool output (including errors) is JSON on stdout. Never plain text.

### Error Envelope

```json
{
  "error": "error_code",
  "message": "human readable description"
}
```

| error_code | Meaning |
|---|---|
| `no_path_provided` | Missing required argument |
| `file_not_found` | Path does not exist |
| `parse_failed` | UAssetAPI failed to parse the file |
| `unsupported_asset_type` | Asset type exists but extractor not implemented |
| `validation_failed` | Post-write round-trip check failed |
| `patch_invalid` | Patch JSON is malformed or path is invalid |

---

## Root Structure

Every asset output follows this envelope:

```json
{
  "assetType": "string",
  "name": "string",
  "engineVersion": "4.27",
  "content": {}
}
```

`engineVersion` is always a bare version string: `"4.27"`, `"5.1"`, etc.

### Unknown Asset Types

If no extractor supports the asset, return:

```json
{
  "assetType": "Unknown",
  "name": "MyAsset",
  "engineVersion": "4.27",
  "content": {
    "exportCount": 12
  }
}
```

No error is raised. The caller can decide whether to proceed.

---

## Supported Asset Types

| Asset Type | Priority | Status |
|---|---|---|
| WidgetBlueprint | High | Planned Phase 1 |
| Blueprint | High | Planned Phase 2 |
| DataAsset | Medium | Planned Phase 2 |
| DataTable | Medium | Planned Phase 2 |
| Material | Low | Future |
| Texture | Low | Future |

---

## WidgetBlueprint Schema

```json
{
  "assetType": "WidgetBlueprint",
  "name": "WBP_Menu",
  "engineVersion": "4.27",
  "content": {
    "root": {
      "type": "CanvasPanel",
      "name": "CanvasPanel_0",
      "properties": {},
      "children": [
        {
          "type": "Button",
          "name": "StartButton",
          "properties": {
            "position": [200, 100],
            "size": [300, 80],
            "visibility": "Visible"
          },
          "children": [
            {
              "type": "TextBlock",
              "name": "StartButton_Text",
              "properties": {
                "text": "Start Game",
                "color": [1.0, 1.0, 1.0, 1.0]
              },
              "children": []
            }
          ]
        }
      ]
    }
  }
}
```

### Widget Node Fields

| Field | Type | Description |
|---|---|---|
| `type` | string | UE widget class name (e.g. `Button`, `TextBlock`) |
| `name` | string | Widget slot name from the WidgetTree |
| `properties` | object | Supported properties (see below) |
| `children` | array | Child widget nodes |

`children` is always present, empty array if no children.

### Supported Widget Properties

| Property | Type | Notes |
|---|---|---|
| `text` | string | Displayed text content |
| `color` | `[r, g, b, a]` | Float values 0.0–1.0 |
| `position` | `[x, y]` | Canvas slot position in pixels |
| `size` | `[w, h]` | Canvas slot size in pixels |
| `visibility` | string | `Visible`, `Hidden`, `Collapsed` |

**Unknown properties:** Any widget property not in the table above is silently omitted from output. On write, unknown properties in patch input are rejected with `patch_invalid`.

---

## Blueprint Schema

```json
{
  "assetType": "Blueprint",
  "name": "BP_Player",
  "engineVersion": "4.27",
  "content": {
    "parentClass": "Character",
    "variables": [
      {
        "name": "Health",
        "type": "float",
        "default": 100.0
      }
    ],
    "functions": [
      {
        "name": "TakeDamage",
        "inputs": [{ "name": "Amount", "type": "float" }],
        "outputs": []
      }
    ]
  }
}
```

Blueprint graph logic (nodes, pins, execution flow) is **not** represented in v1. Only variables and function signatures are extracted.

---

## DataAsset Schema

```json
{
  "assetType": "DataAsset",
  "name": "ItemConfig",
  "engineVersion": "4.27",
  "content": {
    "fields": {
      "ItemName": "Sword",
      "Damage": 25,
      "Weight": 2.5
    }
  }
}
```

---

## DataTable Schema

```json
{
  "assetType": "DataTable",
  "name": "ItemTable",
  "engineVersion": "4.27",
  "content": {
    "rows": [
      { "Id": "Sword", "Damage": 20 },
      { "Id": "Bow", "Damage": 15 }
    ]
  }
}
```

---

## Patch Schema (`modify-property`)

Patches are applied via the `modify-property` command. Each patch is a single operation.

```json
{
  "operation": "set" | "add" | "remove",
  "path": "content.root.children[name=StartButton].properties.text",
  "value": "Start Game"
}
```

### Path Syntax

Paths use dot notation. Array elements can be addressed two ways:

| Syntax | Meaning |
|---|---|
| `children[0]` | By zero-based index |
| `children[name=StartButton]` | By `name` field value |

`name=` lookup is preferred. Index access is fragile if the widget tree changes.

### Operations

| Operation | `value` required | Description |
|---|---|---|
| `set` | Yes | Set a property to a new value |
| `add` | Yes | Append a node to an array |
| `remove` | No | Remove a node or property |

**`add` always appends to the end of the array.** There is no insert-at-index in v1.

### Validation Rules

- Path must resolve to an existing node (except `add`, which resolves the parent)
- `value` type must match the target field's expected type
- Unknown property names in `properties` are rejected

---

## Asset Creation Schema (`create-asset`)

The input JSON for `create-asset` follows the same root structure as output, with `content` defining the initial state.

```json
{
  "assetType": "WidgetBlueprint",
  "name": "WBP_Settings",
  "content": {
    "root": {
      "type": "CanvasPanel",
      "name": "CanvasPanel_0",
      "properties": {},
      "children": []
    }
  }
}
```

`engineVersion` is ignored on input — always uses the tool's configured version (4.27).

---

## Schema Version

Version: 1.0 — UE 4.27 target
