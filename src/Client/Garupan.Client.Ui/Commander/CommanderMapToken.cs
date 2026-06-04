using Opus.Engine.Ui;

namespace Garupan.Client.Ui.Commander;

/// <summary>
/// A 2D tank token placed by the commander on the map — a position, an ink colour (school
/// / known-vs-suspected coding), and an auto-numbered label. The commander drops these
/// onto the designer-drawn battlefield map as scout reports come in: a friendly unit at
/// a known position, an enemy seen at a grid square, a probable observation post.
///
/// Tokens are immutable like every other <see cref="CommanderMapMark"/> — to move a token
/// the commander undoes and re-places. A drag-edit affordance is post-alpha polish.
/// </summary>
/// <param name="Position">Screen-pixel position of the token's centre.</param>
/// <param name="InkColor">Primary ink for own units, accent ink for hostile / suspected.</param>
/// <param name="Label">Short identifier — auto-assigned "1", "2", … in placement order.</param>
public sealed record CommanderMapToken(
    CommanderMapPoint Position,
    Color InkColor,
    string Label) : CommanderMapMark;
