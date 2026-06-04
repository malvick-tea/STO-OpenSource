using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Per-tank audio profiles loaded from <c>data/tank-audio.csv</c> (embedded into this assembly
/// as <c>tank-audio.csv</c>). Like <see cref="TankRoster"/>, the data lives in a CSV — not C#
/// constants — so a vehicle's sound set is one data row resolved by tank id, and the genre-neutral
/// engine never learns which assets a consumer ships.
/// </summary>
/// <remarks>
/// Loading is lazy + cached; a malformed catalogue surfaces on first access (see
/// <see cref="TankAudioProfileCsv"/>). To re-author at runtime without recompiling, ship an
/// external <c>data/tank-audio.csv</c> and load it via <see cref="TankAudioProfileCsv.LoadFile"/>.
/// </remarks>
public static class TankAudioCatalog
{
    private const string EmbeddedResourceName = "Garupan.Content.tank-audio.csv";

    private static readonly Lazy<IReadOnlyDictionary<string, TankAudioProfile>> Profiles = new(LoadEmbedded);

    /// <summary>Resolves a tank's audio profile by id; null when the id has no row.</summary>
    public static TankAudioProfile? FindById(string id) =>
        Profiles.Value.TryGetValue(id, out var profile) ? profile : null;

    /// <summary>Resolves a tank's audio profile by id or throws — used where the caller knows the
    /// vehicle is in the catalogue (e.g. the player's tank).</summary>
    public static TankAudioProfile RequireById(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Tank-audio catalogue has no profile for id \"{id}\".");

    /// <summary>Resolves the data-authored profile used until the player's selected tank is
    /// replicated into the client scene plan.</summary>
    public static TankAudioProfile RequireDefault()
    {
        foreach (var profile in Profiles.Value.Values)
        {
            if (profile.IsDefault)
            {
                return profile;
            }
        }

        throw new InvalidOperationException("Tank-audio catalogue has no default profile.");
    }

    private static IReadOnlyDictionary<string, TankAudioProfile> LoadEmbedded()
    {
        var assembly = typeof(TankAudioCatalog).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded tank-audio catalogue \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return TankAudioProfileCsv.Parse(reader.ReadToEnd());
    }
}
