# uasset-ai-bridge Architecture

## Core Idea

Unreal Engine assets are binary-serialized objects. This project introduces a **semantic bridge** that converts `.uasset` files into an AI-friendly JSON representation, and writes modifications back.

---

## Pipeline (Read)

```
.uasset / .uexp (binary)
    ↓
UAssetAPI — parses NameMap, ImportMap, ExportMap, properties
    ↓
Semantic Extractor — interprets raw exports into typed models
    ↓
AI JSON Schema — stable, human-readable output for AI agents
```

### UE Asset File Format Note

UE4 assets are typically split across two files:

- `.uasset` — header, NameMap, ImportMap, ExportMap
- `.uexp` — export payload (bulk data)

UAssetAPI handles both automatically when given the `.uasset` path. Always pass the `.uasset` path; the `.uexp` is resolved implicitly.

---

## Pipeline (Write)

```
Modified AI JSON
    ↓
Schema Validator — reject malformed input before touching disk
    ↓
Semantic Model — reconstruct internal representation
    ↓
UAssetAPI Writer — patch export properties
    ↓
Write to new file (original untouched unless --overwrite)
    ↓
Post-write Validation — reload and verify round-trip integrity
```

If post-write validation fails, the output file is deleted and an error is returned.

---

## Engine Version

Target version: **UE 4.27** (`EngineVersion.VER_UE4_27` in UAssetAPI).

Version is currently hardcoded. Future plan: accept `--engine-version` flag. Do not attempt auto-detection — it is unreliable without a full project context.

---

## Source Modules

```
src/UAssetAiBridge/
 ├── CLI/          — command dispatch, arg parsing
 ├── Extractors/   — one extractor per asset type
 ├── Models/       — internal semantic types (independent of UAssetAPI)
 ├── Schema/       — serialize/deserialize Models ↔ AI JSON
 └── Writer/       — apply AI JSON patches back to UAsset exports
```

### Data Flow Between Modules

```
CLI
 └─► Extractor(UAsset) → Model
          └─► Schema.Serialize(Model) → JSON string   [read path]

CLI
 └─► Schema.Deserialize(JSON) → Patch
          └─► Writer(Patch, UAsset) → UAsset          [write path]
```

### Extractor Interface

Each extractor receives a parsed `UAsset` and returns a `SemanticAsset` model.

```csharp
interface IAssetExtractor
{
    bool CanHandle(UAsset asset);
    SemanticAsset Extract(UAsset asset);
}
```

Extractors are selected by inspecting the asset's primary export class name (from the NameMap).

Priority order: `WidgetBlueprintExtractor` → `BlueprintExtractor` → `DataTableExtractor` → `DataAssetExtractor` → `FallbackExtractor`.

`FallbackExtractor` returns a minimal schema with `"assetType": "Unknown"` and raw export count. It never throws.

---

## Output Contract

All stdout is valid JSON. This applies to errors too — consumers are AI agents that parse output programmatically.

stderr is reserved for debug/trace output only (not part of the AI contract).

---

## Future Extensions

Additional asset types planned after Phase 1–2 are complete:

- Material graphs
- Animation blueprints
- Niagara systems
- Level assets (`.umap`)
