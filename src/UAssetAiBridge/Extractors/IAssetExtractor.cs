using UAssetAPI;
using UAssetAiBridge.Models;

namespace UAssetAiBridge.Extractors;

interface IAssetExtractor
{
    bool CanHandle(UAsset asset);
    string AssetTypeName { get; }
    SemanticAsset Extract(UAsset asset);
}
