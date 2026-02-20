using Chess.Domain;

namespace Chess.UI.Assets;

/// <summary>
/// IPieceAssetResolver defines a contract within the UI/Assets module.
/// It declares member signatures that decouple callers from implementation details.
/// Primary production consumers include MainWindowViewModel (UI/ViewModels), MainWindowTextFormatter (UI/Services), BoardSquareViewModel (UI/ViewModels).
/// Key collaborators are Implementations include PieceAssetResolver (UI/Assets).
/// </summary>
/// <remarks>
/// <para><b>Used by:</b> MainWindowViewModel (UI/ViewModels), MainWindowTextFormatter (UI/Services), BoardSquareViewModel (UI/ViewModels), PieceAssetResolver (UI/Assets)</para>
/// <para><b>Usage pattern:</b> Presentation components call this type while resolving, parsing, caching, and rendering piece-related visual assets.</para>
/// <para><b>Dependencies/Collaborators:</b> Implementations include PieceAssetResolver (UI/Assets).</para>
/// <para><b>Boundary:</b> This type sits in the UI/Assets UI boundary and supports presentation, interaction, or view-facing coordination.</para>
/// </remarks>
public interface IPieceAssetResolver
{
    ResolvedPieceAsset? Resolve(Piece piece);
}
