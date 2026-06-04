using System;

namespace Garupan.Content;

/// <summary>
/// Per-school AI personality knobs. Tuned to canon armored combat� behaviour — RivalDelta's siege
/// patience, RivalEcho's overwhelming firepower, RivalCharlie's flamboyant aggression — but
/// authored as data (<c>data/ai-personalities.csv</c>) so a designer / balancer iterates
/// without touching C#. Phase-0 ships three knobs; suppression-driven degradation
/// (Phase 6) layers on top by *scaling* these baselines at runtime, never replacing them.
/// </summary>
/// <param name="School">The opponent school this personality applies to.</param>
/// <param name="EngageRangeMeters">Maximum target distance before the bot disengages.
/// Wider than the C++ 60 m default for snipers (RivalEcho / RivalBravo), tighter for
/// brawlers (RivalFoxtrot).</param>
/// <param name="ThrottleScale">Cruise throttle in [0, 1]. RivalDelta crawls, RivalCharlie sprints.
/// Multiplied onto the system's full-throttle target — never additive.</param>
/// <param name="AlignmentToleranceRadians">How close the turret must be to the target
/// bearing before the bot will fire. Tighter = more disciplined gunnery.</param>
/// <remarks>
/// All three knobs are finite + positive + bounded — the loader enforces every constraint
/// so a typo in the CSV surfaces at boot, not deep in a match. The record is immutable
/// (record + init-only by default) so a personality, once loaded, is shared safely across
/// every bot stamped with that school.
/// </remarks>
public sealed record BotPersonality(
    OpponentSchool School,
    float EngageRangeMeters,
    float ThrottleScale,
    float AlignmentToleranceRadians)
{
    /// <summary>The C++-reference Phase-0 personality — used as the AI fallback when a
    /// school has no entry in the loaded catalog. Matches the previous magic constants
    /// in <c>AiBotSystem</c> exactly so a roster without a personality CSV behaves as
    /// the M3 / M4 demos did.</summary>
    public static readonly BotPersonality LegacyFallback = new(
        School: OpponentSchool.PlayerSchool,
        EngageRangeMeters: 60f,
        ThrottleScale: 0.5f,
        AlignmentToleranceRadians: 0.05f);

    internal static BotPersonality CreateValidated(
        OpponentSchool school,
        float engageRangeMeters,
        float throttleScale,
        float alignmentToleranceRadians)
    {
        EnsurePositiveFinite(engageRangeMeters, nameof(engageRangeMeters));
        EnsureFiniteInRange(throttleScale, 0f, 1f, nameof(throttleScale));
        EnsureFiniteInRange(alignmentToleranceRadians, 0f, MathF.PI, nameof(alignmentToleranceRadians));
        return new BotPersonality(school, engageRangeMeters, throttleScale, alignmentToleranceRadians);
    }

    private static void EnsurePositiveFinite(float value, string paramName)
    {
        if (!float.IsFinite(value) || value <= 0f)
        {
            throw new ArgumentOutOfRangeException(paramName, value, $"{paramName} must be positive and finite");
        }
    }

    private static void EnsureFiniteInRange(float value, float minInclusive, float maxInclusive, string paramName)
    {
        if (!float.IsFinite(value) || value < minInclusive || value > maxInclusive)
        {
            throw new ArgumentOutOfRangeException(
                paramName,
                value,
                $"{paramName} must be finite and within [{minInclusive}, {maxInclusive}]");
        }
    }
}
