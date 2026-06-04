using System;
using System.IO;

namespace Garupan.Content;

/// <summary>One battle-map candidate from <c>content/maps/catalog.csv</c>. Candidates are tried
/// in declaration order, so content authors choose the preferred map without recompiling the
/// client or server. Asset names are leaf files rooted under <c>content/maps/</c>.</summary>
public sealed record BattleMapSpec(
    string Id,
    string RenderModelFileName,
    string HeightFieldFileName,
    string? PropsFileName,
    string? ObstaclesFileName)
{
    private const string RenderModelExtension = ".glb";
    private const string HeightFieldExtension = ".heightfield";
    private const string PropsFileSuffix = "-props.csv";
    private const string ObstaclesFileSuffix = "-obstacles.csv";

    internal static BattleMapSpec CreateValidated(
        string id,
        string renderModelFileName,
        string heightFieldFileName,
        string propsFileName,
        string obstaclesFileName)
    {
        var validatedPropsFileName = string.IsNullOrWhiteSpace(propsFileName)
            ? null
            : RequireLeafFile(propsFileName, nameof(propsFileName), PropsFileSuffix);
        var validatedObstaclesFileName = string.IsNullOrWhiteSpace(obstaclesFileName)
            ? null
            : RequireLeafFile(obstaclesFileName, nameof(obstaclesFileName), ObstaclesFileSuffix);
        return new BattleMapSpec(
            RequireNonEmpty(id, nameof(id)),
            RequireLeafFile(renderModelFileName, nameof(renderModelFileName), RenderModelExtension),
            RequireLeafFile(heightFieldFileName, nameof(heightFieldFileName), HeightFieldExtension),
            validatedPropsFileName,
            validatedObstaclesFileName);
    }

    private static string RequireLeafFile(string value, string parameterName, string suffix)
    {
        var trimmed = RequireNonEmpty(value, parameterName);
        if (!string.Equals(Path.GetFileName(trimmed), trimmed, StringComparison.Ordinal)
            || trimmed.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            throw new ArgumentException("Battle-map asset must be a leaf file name.", parameterName);
        }

        if (!trimmed.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
        {
            throw new ArgumentException($"Battle-map asset must end with \"{suffix}\".", parameterName);
        }

        return trimmed;
    }

    private static string RequireNonEmpty(string value, string parameterName)
    {
        var trimmed = value.Trim();
        return trimmed.Length > 0
            ? trimmed
            : throw new ArgumentException("Battle-map value must not be empty.", parameterName);
    }
}
