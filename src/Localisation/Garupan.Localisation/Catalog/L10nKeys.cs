using Opus.Localisation;

namespace Garupan.Localisation;

/// <summary>
/// Centralised registry of UI / Sim-side translation keys. All callers route through
/// here so the lint pass (planned: <c>sto-tools loc-lint</c>) can verify each is
/// present in the master CSV. New keys MUST be added here — no raw <c>Tr("...")</c>
/// literals are allowed in UI code (planned analyzer GS060).
/// </summary>
public static class L10nKeys
{
    public static class Common
    {
        public static readonly TranslationKey Ok        = TranslationKey.Of("common.ok");
        public static readonly TranslationKey Cancel    = TranslationKey.Of("common.cancel");
        public static readonly TranslationKey Apply     = TranslationKey.Of("common.apply");
        public static readonly TranslationKey Back      = TranslationKey.Of("common.back");
        public static readonly TranslationKey Close     = TranslationKey.Of("common.close");
        public static readonly TranslationKey Yes       = TranslationKey.Of("common.yes");
        public static readonly TranslationKey No        = TranslationKey.Of("common.no");
    }

    public static class Splash
    {
        public static readonly TranslationKey Tagline   = TranslationKey.Of("splash.tagline");
    }

    public static class MainMenu
    {
        public static readonly TranslationKey Title       = TranslationKey.Of("menu.title");
        public static readonly TranslationKey Subtitle    = TranslationKey.Of("menu.subtitle");
        public static readonly TranslationKey Campaign    = TranslationKey.Of("menu.campaign");
        public static readonly TranslationKey Garage      = TranslationKey.Of("menu.garage");
        public static readonly TranslationKey Barracks    = TranslationKey.Of("menu.barracks");
        public static readonly TranslationKey Archive     = TranslationKey.Of("menu.archive");
        public static readonly TranslationKey Settings    = TranslationKey.Of("menu.settings");
        public static readonly TranslationKey Quit        = TranslationKey.Of("menu.quit");
        public static readonly TranslationKey Proceed     = TranslationKey.Of("menu.proceed");
        public static readonly TranslationKey QuitConfirm = TranslationKey.Of("menu.quit.confirm");
    }

    public static class Profile
    {
        public static readonly TranslationKey GuestName       = TranslationKey.Of("profile.guest");
        public static readonly TranslationKey RankCommander   = TranslationKey.Of("profile.rank.commander");
        public static readonly TranslationKey CurrencySilver  = TranslationKey.Of("profile.currency.silver");
        public static readonly TranslationKey CurrencyGold    = TranslationKey.Of("profile.currency.gold");
        public static readonly TranslationKey CurrencyXp      = TranslationKey.Of("profile.currency.xp");
    }

    public static class Settings
    {
        public static readonly TranslationKey Title           = TranslationKey.Of("settings.title");
        public static readonly TranslationKey TabGraphics     = TranslationKey.Of("settings.tab.graphics");
        public static readonly TranslationKey TabAudio        = TranslationKey.Of("settings.tab.audio");
        public static readonly TranslationKey TabControls     = TranslationKey.Of("settings.tab.controls");
        public static readonly TranslationKey TabLanguage     = TranslationKey.Of("settings.tab.language");
        public static readonly TranslationKey TabMultiplayer  = TranslationKey.Of("settings.tab.multiplayer");
        public static readonly TranslationKey MasterVolume    = TranslationKey.Of("settings.audio.master");
        public static readonly TranslationKey MusicVolume     = TranslationKey.Of("settings.audio.music");
        public static readonly TranslationKey SfxVolume       = TranslationKey.Of("settings.audio.sfx");
        public static readonly TranslationKey UiVolume        = TranslationKey.Of("settings.audio.ui");
        public static readonly TranslationKey Locale          = TranslationKey.Of("settings.language.locale");
        public static readonly TranslationKey VSync           = TranslationKey.Of("settings.video.vsync");
        public static readonly TranslationKey Resolution      = TranslationKey.Of("settings.video.resolution");
        public static readonly TranslationKey Restart         = TranslationKey.Of("settings.video.restart");
        public static readonly TranslationKey Hint            = TranslationKey.Of("settings.hint");

        public static class Multiplayer
        {
            public static readonly TranslationKey Edit            = TranslationKey.Of("settings.multiplayer.edit");
            public static readonly TranslationKey Host            = TranslationKey.Of("settings.multiplayer.host");
            public static readonly TranslationKey Port            = TranslationKey.Of("settings.multiplayer.port");
            public static readonly TranslationKey Hint            = TranslationKey.Of("settings.multiplayer.hint");
            public static readonly TranslationKey Valid           = TranslationKey.Of("settings.multiplayer.valid");
            public static readonly TranslationKey Invalid         = TranslationKey.Of("settings.multiplayer.invalid");
            public static readonly TranslationKey SectionDefault  = TranslationKey.Of("settings.multiplayer.section.default");
            public static readonly TranslationKey SectionHungry   = TranslationKey.Of("settings.multiplayer.section.hungry");
            public static readonly TranslationKey SectionTactical = TranslationKey.Of("settings.multiplayer.section.tactical");
            public static readonly TranslationKey UseDefault      = TranslationKey.Of("settings.multiplayer.use_default");
        }
    }

    public static class Controls
    {
        public static readonly TranslationKey MoveForward  = TranslationKey.Of("controls.move_forward");
        public static readonly TranslationKey MoveBackward = TranslationKey.Of("controls.move_backward");
        public static readonly TranslationKey SteerLeft    = TranslationKey.Of("controls.steer_left");
        public static readonly TranslationKey SteerRight   = TranslationKey.Of("controls.steer_right");
        public static readonly TranslationKey Fire         = TranslationKey.Of("controls.fire");
        public static readonly TranslationKey Edit         = TranslationKey.Of("controls.edit");
        public static readonly TranslationKey Hint         = TranslationKey.Of("controls.hint");
        public static readonly TranslationKey Listening    = TranslationKey.Of("controls.listening");
    }

    public static class School
    {
        public static readonly TranslationKey PlayerSchool       = TranslationKey.Of("school.player_school");
    }

    public static class Tanks
    {
        public static readonly TranslationKey VehicleMediumA    = TranslationKey.Of("tank.vehicle_medium_a");
    }

    public static class Crew
    {
        public static readonly TranslationKey RoleCommander = TranslationKey.Of("crew.role.commander");
        public static readonly TranslationKey RoleGunner    = TranslationKey.Of("crew.role.gunner");
        public static readonly TranslationKey RoleLoader    = TranslationKey.Of("crew.role.loader");
        public static readonly TranslationKey RoleDriver    = TranslationKey.Of("crew.role.driver");
        public static readonly TranslationKey RoleRadio     = TranslationKey.Of("crew.role.radio");
    }

    public static class Campaign
    {
        public static readonly TranslationKey Title              = TranslationKey.Of("campaign.title");
        public static readonly TranslationKey SelectMission      = TranslationKey.Of("campaign.select_mission");
        public static readonly TranslationKey StatusLocked       = TranslationKey.Of("campaign.status.locked");
        public static readonly TranslationKey StatusAvailable    = TranslationKey.Of("campaign.status.available");
        public static readonly TranslationKey StatusComplete     = TranslationKey.Of("campaign.status.complete");
        public static readonly TranslationKey StatusHidden       = TranslationKey.Of("campaign.status.hidden");
        public static readonly TranslationKey LockedHint         = TranslationKey.Of("campaign.locked_hint");
        public static readonly TranslationKey HiddenFirstHint    = TranslationKey.Of("campaign.hidden.first_hint");
        public static readonly TranslationKey HiddenArcBlock     = TranslationKey.Of("campaign.hidden.arc_block");
        public static readonly TranslationKey BriefingTitle      = TranslationKey.Of("campaign.briefing.title");
        public static readonly TranslationKey BriefingObjective  = TranslationKey.Of("campaign.briefing.objective");
        public static readonly TranslationKey BriefingOpponent   = TranslationKey.Of("campaign.briefing.opponent");
        public static readonly TranslationKey BriefingEpisode    = TranslationKey.Of("campaign.briefing.episode");
        public static readonly TranslationKey BriefingEnvironment = TranslationKey.Of("campaign.briefing.environment");
        public static readonly TranslationKey BriefingDeploy     = TranslationKey.Of("campaign.briefing.deploy");

        public static readonly TranslationKey SampleName       = TranslationKey.Of("campaign.sample.name");
        public static readonly TranslationKey SampleSubtitle   = TranslationKey.Of("campaign.sample.subtitle");
    }

    public static class Match
    {
        public static readonly TranslationKey Paused    = TranslationKey.Of("match.paused");
        public static readonly TranslationKey Resume    = TranslationKey.Of("match.resume");
        public static readonly TranslationKey Abandon   = TranslationKey.Of("match.abandon");
        public static readonly TranslationKey PauseHint = TranslationKey.Of("match.pause_hint");
    }

    public static class Lobby
    {
        public static readonly TranslationKey Title           = TranslationKey.Of("lobby.title");
        public static readonly TranslationKey Hint            = TranslationKey.Of("lobby.hint");
        public static readonly TranslationKey ClosedAlpha     = TranslationKey.Of("lobby.queue.closed");
        public static readonly TranslationKey Deploy          = TranslationKey.Of("lobby.deploy");
        public static readonly TranslationKey Respawns        = TranslationKey.Of("lobby.mode.respawns");
        public static readonly TranslationKey CommanderLed    = TranslationKey.Of("lobby.mode.commander_led");
        public static readonly TranslationKey FreeForAll      = TranslationKey.Of("lobby.mode.free_for_all");

        public static readonly TranslationKey HungryName      = TranslationKey.Of("lobby.mode.hungry.name");
        public static readonly TranslationKey HungrySummary   = TranslationKey.Of("lobby.mode.hungry.summary");
        public static readonly TranslationKey TacticalName    = TranslationKey.Of("lobby.mode.tactical.name");
        public static readonly TranslationKey TacticalSummary = TranslationKey.Of("lobby.mode.tactical.summary");
    }

    public static class Schools
    {
        public static readonly TranslationKey PlayerSchool        = TranslationKey.Of("school.player_school");
        public static readonly TranslationKey RivalAlpha    = TranslationKey.Of("school.rival_alpha");
        public static readonly TranslationKey RivalBravo      = TranslationKey.Of("school.rival_bravo");
        public static readonly TranslationKey RivalCharlie         = TranslationKey.Of("school.rival_charlie");
        public static readonly TranslationKey RivalDelta        = TranslationKey.Of("school.rival_delta");
        public static readonly TranslationKey RivalEcho  = TranslationKey.Of("school.rival_echo");
        public static readonly TranslationKey RivalFoxtrot      = TranslationKey.Of("school.rival_foxtrot");
        public static readonly TranslationKey RivalGolf     = TranslationKey.Of("school.rival_golf");
    }

    public static class Garage
    {
        public static readonly TranslationKey Title             = TranslationKey.Of("garage.title");

        public static readonly TranslationKey ArmorTitle        = TranslationKey.Of("garage.armor.title");
        public static readonly TranslationKey ArmorFront        = TranslationKey.Of("garage.armor.front");
        public static readonly TranslationKey ArmorSide         = TranslationKey.Of("garage.armor.side");
        public static readonly TranslationKey ArmorRear         = TranslationKey.Of("garage.armor.rear");
        public static readonly TranslationKey ArmorRoof         = TranslationKey.Of("garage.armor.roof");

        public static readonly TranslationKey GunTitle          = TranslationKey.Of("garage.gun.title");
        public static readonly TranslationKey GunCaliber        = TranslationKey.Of("garage.gun.caliber");
        public static readonly TranslationKey GunPenetration    = TranslationKey.Of("garage.gun.penetration");
        public static readonly TranslationKey GunDamage         = TranslationKey.Of("garage.gun.damage");
        public static readonly TranslationKey GunReload         = TranslationKey.Of("garage.gun.reload");
        public static readonly TranslationKey GunRpm            = TranslationKey.Of("garage.gun.rpm");

        public static readonly TranslationKey MobilityTitle     = TranslationKey.Of("garage.mobility.title");
        public static readonly TranslationKey MobilitySpeed     = TranslationKey.Of("garage.mobility.speed");
        public static readonly TranslationKey MobilityPower     = TranslationKey.Of("garage.mobility.power");
        public static readonly TranslationKey MobilityHullTrav  = TranslationKey.Of("garage.mobility.hull_traverse");
        public static readonly TranslationKey MobilityTurrTrav  = TranslationKey.Of("garage.mobility.turret_traverse");

        public static readonly TranslationKey CrewTitle         = TranslationKey.Of("garage.crew.title");
        public static readonly TranslationKey ActionModify      = TranslationKey.Of("garage.action.modify");
        public static readonly TranslationKey ActionCrew        = TranslationKey.Of("garage.action.crew");

        public static readonly TranslationKey UnitMm            = TranslationKey.Of("garage.unit.mm");
        public static readonly TranslationKey UnitKph           = TranslationKey.Of("garage.unit.kph");
        public static readonly TranslationKey UnitHpPerTon      = TranslationKey.Of("garage.unit.hp_per_ton");
        public static readonly TranslationKey UnitDegPerSec     = TranslationKey.Of("garage.unit.deg_per_sec");
        public static readonly TranslationKey UnitSeconds       = TranslationKey.Of("garage.unit.seconds");
        public static readonly TranslationKey UnitRpm           = TranslationKey.Of("garage.unit.rpm");
    }
}
