namespace Garupan.Client.Ui.Screens.Settings.Multiplayer;

/// <summary>Pure layout maths for <see cref="MultiplayerSettingsScreen"/> — three
/// stacked sections (default + Hungry Battles override + Tactical 5v5 override), each
/// with a header row and two field rows. Shared by the renderer and the mouse hit-test
/// so the drawn rows and click targets stay in lockstep.</summary>
public sealed class MultiplayerSettingsLayout
{
    public const int SectionHeaderHeight = 28;
    public const int SectionSpacing = 16;

    private readonly int[] _rowTop = new int[MultiplayerSettingsModel.FieldCount];
    private readonly int[] _sectionHeaderTop = new int[MultiplayerSettingsModel.SectionCount];

    public MultiplayerSettingsLayout()
    {
        var y = SettingsPalette.FirstRowY;
        for (var section = 0; section < MultiplayerSettingsModel.SectionCount; section++)
        {
            _sectionHeaderTop[section] = y;
            y += SectionHeaderHeight;

            for (var slot = 0; slot < MultiplayerSettingsModel.FieldsPerSection; slot++)
            {
                var fieldIndex = (section * MultiplayerSettingsModel.FieldsPerSection) + slot;
                _rowTop[fieldIndex] = y;
                y += SettingsPalette.RowHeight;
            }

            if (section < MultiplayerSettingsModel.SectionCount - 1)
            {
                y += SectionSpacing;
            }
        }

        ContentBottom = y;
    }

    /// <summary>Top pixel of the row band for the field at <paramref name="index"/>.</summary>
    public int RowTop(int index) => _rowTop[index];

    /// <summary>Top pixel of the section header row above the section at
    /// <paramref name="section"/> (0 = default, 1 = Hungry, 2 = Tactical).</summary>
    public int SectionHeaderTop(int section) => _sectionHeaderTop[section];

    /// <summary>Lowest pixel the field stack reaches — the renderer parks the validity
    /// pill and hint line below this.</summary>
    public int ContentBottom { get; }

    /// <summary>Field index under <paramref name="mouseY"/>, or -1 if off every row.</summary>
    public int FieldOf(int mouseY)
    {
        for (var i = 0; i < _rowTop.Length; i++)
        {
            if (mouseY >= _rowTop[i] && mouseY < _rowTop[i] + SettingsPalette.RowHeight)
            {
                return i;
            }
        }

        return -1;
    }
}
