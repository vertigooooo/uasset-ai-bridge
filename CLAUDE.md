# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project

`uasset-ai-bridge` is a CLI tool for AI agents to read, modify, and create Unreal Engine `.uasset` files. It bridges binary UE assets to a structured AI-friendly JSON schema.

## Build & Run

```bash
# Build
dotnet build src/UAssetAiBridge/UAssetAiBridge.csproj

# Run
dotnet run --project src/UAssetAiBridge -- <command> <args>

# Example
dotnet run --project src/UAssetAiBridge -- inspect MyAsset.uasset
```

`lib/UAssetAPI` is a git submodule. Run `git submodule update --init` if it's empty.

## Architecture

Four-layer pipeline:

```
.uasset (binary)
    ↓
UAssetAPI (lib/UAssetAPI) — parses NameMap / ImportMap / ExportMap / properties
    ↓
Semantic Extractor — converts raw UE exports into logical models (planned modules below)
    ↓
AI JSON Schema (docs/ai_schema.md) — the only layer AI interacts with
```

Planned source modules under `src/UAssetAiBridge/`:
- `CLI/` — command dispatch
- `Extractors/` — per-asset-type extractors (`WidgetBlueprintExtractor`, `BlueprintExtractor`, etc.)
- `Models/` — internal semantic types (`WidgetNode`, `BlueprintFunction`, etc.)
- `Schema/` — serializes models to the AI JSON schema
- `Writer/` — converts modified JSON back to UE objects via UAssetAPI

Currently, only `Program.cs` exists — a single-file prototype that loads a `.uasset` and outputs export count.

## Current Implementation State

`Program.cs` is a prototype. It:
- Takes a single positional arg (file path)
- Hardcodes `EngineVersion.VER_UE5_1`
- Outputs `{ file, summary.export_count }` as JSON
- Outputs all errors as structured JSON (this contract must be preserved)

The full CLI spec (inspect, dump-json, modify-property, create-asset) is **not yet implemented**.

## AI JSON Contract

All stdout must be valid JSON — including errors. Consumers are AI agents that parse output programmatically. Never print plain text.

Error envelope:
```json
{ "error": "error_code", "message": "..." }
```

Asset output root structure (see `docs/ai_schema.md` for full spec):
```json
{ "assetType": "...", "name": "...", "engineVersion": "...", "content": {} }
```

## CLI Spec Summary

| Command | Args | Status |
|---|---|---|
| `inspect` | `<file.uasset>` | Partial (export count only) |
| `dump-json` | `<file.uasset>` | Not implemented |
| `modify-property` | `<file.uasset> <path> <value>` | Not implemented |
| `create-asset` | `<type> <outputPath>` | Not implemented |

## Safety Rules

- Never overwrite the source asset by default
- Write to a new file unless `--overwrite` is explicitly passed
- Validate asset after modification before saving

## Asset Type Priority

1. WidgetBlueprint (UI focus first)
2. Blueprint
3. DataAsset
4. DataTable
5. Material
6. Texture
