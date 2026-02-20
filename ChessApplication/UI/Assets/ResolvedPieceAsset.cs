using Avalonia.Media;

namespace Chess.UI.Assets;

/// <summary>
/// ResolvedPieceAsset is a record type within the UI/Assets module.
/// It models structured immutable data that is passed across service and boundary interactions.
/// Primary production consumers include MoveHistoryEntryViewModel (UI/ViewModels), PieceAssetResolver (UI/Assets), PieceAssetCache (UI/Assets).
/// This data contract is exchanged by the services and models listed in the Used by section.
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MoveHistoryEntryViewModel (UI/ViewModels), PieceAssetResolver (UI/Assets), PieceAssetCache (UI/Assets), IPieceAssetCache (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> This data contract is exchanged by the services and models listed in the Used by section.</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
public sealed record ResolvedPieceAsset(IImage Image, string AssetUri, bool UsedFallback);
