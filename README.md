# ArcCreate

![Logo](Assets/Textures/Logos/LogoFull.png?raw=true "Title")

Fast and powerful .aff editor made with Unity.

### Development discord server:

This server is strictly for ArcCreate development purposes, you will be screened before joining.  
[Permanent invite link](https://discord.gg/3MAyPwPma5)


# Getting started

### Installation

ArcCreate is available on Windows, MacOS, Linux, Android and iOS.

- Windows, MacOS, Linux (Editor):

  [Download through GitHub releases](https://github.com/Arcthesia/ArcCreate/releases/)

- Android & iOS (Player):

| [<img src="https://play.google.com/intl/en_us/badges/images/generic/en-play-badge.png" height=75px>](https://play.google.com/store/apps/details?id=com.Arcthesia.ArcCreate) | [<img src="https://developer.apple.com/assets/elements/badges/download-on-the-app-store.svg" height=50px>](https://apps.apple.com/us/app/arccreate/id6445904090) |
| - | - |

### Building

This fork is developed and tested with Unity 6000.3.20f1 (Unity 6.3 LTS). You can download the exact version from the [official Download Archive here](https://unity.com/releases/editor/archive).

### 外部スキン（Windows版）

外部スキンの基準フォルダは、`ArcCreateNext.exe`と同じ場所にある`Skin`です。画像は`.jpg`または`.png`を使用でき、両方ある場合は`.jpg`が優先されます。ファイル名は次の名前に合わせ、変更後はアプリを再起動してください。

| 用途 | `Skin`からの相対パス |
| --- | --- |
| Light標準背景 | `DefaultBackgrounds/BaseLight.jpg` |
| Conflict標準背景 | `DefaultBackgrounds/BaseConflict.jpg` |
| Colorless標準背景 | `DefaultBackgrounds/Epilogue.jpg` |
| Touchノート | `Note/Touch/TapNoteLight.png`、`HoldNoteLight.png`、`TapNoteConflict.png`、`HoldNoteConflict.png`、`ArcCap.png`、`ArcTapLight.png`、`ArcTapConflict.png` |
| 通常タップの判定パーティクル | `Particles/TapLight.png`、`TapConflict.png`、`TapColorless.png`、`TapMiraiLight.png`、`TapMiraiConflict.png` |
| SFXノートの判定パーティクル | `Particles/TapSfx.png` |
| アークのパーティクル | `Particles/ArcParticle.png` |
| 判定文字 | `Particles/TextPerfect.png`、`TextGood.png`、`TextMiss.png` |

曲固有の背景は外部スキンではありません。曲フォルダ側の`bg.jpg`などを譜面の背景として指定します。

現在の制限事項：

- `Particles/HoldParticle.png`は既存実装に読み込み呼出しの欠落があるため、配置しても反映されません。
- `Particles/ClassicArcParticle.png`、`Grid.png`、`GridMask.png`は外部スキン読込へ接続されていないため、配置しても反映されません。
- `Build/ArcCreateNext/Skin`以下の個人用素材はGit管理外です。Windowsビルド時にはフォルダ全体を退避・検証・復元します。

# Project status

### Gameplay
- [x] Gameplay rendering
- [x] Gameplay judgement
- [x] Pause menu
- [x] Compiled scenecontrol support
- [x] Level selection menu
- [x] Result screen
- [x] Import level from package file
- [x] Settings menu
- [ ] Support for controller input
- [ ] Custom gauge and partner

### Editor (Desktop only)
- [x] Project metadata management
- [x] Project skin settings
- [x] Note editing
- [x] Timing, camera editing
- [x] Custom hotkeys configuration
- [x] FFmpeg rendering support
- [x] Lua macro support
- [x] Lua scenecontrol editing & compiling
- [x] LAN communication between desktop and mobile

# Contributing

See:
- [CONTRIBUTING](CONTRIBUTING.md) for code contribution
- [TRANSLATING](TRANSLATING.md) for helping with translating the application.

# License

This project was licensed under GPL-3.0 license (see [LICENSE](LICENSE)).

# Credits

- `Assets/Plugins/ColorPicker`: https://github.com/mmaletin/UnityColorPicker
- `Assets/Plugins/DOTween`: http://dotween.demigiant.com
- `Assets/Plugins/DynamicPanels`: https://github.com/yasirkula/UnityDynamicPanels
- `Assets/Plugins/Graphy`: https://github.com/Tayx94/graphy/
- `Assets/Plugins/MaterialIcons`: https://fonts.google.com/icons for the icons themselves and https://github.com/convalise/unity-material-icons/ for packaging them for Unity.
- `Assets/Plugins/NativeFilePicker`: https://github.com/yasirkula/UnityNativeFilePicker
- `Assets/Plugins/StandaloneFileBrowser`: https://github.com/gkngkc/UnityStandaloneFileBrowser
- `Assets/Plugins/UIGradient`: https://github.com/azixMcAze/Unity-UIGradient
- `Assets/Plugins/YamlDotNet`: https://github.com/aaubry/YamlDotNet
- Other files under `Assets/Plugins/` are downloaded from NuGet
- A large portion of files under `Assets/Textures/Gameplay` are taken from, or derived from https://github.com/yojohanshinwataikei/arcade-plus
- Files under `Assets/AudioClips` are from https://github.com/yojohanshinwataikei/Arcade-plus.
- Files under `Assets/Fonts/FontFiles` are free font files taken from https://fonts.google.com
