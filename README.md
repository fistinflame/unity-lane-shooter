# Unity Lane Shooter (Android Hypercasual)

This is a Unity project for a lane-shooter / crowd runner game.

## Prerequisites

- Unity Hub
- Unity Editor `6000.3.10f1` (Unity 6)
- Windows 10/11 (recommended)
- Git (to clone/pull updates)

For Android builds, install these Unity modules for the same editor version:

- Android Build Support
- OpenJDK
- Android SDK & NDK Tools

## Project Path

Repository root (Unity project root):

`D:\GPT\Assets\My project`

## How To Open In Unity Hub

1. Open Unity Hub.
2. Click **Add** (or **Add project from disk**).
3. Select folder: `D:\GPT\Assets\My project`.
4. Make sure Unity Hub uses editor `6000.3.10f1`.
5. Open the project.

## How To Run In Editor

1. Open scene: `Assets/Scenes/Main.unity`.
2. Press **Play**.
3. Controls: drag left/right (mouse in Editor, touch on Android).

## Build For Android

1. In Unity: **File > Build Settings**.
2. Select **Android** and click **Switch Platform**.
3. Ensure `Assets/Scenes/Main.unity` is in **Scenes In Build** (checked).
4. Click **Build** or **Build And Run**.

## Notes

- The project uses primitive/runtime-generated visuals and imported low-poly environment assets.
- If Unity asks to upgrade project settings/packages, allow it and then re-open the project if prompted.
- First import can take a few minutes.

## Troubleshooting

- If scripts fail to compile after editor upgrade, close Unity and re-open the project.
- If Android build fails, re-check Android modules in Unity Hub for the exact editor version.
- If textures appear pink, ensure the project is using the included URP settings in `Assets/Settings`.
