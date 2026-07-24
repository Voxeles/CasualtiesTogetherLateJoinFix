using BepInEx;
using BepInEx.Logging;
using KrokoshaCasualtiesMP;
using KrokoshaCasualtiesUtils;
using UnityEngine;

namespace CasualtiesTogetherLateJoinFix;

[BepInPlugin(ModGuid, ModName, ModVersion)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "cump.latejoin.maybefix";
    public const string ModName = "CasualtiesTogetherLateJoinFix";
    public const string ModVersion = "0.0.1";

    internal static new ManualLogSource Logger;
    
    private float _timer = 0;
    private int _attempts = 0;
    
    private void Awake()
    {
        Logger = base.Logger;
        WorldgenPatches.OnWorldgenFinish += OnWorldgenFinish;
        NetPlayer.OnPlayerJoined += OnPlayerJoined;
        Logger.LogInfo($"Plugin {ModName} is loaded!");
    }

    private void OnDestroy()
    {
        WorldgenPatches.OnWorldgenFinish -= OnWorldgenFinish;
        NetPlayer.OnPlayerJoined -= OnPlayerJoined;
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
        if (!Net.running || Net.is_host || !Util.IsWorldGenerated())
            return;
        
        if (NetPlayer.ClientIdToPlayerDict.Count == NetPlayer.BodyToPlayerDict.Count)
            return;
        
        _timer += Time.deltaTime;
        if (_timer < 10.0f || _attempts >= 10)
            return;
        
        Logger.LogWarning("Attempting to fix player-body desync!");
        _attempts += 1;
            
        var bodies = FindObjectsByType<Body>(FindObjectsSortMode.None);
        var netBodies = FindObjectsByType<NetBody>(FindObjectsSortMode.None);
        Logger.LogInfo($"Players: {NetPlayer.ClientIdToPlayerDict.Count}, bodies: {bodies.Length}, netBodies: {netBodies.Length}.");
        if (bodies.Length != netBodies.Length)
        {
            Logger.LogWarning($"Bodies ({bodies.Length}) and netBodies ({netBodies.Length}) doesn't match!");
            return;
        }
        foreach (var body in netBodies)
        {
            if (!body.player)
            {
                Logger.LogWarning($"{body.name} doesn't have a player!");
                continue;
            }
            if (NetPlayer.BodyToPlayerDict.ContainsKey(body.body))
                continue;
            NetPlayer.BodyToPlayerDict.Add(body.body, body.player);
            body.player.body = body.body;
            Logger.LogInfo($"Fixed player {body.player.playername}");
        }

        _timer = 0;
    }
}

