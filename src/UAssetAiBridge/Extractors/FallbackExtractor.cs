using UAssetAPI;
using UAssetAiBridge.Models;

namespace UAssetAiBridge.Extractors;

class FallbackExtractor : IAssetExtractor
{
    public string AssetTypeName => "Unknown";

    public bool CanHandle(UAsset asset) => true;

    public SemanticAsset Extract(UAsset asset) => new(
        AssetType: "Unknown",
        Name: Path.GetFileNameWithoutExtension(asset.FilePath),
        EngineVersion: "4.27",
        Content: new { exportCount = asset.Exports.Count }
    );
}
