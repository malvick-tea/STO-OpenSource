using System;
using System.Collections.Generic;
using System.IO;

namespace Garupan.Content;

/// <summary>
/// Master roster of vehicles available in the game. The data lives in <c>data/tanks.csv</c>
/// (embedded into this assembly as <c>tanks.csv</c>) — NOT in C# constants — so the catalogue
/// scales to the 200+ vehicles the game targets without a code change per chassis: a new tank
/// is one CSV row referencing a shared <see cref="GunCatalog"/> / <see cref="GunMountCatalog"/>
/// / <see cref="GroundDriveCatalog"/> envelope by id.
/// </summary>
/// <remarks>
/// The named accessors below (<see cref="VehicleMediumA"/>, …) are convenience handles for the canon
/// vehicles that runtime code and tests reference by name; they resolve from the loaded
/// roster, so they stay in lock-step with the CSV and add no second source of truth. New
/// chassis are reached via <see cref="FindById"/> / <see cref="All"/> with no accessor needed.
/// Loading is lazy + cached; a malformed roster surfaces on first access (see
/// <see cref="TankRosterCsv"/>). To author rosters at runtime without recompiling, ship an
/// external <c>data/tanks.csv</c> and load it through <see cref="TankRosterCsv.LoadFile"/>.
/// </remarks>
public static class TankRoster
{
    /// <summary>Manifest name of the embedded baseline roster (see the <c>LogicalName</c>
    /// on the <c>EmbeddedResource</c> in <c>Garupan.Content.csproj</c>).</summary>
    private const string EmbeddedResourceName = "Garupan.Content.tanks.csv";

    private static readonly Lazy<IReadOnlyList<TankSpec>> Roster = new(LoadEmbedded);
    private static readonly Lazy<IReadOnlyDictionary<string, TankSpec>> Index =
        new(() => BuildIndex(Roster.Value));

    /// <summary>Order of unlock in the player commander's canonical progression. First entry is the starting vehicle.</summary>
    public static IReadOnlyList<TankSpec> All => Roster.Value;

    public static TankSpec VehicleMediumA => RequireById("vehicle_medium_a");

    public static TankSpec VehicleMediumB => RequireById("vehicle_medium_b");

    public static TankSpec VehicleHeavyA => RequireById("vehicle_heavy_a");

    public static TankSpec VehicleMediumC => RequireById("vehicle_medium_c");

    public static TankSpec VehicleLightA => RequireById("vehicle_light_a");

    public static TankSpec VehicleAssaultA => RequireById("vehicle_assault_a");

    public static TankSpec VehicleMediumD => RequireById("vehicle_medium_d");

    public static TankSpec VehicleHeavyB => RequireById("vehicle_heavy_b");

    public static TankSpec VehicleHeavyC => RequireById("vehicle_heavy_c");

    public static TankSpec VehicleMediumE => RequireById("vehicle_medium_e");

    public static TankSpec VehicleHeavyD => RequireById("vehicle_heavy_d");

    public static TankSpec VehicleMediumF => RequireById("vehicle_medium_f");

    public static TankSpec? FindById(string id) =>
        Index.Value.TryGetValue(id, out var spec) ? spec : null;

    /// <summary>Resolves a roster entry by id or throws — used by the canon named accessors,
    /// where a missing id means the embedded <c>tanks.csv</c> lost a canon row.</summary>
    public static TankSpec RequireById(string id) =>
        FindById(id) ?? throw new KeyNotFoundException($"Tank roster has no vehicle with id \"{id}\".");

    private static IReadOnlyList<TankSpec> LoadEmbedded()
    {
        var assembly = typeof(TankRoster).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded tank roster \"{EmbeddedResourceName}\" is missing from {assembly.GetName().Name}.");
        using var reader = new StreamReader(stream);
        return TankRosterCsv.Parse(reader.ReadToEnd());
    }

    private static IReadOnlyDictionary<string, TankSpec> BuildIndex(IReadOnlyList<TankSpec> all)
    {
        var byId = new Dictionary<string, TankSpec>(StringComparer.Ordinal);
        foreach (var spec in all)
        {
            byId[spec.Id] = spec;
        }

        return byId;
    }
}
