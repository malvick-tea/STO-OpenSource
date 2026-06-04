namespace Garupan.Content;

/// <summary>
/// Roles a crew member can occupy. Each tank type lists which roles its crew slots
/// require — a medium tank has 5 (Commander, Gunner, Loader, Driver, RadioOperator).
/// </summary>
public enum CrewRole
{
    Commander,
    Gunner,
    Loader,
    Driver,
    RadioOperator,
}
