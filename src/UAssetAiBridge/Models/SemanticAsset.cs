namespace UAssetAiBridge.Models;

record SemanticAsset(string AssetType, string Name, string EngineVersion, object Content);

record WidgetNode(
    string Type,
    string Name,
    Dictionary<string, object?> Properties,
    List<WidgetNode> Children);
