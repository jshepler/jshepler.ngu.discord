using System.Collections;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace jshepler.ngu.discord
{
    [HarmonyPatch]
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony _harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        private const long CLIENT_ID = 1261635031391273000L;

        private static Character _character;
        private static Discord.Discord _discord;
        private static bool _discordIsRunning = false;

        private void Awake()
        {
            _harmony.PatchAll();
            Logger.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
        }

        private void Update()
        {
            _discordIsRunning = IsDiscordRunning();
        }

        private void OnApplicationQuit()
        {
            if (_discord != null)
                _discord.Dispose();
        }

        [HarmonyPostfix, HarmonyPatch(typeof(Character), "Start")]
        private static void Character_Start_postfix(Character __instance)
        {
            _character = __instance;
            _character.StartCoroutine(UpdateActivity());
        }

        private static IEnumerator UpdateActivity()
        {
            var delay = new WaitForSeconds(4f);

            while (true)
            {
                if (_discordIsRunning)
                    yield return UpdateActivity(BuildActivity());

                yield return delay;
            }
        }

        private static IEnumerator UpdateActivity(Discord.Activity activity)
        {
            var calledBack = false;

            _discord.GetActivityManager().UpdateActivity(activity, res => calledBack = true);
            yield return new WaitUntil(() => calledBack);
        }

        private static bool IsDiscordRunning()
        {
            if (_discord == null)
            {
                try
                {
                    _discord = new Discord.Discord(CLIENT_ID, (ulong)Discord.CreateFlags.NoRequireDiscord);
                }

                catch // eat the exception and assume client is no longer running
                {
                    return false;
                }
            }

            try
            {
                _discord.RunCallbacks();
            }

            catch // eat the exception and assume client is no longer running
            {
                _discord = null;
                return false;
            }

            return true;
        }

        private static Discord.Activity BuildActivity()
        {
            var state = $"Zone: {_character.adventureController.zoneName(_character.adventure.zone)}";
            if (_character.adventure.zone == 1000)
                state += $" (floor {_character.adventureController.itopodLevel})";

            if (_character.challenges.inChallenge)
                state = $"Challenge: {GetCurrentChallenge()}";

            var diff = _character.settings.rebirthDifficulty switch
            {
                difficulty.normal => "NORMAL",
                difficulty.evil => "EVIL",
                difficulty.sadistic => "SAD",
                _ => "??"
            };

            var activity = new Discord.Activity
            {
                Details = $"{diff} | {GetMaxTitan()}",
                State = state,
                Assets = { LargeImage = "ngu_logo", LargeText = "NGU IDLE" }
            };

            activity.Timestamps.Start = 0;
            activity.Timestamps.End = 0;
            activity.Party.Size.CurrentSize = 0;
            activity.Party.Size.MaxSize = 0;

            return activity;
        }

        private static string GetCurrentChallenge()
        {
            var c = _character.challenges;
            var ac = _character.allChallenges;

            if (c.basicChallenge.inChallenge)
                return $"BASIC {ac.basicChallenge.currentCompletions() + 1}/{ac.basicChallenge.maxCompletions}";

            if (c.noAugsChallenge.inChallenge)
                return $"NOAUGS {ac.noAugsChallenge.currentCompletions() + 1}/{ac.noAugsChallenge.maxCompletions}";

            if (c.hour24Challenge.inChallenge)
                return $"24HR {ac.hour24Challenge.currentCompletions() + 1}/{ac.hour24Challenge.maxCompletions}";

            if (c.levelChallenge10k.inChallenge)
                return $"100L {ac.level100Challenge.currentCompletions() + 1}/{ac.level100Challenge.maxCompletions}";

            if (c.noEquipmentChallenge.inChallenge)
                return $"NOEQ {ac.noEquipmentChallenge.currentCompletions() + 1}/{ac.noEquipmentChallenge.maxCompletions}";

            if (c.noRebirthChallenge.inChallenge)
                return $"NORB {ac.noRebirthChallenge.currentCompletions() + 1}/{ac.noRebirthChallenge.maxCompletions}";

            if (c.trollChallenge.inChallenge)
                return $"TROLL {ac.trollChallenge.currentCompletions() + 1}/{ac.trollChallenge.maxCompletions}";

            if (c.laserSwordChallenge.inChallenge)
                return $"LSC {ac.laserSwordChallenge.currentCompletions() + 1}/{ac.laserSwordChallenge.maxCompletions}";

            if (c.nguChallenge.inChallenge)
                return $"NONGU {ac.NGUChallenge.currentCompletions() + 1}/{ac.NGUChallenge.maxCompletions}";

            if (c.timeMachineChallenge.inChallenge)
                return $"NOTM {ac.timeMachineChallenge.currentCompletions() + 1}/{ac.timeMachineChallenge.maxCompletions}";

            if (c.blindChallenge.inChallenge)
                return $"BLIND {ac.blindChallenge.currentCompletions() + 1}/{ac.blindChallenge.maxCompletions}";

            return "??";
        }

        private static string GetMaxTitan()
        {
            var enemies = _character.bestiary.enemies;
            if (enemies[302].kills == 0)
                return "T0";

            var data = _akData.First(d => enemies[(int)d.Value[0]].kills > 0);
            var titan = data.Key;

            var hasPower = data.Value[1] > 0 && _character.totalAdvAttack() >= data.Value[1];
            var hasToughness = data.Value[2] > 0 && _character.totalAdvDefense() >= data.Value[2];
            var hasRegen = data.Value[3] > 0 && _character.totalAdvHPRegen() >= data.Value[3];
            var hasKills = data.Value[4] > 0 && enemies[(int)data.Value[0]].kills >= data.Value[4];
            if (hasKills || (hasPower && hasToughness && hasRegen))
                titan += " AK";

            return titan;
        }

        // "name", [bossId, P, T, regen, optional kill count]
        private static Dictionary<string, float[]> _akData = new()
        {
            { "T14", [378, 0, 0, 0, 0] },
            { "T13", [377, 0, 0, 0, 0] },
            { "T12v4", [376, 7.2E+34f, 2.4E+34f, 4.8E+32f, 5] },
            { "T12v3", [375, 3.6E+34f, 1.2E+34f, 2.4E+32f, 5] },
            { "T12v2", [374, 1.2E+34f, 4E+33f, 8E+31f, 5] },
            { "T12v1", [373, 3E+33f, 1E+33f, 2E+31f, 5] },
            { "T11v4", [372, 1.1E+33f, 3.6E+32f, 7.5E+30f, 5] },
            { "T11v3", [371, 3.6E+32f, 1.2E+32f, 2.5E+30f, 5] },
            { "T11v2", [370, 9E+31f, 3E+31f, 6E+29f, 5] },
            { "T11v1", [369, 1.8E+31f, 6E+30f, 1.2E+29f, 5] },
            { "T10v4", [378, 1E+31f, 5E+30f, 5E+28f, 5] },
            { "T10v3", [377, 2E+30f, 1E+30f, 9.999999E+27f, 5] },
            { "T10v2", [376, 3.2E+29f, 1.6E+29f, 1.6E+27f, 5] },
            { "T10v1", [375, 4E+28f, 2E+28f, 4E+26f, 5] },
            { "T9v4", [347, 7.5E+26f, 3.7E+26f, 7.5E+24f, 24] },
            { "T9v3", [346, 4E+25f, 2E+25f, 4E+23f, 24] },
            { "T9v2", [345, 2E+24f, 1E+24f, 2E+22f, 24] },
            { "T9v1", [344, 1E+23f, 5E+22f, 1E+21f, 24] },
            { "T8v4", [342, 5E+22f, 2.5E+22f, 5E+20f, 0] },
            { "T8v3", [341, 2E+21f, 1E+21f, 2E+19f, 0] },
            { "T8v2", [340, 1E+20f, 5E+19f, 1E+18f, 0] },
            { "T8v1", [339, 5E+18f, 2.5E+18f, 5E+16f, 0] },
            { "T7v4", [337, 5E+18f, 2.5E+18f, 5E+16f, 0] },
            { "T7v3", [336, 2E+17f, 1E+17f, 2E+15f, 0] },
            { "T7v2", [335, 1E+16f, 5E+15f, 1E+14f, 0] },
            { "T7v1", [334, 5E+14f, 2.5E+14f, 5E+12f, 0] },
            { "T6v4", [315, 2.5E+12f, 1.6E+12f, 2.5e+10f, 0] },
            { "T6v3", [314, 2.5E+11f, 1.6E+11f, 2.5e+09f, 0] },
            { "T6v2", [313, 2.5E+10f, 1.6E+10f, 2.5e+08f, 0] },
            { "T6v1", [312, 2.5E+09f, 1.6E+09f, 2.5e+07f, 0] },
            { "T5", [310, 1.3e+7f, 7.0e+6f, 150000f, 0] },
            { "T5-4", [309, 0, 0, 0, 0] },
            { "T5-3", [308, 0, 0, 0, 0] },
            { "T5-2", [307, 0, 0, 0, 0] },
            { "T5-1", [306, 0, 0, 0, 0] },
            { "T4", [305, 800000f, 400000f, 14000f, 0] },
            { "T3", [304, 25000f, 15000f, 0, 0] },
            { "T2", [303, 9000f, 7000f, 0, 0] },
            { "T1", [302, 3000f, 2500f, 0, 0] }
        };
    }
}
