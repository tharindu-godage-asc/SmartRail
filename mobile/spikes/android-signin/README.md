# Android sign-in spike

**Throw-away spike, not product code.** It answers one question: does mobile sign-in work on the Android emulator with the Compose Keycloak? The answer is in [RESULT.md](RESULT.md). Spec: [_specs/android-signin-spike.md](../../../_specs/android-signin-spike.md).

The app has one screen: Sign in, Call whoami, Refresh, Sign out (local). It shows the subject, roles, audience and expiry, never a token. Tokens are kept in memory only.

## Run it

You need Docker, Flutter and an Android emulator (the `Pixel_6` image was used). Development-only credentials come from the realm file.

1. Start the stack from `smartrail-platform/`: `docker compose up -d` (Keycloak on 8180, API on 8080).
2. Start the emulator: `emulator -avd Pixel_6`.
3. Let the emulator reach your PC: `adb reverse tcp:8180 tcp:8180` and `adb reverse tcp:8080 tcp:8080`. Run these again after restarting the emulator.
4. From this folder: `flutter run`.
5. Tap **Sign in**, then sign in as `nimal` with password `nimal-dev-only`. Keep the login short: if Android kills the app while you type, the sign-in is lost (see RESULT.md, finding 5).
6. Tap **Call whoami**, then **Refresh**, then **Call whoami** again.

## Files that matter

- `lib/auth_service.dart`: the sign-in, refresh and whoami calls.
- `android/app/build.gradle.kts`: the redirect scheme `com.smartrail.operator`.
- `android/app/src/main/AndroidManifest.xml`: no `taskAffinity` on `MainActivity` (this is required).
- `android/app/src/debug/`: cleartext HTTP to `localhost`, debug builds only.

Screenshots from the run are in `screenshots/`.
