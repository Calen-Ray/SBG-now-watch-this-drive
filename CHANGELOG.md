# Changelog

## v0.4.0
- Swinger now hears their own clip as full-quality 2D audio (no positional attenuation), restoring pre-3D playback feel.
- Other players in a multiplayer lobby still hear the clip as 3D audio positioned at the swinger's hit point.
- Tightened 3D rolloff range (8m–60m linear) so distance cues are audibly distinct rather than collapsing to L/R panning only.

## v0.3.2
- Added verbose diagnostic logging around the `NiceShot` announcer intercept, `SwingNiceShot` VFX hook, and each FMOD playback step to isolate freezes during trigger-time audio execution.

## v0.3.1
- Removed the custom Mirror audio replication messages that could disconnect clients without the mod.
- Drive clip playback now follows the game's existing replicated `SwingNiceShot` VFX event instead.

## v0.3.0
- Replicate perfect-swing clip playback to all modded clients in a match via Mirror.
- Play the clip as 3D FMOD audio from the emitting golfer's position instead of only locally.

## v0.2.1
- New icon / cover art by **MultipleBees**. Original high-res art kept in `cover-art/`.
- Added `.github/workflows/release.yml` so publishing a GitHub Release auto-uploads the zip to Thunderstore.

## v0.2.0
- Updated bundled audio clip.
- Route audio playback through FMOD (Unity's native audio is disabled in the game build).
- Reference assets deploy alongside the DLL in the Thunderstore package layout.

## v0.1.0
- Initial release.
- Intercepts `CourseManager.PlayAnnouncerLineLocalOnly(NiceShot)` and plays the override clip.
