namespace Garupan.Client.Ui.Commander;

/// <summary>
/// One commit on the commander's map — either a hand-drawn stroke or a placed tank token.
/// Marks are immutable, ordered (insertion order), and the unit of
/// <see cref="CommanderMapState.Undo"/> — popping a mark restores the map to its prior
/// state regardless of which kind it was. The renderer iterates marks in order, so a
/// token placed after a stroke draws on top of it.
///
/// Closed hierarchy with two known subtypes (<see cref="CommanderMapStroke"/>,
/// <see cref="CommanderMapToken"/>). A future "arrow" tool would add a third subtype.
/// </summary>
public abstract record CommanderMapMark;
