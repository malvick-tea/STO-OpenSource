namespace Garupan.Content;

/// <summary>
/// One crew member of a vehicle. <see cref="GivenName"/> / <see cref="FamilyName"/> hold the
/// display name as authored; they are not translated.
/// <see cref="RoleKey"/> is a raw localisation key for the role label
/// ("crew.role.commander" → "Commander" / "Командир" / "車長").
/// </summary>
public sealed record CrewMember(
    string Id,
    string GivenName,
    string FamilyName,
    CrewRole Role,
    string RoleKey,
    string SchoolKey);
