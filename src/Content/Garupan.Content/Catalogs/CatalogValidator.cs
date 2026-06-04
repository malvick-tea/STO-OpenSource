using System.Collections.Generic;

namespace Garupan.Content;

/// <summary>
/// Runtime cross-catalogue defence-in-depth. Asserts that every <see cref="TankSpec"/>
/// in <see cref="TankRoster.All"/> has valid physical data, references an
/// <see cref="AmmoSpec"/> id that resolves in <see cref="AmmoCatalog"/>, and (when a
/// <see cref="SchoolPalette"/> is supplied) has a <see cref="TankSpec.School"/> that
/// resolves in the loaded palette so the renderer can look up a camo tint for every
/// spawned vehicle.
///
/// The build-time invariants (record types, compile-time constants) already make these
/// branches unreachable from current Garupan code, but the validator is the safety net
/// for:
/// <list type="bullet">
/// <item><description>future JSON-loaded catalogues (mods, balance-pass overlays);</description></item>
/// <item><description>community packs that override individual catalogue entries;</description></item>
/// <item><description>a refactor that accidentally renames an ammo id and leaves a dangling reference;</description></item>
/// <item><description>a partial <c>data/school-palette.csv</c> missing a school that a roster entry references.</description></item>
/// </list>
///
/// Mirrors C++ <c>TankCatalog::load_from_string</c>'s validation pass — the loader there
/// rejects any spec whose ammo references don't resolve. We call this from a boot stage
/// so a broken release dataset fails fast and loudly instead of silently mis-firing in a
/// match.
/// </summary>
public static class CatalogValidator
{
    public sealed record ValidationResult(bool Ok, IReadOnlyList<string> Errors);

    /// <summary>Validates roster physical data and the static <see cref="AmmoCatalog"/>.
    /// School coverage is not checked — use the <see cref="Validate(SchoolPalette)"/>
    /// overload for that.</summary>
    public static ValidationResult Validate()
    {
        var errors = new List<string>();
        foreach (var tank in TankRoster.All)
        {
            ValidateTankAmmo(tank, errors);
            ValidateTankPhysicalSpec(tank, errors);
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    /// <summary>Validates the roster against the ammo catalogue AND verifies every
    /// <see cref="TankSpec.School"/> resolves in the supplied <paramref name="palette"/>.
    /// Use this overload at boot time when the canonical palette CSV has been loaded — a
    /// missing school entry fails the validation rather than silently rendering at
    /// identity tint.</summary>
    public static ValidationResult Validate(SchoolPalette palette)
    {
        System.ArgumentNullException.ThrowIfNull(palette);
        var errors = new List<string>();
        foreach (var tank in TankRoster.All)
        {
            ValidateTankAmmo(tank, errors);
            ValidateTankPhysicalSpec(tank, errors);
            ValidateTankSchool(tank, palette, errors);
        }

        return new ValidationResult(errors.Count == 0, errors);
    }

    private static void ValidateTankAmmo(TankSpec tank, List<string> errors)
    {
        if (string.IsNullOrWhiteSpace(tank.Gun.DefaultAmmoId))
        {
            errors.Add($"tank '{tank.Id}': gun has empty DefaultAmmoId");
            return;
        }

        if (AmmoCatalog.FindById(tank.Gun.DefaultAmmoId) is null)
        {
            errors.Add(
                $"tank '{tank.Id}': gun default ammo id '{tank.Gun.DefaultAmmoId}' does not resolve in AmmoCatalog");
        }
    }

    private static void ValidateTankSchool(TankSpec tank, SchoolPalette palette, List<string> errors)
    {
        if (!palette.Contains(tank.School))
        {
            errors.Add(
                $"tank '{tank.Id}': school '{tank.School}' has no entry in the loaded SchoolPalette");
        }
    }

    private static void ValidateTankPhysicalSpec(TankSpec tank, List<string> errors)
    {
        var mobility = tank.Mobility;
        if (mobility.MassTonnes <= 0
            || mobility.BodyLengthMeters <= 0
            || mobility.BodyWidthMeters <= 0
            || mobility.BodyHeightMeters <= 0)
        {
            errors.Add($"tank '{tank.Id}': mobility dimensions and mass must be positive");
        }

        var drive = mobility.Drive;
        if (drive is null || drive.ForwardGearRatios is null || drive.ForwardGearRatios.Count == 0)
        {
            errors.Add($"tank '{tank.Id}': drive must define at least one forward gear");
        }
        else
        {
            for (var i = 0; i < drive.ForwardGearRatios.Count; i++)
            {
                if (drive.ForwardGearRatios[i] <= 0)
                {
                    errors.Add($"tank '{tank.Id}': drive forward gear {i} must be positive");
                }
            }

            if (drive.ReverseGearRatio <= 0
                || drive.FinalDriveRatio <= 0
                || drive.MaximumHullTraverseRadiansPerSecond <= 0
                || drive.EngineBrakingRatePerSecond < 0
                || drive.TurningResistanceCoefficientSeconds < 0)
            {
                errors.Add($"tank '{tank.Id}': drive ratios and tracked-contact tuning are invalid");
            }
        }

        var mount = tank.GunMount;
        if (mount is null
            || mount.MinPitchDegrees >= mount.MaxPitchDegrees
            || mount.TrunnionForwardMeters < 0
            || mount.TrunnionHeightMeters < 0
            || mount.BarrelLengthMeters <= 0)
        {
            errors.Add($"tank '{tank.Id}': gun mount geometry is invalid");
        }
    }
}
