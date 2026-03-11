# UAssetAPI Notes

Practical notes on UAssetAPI usage within this project. UE 4.27 target.

Source: `lib/UAssetAPI` (git submodule). Namespace: `UAssetAPI`, `UAssetAPI.UnrealTypes`, `UAssetAPI.ExportTypes`, `UAssetAPI.PropertyTypes.Objects`.

---

## Loading an Asset

```csharp
UAsset asset = new UAsset("path/to/file.uasset", EngineVersion.VER_UE4_27);
```

UAssetAPI automatically loads the paired `.uexp` file if present (same directory, same base name). Always pass the `.uasset` path.

Throws on parse failure — wrap in try/catch.

---

## Key Classes

### `UAsset`

Top-level container.

| Member | Type | Description |
|---|---|---|
| `Exports` | `List<Export>` | All exports defined in this asset |
| `Imports` | `List<Import>` | External references (engine classes, other assets) |
| `GetNameMapIndexList()` | method | Returns all FName strings in the NameMap |

### `Export` (base class)

Every export has:

| Member | Type | Description |
|---|---|---|
| `ObjectName` | `FName` | The export's own name |
| `ClassIndex` | `FPackageIndex` | Points to its class (in Imports or Exports) |
| `OuterIndex` | `FPackageIndex` | Parent object (for nested objects) |

### `NormalExport` (most common subclass)

```csharp
if (export is NormalExport ne)
{
    foreach (PropertyData prop in ne.Data)
    {
        // prop.Name.Value.Value is the property name string
    }
}
```

Other export types: `FunctionExport`, `ClassExport`, `StructExport`. These are rare in data assets.

### `FPackageIndex`

References another object — either an Import (negative index) or Export (positive index).

```csharp
// Resolve to a name string
string className = asset.GetClassExportName(export.ClassIndex);
// Or manually:
if (index.IsImport()) asset.Imports[-index.Index - 1].ObjectName.Value.Value
if (index.IsExport()) asset.Exports[index.Index - 1].ObjectName.Value.Value
```

### `FName`

```csharp
string name = fname.Value.Value; // the plain string
```

---

## Identifying Asset Type

There is no single "asset type" field. Infer from the primary export's class name:

```csharp
string GetAssetTypeName(UAsset asset)
{
    if (asset.Exports.Count == 0) return "Unknown";
    var primary = asset.Exports[0];
    // ClassIndex resolves to an Import for engine classes
    if (primary.ClassIndex.IsImport())
    {
        var imp = asset.Imports[-primary.ClassIndex.Index - 1];
        return imp.ObjectName.Value.Value; // e.g. "WidgetBlueprint"
    }
    return "Unknown";
}
```

Common class names seen in Imports for UE4.27:

| Class name in NameMap | Asset type |
|---|---|
| `WidgetBlueprint` | Widget Blueprint |
| `Blueprint` | Blueprint |
| `DataTable` | Data Table |
| `DataAsset` (or subclass name) | Data Asset |
| `Material` | Material |
| `Texture2D` | Texture |

---

## Property Data Types

Properties are stored as `PropertyData` subclasses. Pattern for reading:

```csharp
foreach (PropertyData prop in ne.Data)
{
    string name = prop.Name.Value.Value;
    switch (prop)
    {
        case StrPropertyData s:    string val = s.Value; break;
        case TextPropertyData t:   string val = t.Value?.Value; break;
        case FloatPropertyData f:  float val = f.Value; break;
        case IntPropertyData i:    int val = i.Value; break;
        case BoolPropertyData b:   bool val = b.Value; break;
        case ObjectPropertyData o: // references another export/import
            break;
        case ArrayPropertyData a:
            foreach (var item in a.Value) { /* each item is PropertyData */ }
            break;
        case StructPropertyData st:
            foreach (var field in st.Value) { /* each field is PropertyData */ }
            break;
        case EnumPropertyData e:   string val = e.Value.Value.Value; break;
    }
}
```

For text displayed to users (widget labels, etc.), prefer `TextPropertyData` over `StrPropertyData`. Both may appear.

---

## WidgetBlueprint: Asset Structure (verified on UE4.27)

A WidgetBlueprint `.uasset` contains **two full copies** of every widget export:
- Set A: owned by the `WidgetBlueprint` object (editor/CDO)
- Set B: owned by the `WidgetBlueprintGeneratedClass` object (runtime CDO)

Both sets have their own `WidgetTree` export. They will appear consecutively near the end of the export list, e.g. `WidgetTree` at outer=N and `WidgetTree` at outer=N+1.

**Always use the first `WidgetTree` (lower export index).** It corresponds to the designer-visible asset.

```csharp
// The first WidgetTree export is always the right one
var widgetTree = asset.Exports
    .First(e => e.ObjectName.Value.Value == "WidgetTree");

int widgetTreeIdx = asset.Exports.IndexOf(widgetTree) + 1; // 1-based index
```

### Widget Ownership via OuterIndex

All widget exports owned by a WidgetTree have `OuterIndex` pointing to that WidgetTree's 1-based export index.

```csharp
// Direct children of this WidgetTree
var ownedWidgets = asset.Exports
    .Where(e => e.OuterIndex.Index == widgetTreeIdx)
    .OfType<NormalExport>();
```

The root widget is referenced by `RootWidget` property on the WidgetTree export (`ObjectPropertyData`).

### Widget Hierarchy: Slot Indirection

Widget parent/child relationships go through a slot export, not direct `OuterIndex` chains. Each panel widget owns slot exports (outer = panel's 1-based index), and each slot has a `Content` property pointing to the child widget.

```
CanvasPanel (export N)
  └── CanvasPanelSlot (outer=N, export M)   ← slot export
        └── Content: ObjectPropertyData → child widget export
```

Slot class names observed in real UE4.27 assets:

| Panel widget | Slot class |
|---|---|
| `CanvasPanel` | `CanvasPanelSlot` |
| `Overlay` | `OverlaySlot` |
| `SizeBox` | `SizeBoxSlot` |
| `WrapBox` | `WrapBoxSlot` |
| `WidgetSwitcher` | `WidgetSwitcherSlot` |
| `HorizontalBox` | `HorizontalBoxSlot` |
| `VerticalBox` | `VerticalBoxSlot` |

Widgets with a single child (e.g. `Button`) use a `Content` property directly, no slot export.

### Root Widget Discovery

```csharp
var rootProp = (widgetTree as NormalExport)?.Data
    .OfType<ObjectPropertyData>()
    .FirstOrDefault(p => p.Name.Value.Value == "RootWidget");

// rootProp.Value is an FPackageIndex pointing to the root widget export
```

---

## Writing Assets

```csharp
asset.Write("output.uasset");
```

This writes both `.uasset` and `.uexp` if the original had a `.uexp`.

To modify a property before writing:

```csharp
var export = asset.Exports[idx] as NormalExport;
var prop = export.Data.FirstOrDefault(p => p.Name.Value.Value == "Text");
if (prop is TextPropertyData t) t.Value = new FString("New Text");
asset.Write("output.uasset");
```

Do not construct new `PropertyData` objects from scratch unless necessary — mutate existing ones to preserve unknown fields.

---

## Common Pitfalls

- **FName interning**: Two FNames with the same string are equal by value but may be different objects. Always compare with `.Value.Value` string comparison.
- **1-based vs 0-based indices**: `FPackageIndex` uses 1-based indices for exports. `asset.Exports[index.Index - 1]` to dereference.
- **Missing .uexp**: If only the `.uasset` is present (no `.uexp`), UAssetAPI may still parse successfully for header-only inspection, but write will be incomplete.
- **Engine version mismatch**: Wrong `EngineVersion` causes silent parse errors or garbage data. Always use `VER_UE4_27` for this project.
