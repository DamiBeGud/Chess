using Chess.Domain;

namespace Chess.UI.Assets;

/// <summary>
/// IPieceAssetCache defines a contract within the UI/Assets module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include PieceAssetResolver (UI/Assets), PieceAssetCache (UI/Assets).
/// Key collaborators are Implementations include PieceAssetCache (UI/Assets).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> PieceAssetResolver (UI/Assets), PieceAssetCache (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include PieceAssetCache (UI/Assets).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
internal interface IPieceAssetCache
{
    bool TryGet((PieceColor Color, PieceType Type) key, out ResolvedPieceAsset? asset);

    void Set((PieceColor Color, PieceType Type) key, ResolvedPieceAsset? asset);
}
