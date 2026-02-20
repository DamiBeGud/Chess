using System;
using Avalonia.Media;

namespace Chess.UI.Assets;

internal interface IPieceAssetImageLoader
{
    IImage? Load(Uri assetUri);
}
