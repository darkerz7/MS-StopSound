using Microsoft.Extensions.Configuration;
using Sharp.Modules.ClientPreferences.Shared;
using Sharp.Modules.LocalizerManager.Shared;
using Sharp.Shared;
using Sharp.Shared.Definition;
using Sharp.Shared.Enums;
using Sharp.Shared.Managers;
using Sharp.Shared.Objects;
using Sharp.Shared.Types;

namespace MS_StopSound
{
    public class StopSound : IModSharpModule
    {
        public string DisplayName => "StopSound";
        public string DisplayAuthor => "DarkerZ[RUS]";

        public StopSound(ISharedSystem sharedSystem, string dllPath, string sharpPath, Version version, IConfiguration coreConfiguration, bool hotReload)
        {
            _modules = sharedSystem.GetSharpModuleManager();
            _clients = sharedSystem.GetClientManager();
            _transmits = sharedSystem.GetTransmitManager();
        }

        private readonly ISharpModuleManager _modules;
        private readonly IClientManager _clients;
        private readonly ITransmitManager _transmits;

        private IDisposable? _callback;

        private IModSharpModuleInterface<ILocalizerManager>? _localizer;
        private IModSharpModuleInterface<IClientPreference>? _icp;

        private readonly bool[] g_bStopsound = new bool[65];

        public bool Init()
        {
            _clients.InstallCommandCallback("stopsound", OnStopSoundCommand);
            return true;
        }

        public void OnAllModulesLoaded()
        {
            GetClientPrefs();
            GetLocalizer()?.LoadLocaleFile("StopSound");
        }

        public void OnLibraryConnected(string name)
        {
            if (name.Equals("ClientPreferences")) GetClientPrefs();
        }

        public void OnLibraryDisconnect(string name)
        {
            if (name.Equals("ClientPreferences")) _icp = null;
        }

        private void OnCookieLoad(IGameClient client)
        {
            if (client == null || !client.IsValid || GetClientPrefs() is not { } cp || !cp.IsLoaded(client.SteamId)) return;

            if (cp.GetCookie(client.SteamId, "StopSound") is { } cookie_enabled)
            {
                string sValue = cookie_enabled.GetString();
                if (string.IsNullOrEmpty(sValue) || !Byte.TryParse(sValue, out byte iValue)) iValue = 0;
                if (iValue == 0) g_bStopsound[client.Slot] = false;
                else g_bStopsound[client.Slot] = true;
            }
            else
            {
                cp.SetCookie(client.SteamId, "StopSound", "0");
                g_bStopsound[client.Slot] = false;
            }
            _transmits.SetTempEntState(BlockTempEntType.FireBullets, client.Slot, !g_bStopsound[client.Slot]);
        }

        public void Shutdown()
        {
            _clients.RemoveCommandCallback("stopsound", OnStopSoundCommand);
            _callback?.Dispose();
        }

        private ECommandAction OnStopSoundCommand(IGameClient client, StringCommand command)
        {
            if (client == null || !client.IsValid) return ECommandAction.Stopped;
            g_bStopsound[client.Slot] = !g_bStopsound[client.Slot];
            _transmits.SetTempEntState(BlockTempEntType.FireBullets, client.Slot, !g_bStopsound[client.Slot]);
            if (GetClientPrefs() is { } cp && cp.IsLoaded(client.SteamId))
            {
                cp.SetCookie(client.SteamId, "StopSound", g_bStopsound[client.Slot] ? "1" : "0");
            }
            if (client.GetPlayerController() is { } player && GetLocalizer() is { } lm)
            {
                var localizer = lm.GetLocalizer(client);
                player.Print(command.ChatTrigger ? HudPrintChannel.Chat : HudPrintChannel.Console, $" {ChatColor.Blue}[{ChatColor.Green}StopSound{ChatColor.Blue}]{ChatColor.White} {ReplaceColorTags(g_bStopsound[client.Slot] ? localizer.Format("StopSound.Enabled") : localizer.Format("StopSound.Disabled"))}");
            }
            return ECommandAction.Stopped;
        }

        private string ReplaceColorTags(string input)
        {
            for (var i = 0; i < colorPatterns.Length; i++)
                input = input.Replace(colorPatterns[i], colorReplacements[i]);

            return input;
        }
        readonly string[] colorPatterns =
        [
            "{default}", "{darkred}", "{purple}", "{green}", "{lightgreen}", "{lime}", "{red}", "{grey}",
            "{olive}", "{a}", "{lightblue}", "{blue}", "{d}", "{pink}", "{darkorange}", "{orange}",
            "{white}", "{yellow}", "{magenta}", "{silver}", "{bluegrey}", "{lightred}", "{cyan}", "{gray}"
        ];
        readonly string[] colorReplacements =
        [
            "\x01", "\x02", "\x03", "\x04", "\x05", "\x06", "\x07", "\x08",
            "\x09", "\x0A", "\x0B", "\x0C", "\x0D", "\x0E", "\x0F", "\x10",
            "\x01", "\x09", "\x0E", "\x0A", "\x0D", "\x0F", "\x03", "\x08"
        ];

        private ILocalizerManager? GetLocalizer()
        {
            if (_localizer?.Instance is null)
            {
                _localizer = _modules.GetOptionalSharpModuleInterface<ILocalizerManager>(ILocalizerManager.Identity);
            }
            return _localizer?.Instance;
        }

        private IClientPreference? GetClientPrefs()
        {
            if (_icp?.Instance is null)
            {
                _icp = _modules.GetOptionalSharpModuleInterface<IClientPreference>(IClientPreference.Identity);
                if (_icp?.Instance is { } instance) _callback = instance.ListenOnLoad(OnCookieLoad);
            }
            return _icp?.Instance;
        }
    }
}
