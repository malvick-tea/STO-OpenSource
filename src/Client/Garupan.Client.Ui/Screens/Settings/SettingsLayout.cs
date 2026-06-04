using System.Collections.Generic;

namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>
/// Pure layout maths for the settings screen: the vertical row positions (with extra gaps
/// before each section header) and the horizontal arrow hit-zones. Both the renderer and
/// the screen's hit-test consume the same routines, so on-screen geometry and click
/// targets never drift apart.
/// </summary>
public sealed class SettingsLayout
{
    private readonly int[] _rowTop;

    public SettingsLayout(IReadOnlyList<SettingsOption> options)
    {
        _rowTop = new int[options.Count];
        var y = SettingsPalette.FirstRowY;
        for (var i = 0; i < options.Count; i++)
        {
            // Sections after the first get breathing room above their header.
            if (i > 0 && options[i].SectionHeader.HasValue)
            {
                y += SettingsPalette.SectionGap;
            }

            _rowTop[i] = y;
            y += SettingsPalette.RowHeight;
        }
    }

    public int RowCount => _rowTop.Length;

    /// <summary>Top pixel of the row band for the option at <paramref name="index"/>.</summary>
    public int RowTop(int index) => _rowTop[index];

    /// <summary>Y at which to draw the section header that sits above row <paramref name="index"/>.</summary>
    public int SectionHeaderY(int index) => _rowTop[index] - 26;

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

    public int LeftArrowX(int surfaceWidth) =>
        surfaceWidth - SettingsPalette.MarginX - SettingsPalette.ValueAreaWidth;

    public int RightArrowX(int surfaceWidth) =>
        surfaceWidth - SettingsPalette.MarginX - SettingsPalette.ArrowWidth;

    /// <summary>Horizontal centre of the value text — midway between the two arrows.</summary>
    public int ValueCenterX(int surfaceWidth) =>
        (LeftArrowX(surfaceWidth) + SettingsPalette.ArrowWidth + RightArrowX(surfaceWidth)) / 2;

    /// <summary>-1 if <paramref name="mouseX"/> is over the decrement arrow, +1 over the
    /// increment arrow, 0 otherwise. Row containment is the caller's job (via <see cref="RowOf"/>).</summary>
    public int ArrowDirectionAt(int mouseX, int surfaceWidth)
    {
        var leftX = LeftArrowX(surfaceWidth);
        if (mouseX >= leftX && mouseX < leftX + SettingsPalette.ArrowWidth)
        {
            return -1;
        }

        var rightX = RightArrowX(surfaceWidth);
        if (mouseX >= rightX && mouseX < rightX + SettingsPalette.ArrowWidth)
        {
            return 1;
        }

        return 0;
    }
}
