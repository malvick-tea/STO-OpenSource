namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// Pure layout maths for <see cref="ControlsScreen"/> — uniform action rows stacked from
/// <see cref="SettingsPalette.FirstRowY"/>. Shared by the renderer and the screen's
/// hit-test so the drawn rows and the click targets never drift apart.
/// </summary>
public sealed class ControlsLayout
{
    private readonly int[] _rowTop;

    public ControlsLayout(int rowCount)
    {
        _rowTop = new int[rowCount];
        for (var i = 0; i < rowCount; i++)
        {
            _rowTop[i] = SettingsPalette.FirstRowY + (i * SettingsPalette.RowHeight);
        }
    }

    public int RowCount => _rowTop.Length;

    /// <summary>Top pixel of the row band for the action at <paramref name="index"/>.</summary>
    public int RowTop(int index) => _rowTop[index];

    /// <summary>Row index under <paramref name="mouseY"/>, or -1 if the cursor is off every row.</summary>
    public int RowOf(int mouseY)
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
