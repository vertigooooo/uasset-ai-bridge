# CLI Reference

Complete command reference for `uasset-ai-bridge`. Target: UE 4.27.

All output is JSON on stdout. All errors are JSON. See `ai_schema.md` for error envelope format.

---

## Global Flags

| Flag | Description |
|---|---|
| `--engine-version <ver>` | Override engine version string (default: `4.27`) |
| `--overwrite` | Allow writing to the source file (write commands only) |
| `--pretty` | Pretty-print JSON output (default: minified) |

---

## Commands

### `inspect <file.uasset>`

Output a summary of the asset. Does not extract full content.

```
uasset-ai-bridge inspect WBP_Menu.uasset
```

Output:

```json
{
  "assetType": "WidgetBlueprint",
  "name": "WBP_Menu",
  "engineVersion": "4.27",
  "exportCount": 42,
  "importCount": 7,
  "sizeBytes": 18432
}
```

Exit codes: `0` success, `3` file not found, `4` parse failed.

---

### `dump-json <file.uasset>`

Extract the full semantic content of the asset as AI JSON.

```
uasset-ai-bridge dump-json WBP_Menu.uasset
```

Output follows the root structure defined in `ai_schema.md`.

If the asset type has no extractor, returns `"assetType": "Unknown"` with `exportCount`. Does not error.

Exit codes: `0` success, `3` file not found, `4` parse failed.

---

### `modify-property <file.uasset> <patch.json> [--output <out.uasset>] [--overwrite]`

Apply a single patch operation to the asset.

```
uasset-ai-bridge modify-property WBP_Menu.uasset patch.json --output WBP_Menu_patched.uasset
```

`patch.json` contains a single patch object as defined in `ai_schema.md` (Patch Schema section).

If `--output` is omitted and `--overwrite` is not set, defaults to writing `<name>_patched.uasset` in the same directory as the source.

Output on success:

```json
{
  "status": "ok",
  "output": "WBP_Menu_patched.uasset"
}
```

Exit codes: `0` success, `3` file not found, `4` parse failed, `5` patch invalid, `6` validation failed.

---

### `create-asset <type> <output.uasset>`

Create a new asset from a JSON template read from stdin.

```
uasset-ai-bridge create-asset WidgetBlueprint WBP_New.uasset < template.json
```

The stdin JSON follows the asset creation schema in `ai_schema.md`.

Output on success:

```json
{
  "status": "ok",
  "output": "WBP_New.uasset"
}
```

Exit codes: `0` success, `5` invalid input JSON, `7` asset type not supported for creation.

---

## Exit Codes

| Code | Meaning |
|---|---|
| `0` | Success |
| `2` | Missing required argument |
| `3` | File not found |
| `4` | Parse failed (UAssetAPI error) |
| `5` | Patch or input JSON invalid |
| `6` | Post-write validation failed |
| `7` | Asset type not supported for this operation |
