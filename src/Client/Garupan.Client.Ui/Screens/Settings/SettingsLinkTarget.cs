namespace Garupan.Client.Ui.Screens.Settings;

/// <summary>Sub-screen a <see cref="SettingsLinkOption"/> opens. Decoupling the routing
/// from <see cref="SettingsScreen"/> via this enum keeps the pure model layer free of
/// any <c>ScreenStack</c> reference.</summary>
public enum SettingsLinkTarget
{
    /// <summary>The rebindable-keys <see cref="ControlsScreen"/>.</summary>
    Controls,

    /// <summary>The local test endpoint editor — <c>MultiplayerSettingsScreen</c>.</summary>
    Multiplayer,
}
