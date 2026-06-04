using System;
using System.Collections.Generic;
using System.Reflection;
using Opus.Localisation;

namespace Garupan.Tools.LocLint;

/// <summary>Collects every <see cref="TranslationKey"/> exposed as a
/// <c>public static</c> field on a root registry type and every nested type underneath
/// it. <see cref="L10nKeys"/> is the canonical registry; this scanner doesn't hard-code
/// the type so test fixtures can feed in a stub registry without re-implementing the
/// traversal.</summary>
internal sealed class KeyRegistryScanner
{
    /// <summary>Walks <paramref name="rootType"/> and every nested public type
    /// recursively, returning the deduplicated set of key strings. Empty keys are
    /// silently filtered — they cannot reach any catalog anyway.</summary>
    public IReadOnlyCollection<string> Collect(Type rootType)
    {
        ArgumentNullException.ThrowIfNull(rootType);
        var keys = new HashSet<string>(StringComparer.Ordinal);
        Visit(rootType, keys);
        return keys;
    }

    private static void Visit(Type type, HashSet<string> keys)
    {
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly;

        foreach (var field in type.GetFields(Flags))
        {
            if (field.FieldType != typeof(TranslationKey))
            {
                continue;
            }

            var value = (TranslationKey)(field.GetValue(null) ?? default(TranslationKey));
            if (!string.IsNullOrEmpty(value.Key))
            {
                keys.Add(value.Key);
            }
        }

        foreach (var nested in type.GetNestedTypes(BindingFlags.Public))
        {
            Visit(nested, keys);
        }
    }
}
