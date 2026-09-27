using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using UnityEngine;

namespace CasualtiesTogetherLateJoinFix;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = MyPluginInfo.PLUGIN_GUID;
    public const string ModName = MyPluginInfo.PLUGIN_NAME;
    public const string ModVersion = MyPluginInfo.PLUGIN_VERSION;

    internal new static ManualLogSource Logger;
    
    private readonly Harmony _harmony = new(ModGuid);
    
    private float _timer = 0;
    private int _attempts = 0;
    private bool _loaded = false;
    
    private void Awake()
    {
        Logger = base.Logger;
        // The MOD_VERSION field is const, which means its load will be optimized out during build time
        // We want to get the MOD_VERSION value of the installed MP mod during runtime
        var mpModVersion = (string)AccessTools.Field(typeof(KrokoshaCasualtiesMP.Plugin), nameof(KrokoshaCasualtiesMP.Plugin.MOD_VERSION)).GetValue(null);
        if (!mpModVersion.StartsWith("4."))
        {
            Logger.LogFatal($"This mod {ModName} is intended ONLY for the v4 version of the multiplayer mod!!! Uninstall me ({ModName}) NOW!!!");
            return;
        }
        WorldgenPatches.OnWorldgenFinish += OnWorldgenFinish;
        NetPlayer.OnPlayerJoined += OnPlayerJoined;
        _harmony.PatchAll();
        Logger.LogInfo($"Plugin {ModName} is loaded!");
        _loaded = true;
    }

    private void OnDestroy()
    {
        if (!_loaded)
            return;
        WorldgenPatches.OnWorldgenFinish -= OnWorldgenFinish;
        NetPlayer.OnPlayerJoined -= OnPlayerJoined;
        _harmony?.UnpatchSelf();
    }

    private void OnWorldgenFinish()
    {
        _attempts = 0;
        _timer = 0;
    }

    private void OnPlayerJoined(NetPlayer _)
    {
        _attempts = 0;
        _timer = 0;
    }

    private void LateUpdate()
    {
        if (!_loaded)
            return;
        
        if (!Net.running || Net.is_host || !Util.IsWorldGenerated())
            return;
        
        if (NetPlayer.ClientIdToPlayerDict.Count == NetPlayer.BodyToPlayerDict.Count)
            return;
        
        _timer += Time.deltaTime;
        if (_timer < 10.0f)
            return;
        if (_attempts >= 10)
        {
            _timer = 0;
            return;
        }
        
        PrintWarning("Attempting to fix player-body desync!");
        _attempts += 1;
        
        var bodies = FindObjectsByType<Body>(FindObjectsSortMode.None);
        var netBodies = FindObjectsByType<NetBody>(FindObjectsSortMode.None);
        PrintMessage($"Players: {NetPlayer.ClientIdToPlayerDict.Count}, bodies: {NetPlayer.BodyToPlayerDict.Count}, Body objects: {bodies.Length}, NetBody objects: {netBodies.Length}, NetBody.all_instances: {NetBody.all_instances.Count}.");
        if (bodies.Length != netBodies.Length)
        {
            PrintWarning($"Bodies ({bodies.Length}) and netBodies ({netBodies.Length}) doesn't match!");
            return;
        }
        foreach (var netBody in netBodies)
        {
            if (!netBody.player)
            {
                PrintWarning($"{netBody.name} doesn't have a player!");
                continue;
            }

            bool didSomething = false;
            
            if (!NetBody.all_instances.Contains(netBody))
            {
                NetBody.all_instances.Add(netBody);
                PrintMessage($"Added {netBody.player.playername}'s netBody to all_instances");
                didSomething = true;
            }
            
            if (!NetPlayer.BodyToPlayerDict.ContainsKey(netBody.body))
            {
                NetPlayer.BodyToPlayerDict.Add(netBody.body, netBody.player);
                PrintMessage($"Added {netBody.player.playername}'s netBody to BodyToPlayerDict");
                didSomething = true;
            }
            
            if (netBody.player.body == null)
            {
                netBody.player.body = netBody.body;
                PrintMessage($"Assigned {netBody.player.playername}'s netBody.body to their player.body");
                didSomething = true;
            }

            if (didSomething)
            {
                PrintMessage($"<b>LateJoinFix: Adjusted player {netBody.player.playername}!</b>");
            }
        }

        _timer = 0;
    }

    private static void PrintWarning(string message)
    {
        Logger.LogWarning(message);
        ConsoleScript.instance.LogToConsole($"<color=yellow>[{ModName}]: {message}</color>");
    }

    private static void PrintMessage(string message)
    {
        Logger.LogWarning(message);
        ConsoleScript.instance.LogToConsole($"[{ModName}]: {message}");
    }
}
