using System.Collections.Generic;
using System.IO;
using Opus.Engine.Pal.Filesystem;

namespace Garupan.Client.Windows.Bootstrap;

/// <summary>
/// Reads the bundled localization CSVs into a raw text corpus. Feeds the D3D12 client's
/// <c>D3D12FontAtlas.BuildAndUpload</c>, which walks the raw strings to discover the
/// glyphs the atlas must carry.
///
/// <para>Scans the locale CSV files directly — rather than waiting for
/// <c>LocalizationStage</c> to load catalogs into the <c>LocaleService</c> — so the font
/// atlas is correct from the very first frame the splash renders.</para>
/// </summary>
public static class LocalizationFontGlyphs
{
    /// <summary>Returns the raw CSV text of every bundled locale file. Caller iterates
    /// for atlas baking. Empty when no localization directory is present (host without
    /// bundled content).</summary>
    public static IReadOnlyList<string> ReadCorpus(IVfs vfs)
    {
        var texts = new List<string>();
        try
        {
            var directory = Path.GetDirectoryName(vfs.Realize("res://localization/en.csv"));
            if (directory is not null && Directory.Exists(directory))
            {
                foreach (var file in Directory.EnumerateFiles(directory, "*.csv"))
                {
                    texts.Add(File.ReadAllText(file));
                }
            }
        }
        catch (IOException)
        {
            // Fall through with whatever was read — the base ranges still cover en / ru.
        }

        return texts;
    }
}
