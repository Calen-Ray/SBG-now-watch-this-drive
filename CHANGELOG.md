# Changelog

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
