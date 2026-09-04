# Release Guide — from this repo to a Play Store listing

What the repo already does for you, what only you can do, and the order to do it in.

> **Status:** the code side is ready — identity, icons and the build script are in.
> The remaining steps all need either a Unity module install or a Google account, so
> they are yours. Nothing here has been run against a real Android SDK yet; the two
> menu items below have been executed headlessly, the AAB script has not.

---

## 1. What the repo already handles

| Thing | Where it comes from |
|---|---|
| Package name, version, versionCode, targetSdk | **Match3 → Setup → Apply Mobile Settings** ([MobileSetupMenu.cs](../Assets/Scripts/Editor/MobileSetupMenu.cs)) |
| Launcher icon + Android adaptive layers + Play feature graphic | **Match3 → Generate → App Icons** ([AppIconGenerator.cs](../Assets/Scripts/Editor/AppIconGenerator.cs)) |
| Portrait lock, IL2CPP + ARM64, safe area, vSync off, ASTC textures | **Match3 → Setup → Apply Mobile Settings** |
| A signed-release AAB | **Match3 → Build → Android AAB** ([AndroidBuild.cs](../Assets/Scripts/Editor/AndroidBuild.cs)) |

Run the two Setup/Generate items once, then commit the `ProjectSettings.asset` and
`Assets/Icons/` changes they produce.

### The identity constants

They live at the top of `MobileSetupMenu`:

```csharp
private const string AndroidApplicationId = "com.thefcan.candymatch";
private const string BundleVersion = "1.0.0";
```

**Change the application id before your first upload.** Play keys every listing,
review and install off it, and it can never be changed for that app afterwards.
The versionCode is derived (`major*10000 + minor*100 + patch`, so 1.0.0 → 10000),
which means bumping `BundleVersion` and re-running the menu item is the entire
release ritual. Play rejects a versionCode it has seen before — including one from
a build you later discarded — so it must only ever go up.

### The productName trap

`productName` is still `unity-match3`, and it is what the launcher prints under the
icon. Renaming it to "Candy Match" is a deliberate step, not a side effect of the
setup menu, because on desktop `productName` also decides `persistentDataPath`:
rename it and every existing local save (`progress.sav`, `meta.sav`) is orphaned
where it stands. On Android the path follows the *application id* instead, so a
first release is free to rename — just do it before there are players, and know
that your own editor saves will look reset afterwards (cloud sync restores the
campaign progress; boosters and the album are device-local and will not come back).

## 2. What only you can do

### 2a. Install Android Build Support

Unity Hub → Installs → 2022.3.x → ⚙ → Add modules → **Android Build Support**,
including **Android SDK & NDK Tools** and **OpenJDK** (~3 GB). Until this is
installed, `Match3 → Generate → App Icons` logs
`Android: no icon slots reported` and writes the PNGs without assigning them —
re-run the menu item once the module is there and the adaptive slots fill in.

### 2b. Create the upload keystore — yourself

```bash
keytool -genkeypair -v -keystore candymatch-upload.keystore \
  -alias candymatch -keyalg RSA -keysize 2048 -validity 10000
```

Then in Unity: **Project Settings → Player → Publishing Settings** → tick *Custom
Keystore*, select the file, and enter the passwords there.

- **Keep the keystore and its passwords out of this repo.** `.gitignore` does not
  cover `*.keystore` by name today — check before you `git add .`.
- Losing it means you can never update the app under the same listing again. Back
  it up somewhere that is not this machine.
- Enrol in **Play App Signing** (the default for new apps) so Google holds the
  actual app signing key and yours is only the *upload* key — that one can be reset
  if it is lost, the app signing key cannot.

### 2c. The $25 developer account

One-off fee, [play.google.com/console](https://play.google.com/console). Everything
below waits on it.

## 3. Building the AAB

```bash
# From the repo root, with the editor CLOSED (Unity locks the project):
/Applications/Unity/Hub/Editor/2022.3.62f3/Unity.app/Contents/MacOS/Unity \
  -batchmode -quit -nographics -projectPath . \
  -executeMethod Match3.EditorTools.AndroidBuild.BuildAab \
  -logFile build.log
```

Output lands in `Build/Android/CandyMatch-<version>-<versionCode>.aab`. The script
fails loudly rather than silently producing a debug-signed bundle when no custom
keystore is configured — Play rejects those, and finding that out after the upload
wastes more time than the check costs.

## 4. Store listing

| Asset | Where |
|---|---|
| App icon, 512×512 PNG | `Assets/Icons/app_icon.png` (generated) |
| Feature graphic, 1024×500 | `Assets/Icons/store_feature_graphic.png` (generated) |
| Phone screenshots (min 2, 16:9–9:16, ≥320px) | `docs/screenshots/*.png` — 860×1600, already the right shape |
| Short description (80 chars) | see below |
| Full description (4000 chars) | the README's opening section is a good starting point |

Short description draft:

> A 120-level candy match-3 with jelly, chocolate, hatching eggs and no lives.

## 5. Data safety and the privacy policy

Play requires **both** a privacy policy URL and a completed Data safety form before
a release goes live, and the answers are dictated by what the app actually does:

- **Unity Gaming Services anonymous sign-in** runs at boot
  ([CloudSync.cs](../Assets/Scripts/Cloud/CloudSync.cs)) and mints a persistent
  player id. On the Data safety form that is *App activity → other user-generated
  content* plus a **device or other ID**, collected and transmitted off-device.
- **Cloud Save** stores your level/star progress against that id.
- **Leaderboards** transmit a score and a display name for the time-attack mode.
- No name, e-mail, contacts, location, photos or advertising id are touched
  anywhere in the codebase.

Two honest notes before you tick "no data collected", because it would be wrong:

1. The anonymous sign-in happens with **no consent prompt and no opt-out** today.
   That is defensible for a pseudonymous game id in most jurisdictions but it is a
   product decision you are making, not a technicality. A first-run "sync progress
   to the cloud?" toggle wired to `Prefs` would remove the question entirely.
2. If UGS is not configured, none of this happens at all — the game is fully
   local-first (see [UGS-SETUP.md](UGS-SETUP.md)). Shipping without cloud sync is a
   legitimate way to make the whole section moot for a first release.

## 6. Before you hit publish

- [ ] `Apply Mobile Settings` and `App Icons` re-run **with the Android module
      installed**, and the resulting `ProjectSettings.asset` committed
- [ ] Application id changed from the placeholder, if you want a different one
- [ ] `productName` decided (see the trap above)
- [ ] Keystore created, backed up, and **not** committed
- [ ] AAB built and installed on a real device — touch targets, safe area, back
      button, haptics, and the frame rate on a mid-range phone
- [ ] Privacy policy URL live
- [ ] Data safety form answered per section 5
- [ ] Internal testing track first; production after that
