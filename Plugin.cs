using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using FMOD;
using FMODUnity;
using UnityEngine;

namespace NowWatchThisDrive
{
    [BepInPlugin(ModGuid, ModName, ModVersion)]
    public class Plugin : BaseUnityPlugin
    {
        public const string ModGuid = "sbg.nowwatchthisdrive";
        public const string ModName = "NowWatchThisDrive";
        public const string ModVersion = "0.4.0";

        private const string AudioFileName = "NowWatchThisDrive.wav";

        // 3D rolloff for hearing another player's perfect shot. Tighter than the previous
        // 1.5/1000 range so distance cues are actually audible — wide ranges with linear rolloff
        // collapse to L/R panning only because the volume curve barely moves with distance.
        private const float Audio3DMinDistance = 8f;
        private const float Audio3DMaxDistance = 60f;
        private const float AudioHeightOffset = 0.9f;

        // The SwingNiceShot VFX is RPC'd to every client including the swinger. The swinger's
        // own machine also gets the announcer hook (which plays the 2D clip), so the VFX hook
        // must skip when the hit position is close to the local player to avoid double-playing.
        private const float LocalSwingProximitySquared = 6f * 6f;

        // Perfect-shot VFX can fire multiple times within one swing on multi-hit collisions.
        private const float DuplicateWindowSeconds = 0.25f;
        private const float DuplicateDistanceSquared = 0.25f;

        internal static ManualLogSource Log;

        private static FMOD.Sound _sound2D;
        private static FMOD.Sound _sound3D;
        private static bool _soundReady;
        private static float _last3DPlayTime;
        private static Vector3 _last3DPlayPosition;

        private void Awake()
        {
            Log = Logger;
            new Harmony(ModGuid).PatchAll();
            Log.LogInfo($"{ModName} v{ModVersion} loaded (sound loads in Start).");
        }

        // Unity audio is disabled project-wide so all playback goes through FMOD. Two Sound
        // handles share the same file: one 2D for the swinger's own clip (no attenuation, full
        // quality) and one 3D for hearing other players' shots positionally.
        private void Start()
        {
            try
            {
                string pluginDir = Path.GetDirectoryName(Info.Location);
                string audioPath = Path.Combine(pluginDir, AudioFileName);
                if (!File.Exists(audioPath))
                {
                    Log.LogError($"Audio file missing: {audioPath}");
                    return;
                }

                var sys = RuntimeManager.CoreSystem;

                var r2 = sys.createSound(audioPath, MODE.CREATESAMPLE | MODE.DEFAULT, out _sound2D);
                if (r2 != RESULT.OK)
                {
                    Log.LogError($"FMOD createSound (2D) failed: {r2}");
                    return;
                }

                var r3 = sys.createSound(audioPath,
                    MODE.CREATESAMPLE | MODE._3D | MODE._3D_LINEARROLLOFF,
                    out _sound3D);
                if (r3 != RESULT.OK)
                {
                    Log.LogError($"FMOD createSound (3D) failed: {r3}");
                    return;
                }
                _sound3D.set3DMinMaxDistance(Audio3DMinDistance, Audio3DMaxDistance);

                _sound2D.getLength(out uint lenMs, TIMEUNIT.MS);
                _soundReady = true;
                Log.LogInfo($"FMOD sounds loaded ({lenMs} ms): {audioPath}");
            }
            catch (Exception ex)
            {
                Log.LogError($"Start failed: {ex}");
            }
        }

        private static void PlayLocal2D()
        {
            if (!_soundReady) return;

            try
            {
                var playResult = RuntimeManager.CoreSystem.playSound(
                    _sound2D, default(ChannelGroup), false, out Channel _);
                if (playResult != RESULT.OK)
                {
                    Log?.LogWarning($"playSound 2D: {playResult}");
                }
            }
            catch (Exception ex)
            {
                Log?.LogError($"PlayLocal2D failed: {ex}");
            }
        }

        private static void PlayRemote3D(Vector3 worldPosition)
        {
            if (!_soundReady) return;

            if (Time.unscaledTime - _last3DPlayTime < DuplicateWindowSeconds &&
                (worldPosition - _last3DPlayPosition).sqrMagnitude < DuplicateDistanceSquared)
            {
                return;
            }

            try
            {
                var sys = RuntimeManager.CoreSystem;
                var playResult = sys.playSound(_sound3D, default(ChannelGroup), true, out Channel channel);
                if (playResult != RESULT.OK)
                {
                    Log?.LogWarning($"playSound 3D: {playResult}");
                    return;
                }

                channel.setMode(MODE._3D | MODE._3D_LINEARROLLOFF);
                channel.set3DMinMaxDistance(Audio3DMinDistance, Audio3DMaxDistance);

                var pos = worldPosition.ToFMODVector();
                var vel = Vector3.zero.ToFMODVector();
                channel.set3DAttributes(ref pos, ref vel);
                channel.setPaused(false);

                _last3DPlayTime = Time.unscaledTime;
                _last3DPlayPosition = worldPosition;
            }
            catch (Exception ex)
            {
                Log?.LogError($"PlayRemote3D failed: {ex}");
            }
        }

        private static bool IsLocalPlayerSwingPosition(Vector3 position)
        {
            var localGolfer = GameManager.LocalPlayerAsGolfer;
            if (localGolfer == null) return false;
            return (localGolfer.transform.position - position).sqrMagnitude <= LocalSwingProximitySquared;
        }

        // Only fires on the swinger's own machine. We swap the vanilla NiceShot announcer FMOD
        // event for our 2D clip and return false to suppress vanilla.
        [HarmonyPatch(typeof(CourseManager), nameof(CourseManager.PlayAnnouncerLineLocalOnly))]
        internal static class Patch_CourseManager_PlayAnnouncerLineLocalOnly
        {
            private static bool Prefix(AnnouncerLine line)
            {
                if (line != AnnouncerLine.NiceShot || !_soundReady)
                {
                    return true;
                }

                PlayLocal2D();
                return false;
            }
        }

        // SwingNiceShot VFX is replicated to every client. On the swinger's own machine the
        // announcer hook already played the 2D clip — skip here via the proximity check.
        // Everywhere else this is the audible signal that another player perfect-shotted.
        [HarmonyPatch(typeof(VfxManager), "PlayPooledVfxLocalOnlyInternal",
            new[] { typeof(VfxType), typeof(Vector3), typeof(Quaternion), typeof(Vector3), typeof(uint), typeof(bool), typeof(float), typeof(Action<PoolableParticleSystem>) })]
        internal static class Patch_VfxManager_PlayPooledVfxLocalOnlyInternal
        {
            private static void Prefix(VfxType vfxType, Vector3 position)
            {
                if (vfxType != VfxType.SwingNiceShot || !_soundReady) return;
                if (IsLocalPlayerSwingPosition(position)) return;

                PlayRemote3D(position + Vector3.up * AudioHeightOffset);
            }
        }
    }
}
