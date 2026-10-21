
<div align="center">
  <video src="https://github.com/user-attachments/assets/39b950a0-5281-4cfd-a2f8-a045be462b45" controls></video>
</div>


# 🎮 PixelShooter

Hypercasual puzzle shooter built in **Unity 6** (`6000.4.5f1`) with the **Universal Render Pipeline**. Gameplay systems are wired with **Zenject**, async flows run on **UniTask**, and motion uses **DOTween** plus **Dreamteck Splines**. Levels are data-driven `ScriptableObject`s: a shooter grid, a waiting area, a conveyor, and a pixel image baked from a source texture.

## 📌 Overview

The player clears a pixel image by launching colored shooter cubes. Shooters start on a grid in front of the picture. A tap sends a shooter that has a clear path into a limited row of waiting slots. From there it boards a plate and rides a conveyor around the image, firing one shot per matching pixel it can claim from the exposed edge.

The level is won when every opaque pixel is cleared and the conveyor is empty. It is lost when the waiting area jams and no shooter can keep firing. Progress, coins, and audio settings persist as JSON between sessions. Each level is spawned into a single game scene from `LevelData`; the image itself is not a separate scene.

## 🔧 Core Features

- **Image-backed pixel grid.** Source textures under `PixelImages` are downsampled and palette-mapped into a `PixelCellData` grid. At runtime, pooled `PixelCube`s are placed from that bake and tinted with `MaterialPropertyBlock`.
- **Shooter board.** Colored shooters, question shooters, and tunnels occupy a cell grid. Clickability is resolved with a breadth-first search toward the board exit, so blocked shooters stay locked until a path opens.
- **Tunnels.** A tunnel cell holds a queue of shooters and releases the next one when its cell is free.
- **Waiting slots.** Each level defines five, six, or seven slots, including locked slots. Shooters park here before they can enter the conveyor.
- **Conveyor and plates.** Dreamteck spline followers carry shooters around the image. A plate queue boards each shooter; the belt material scrolls through a `MaterialPropertyBlock`.
- **Edge claiming and projectiles.** While a shooter orbits, it claims the front-most pixel of its palette and fires a pooled projectile. Claimed cubes play a short pop and return to the pool.
- **Win fast-forward.** When remaining ammo covers the remaining pixels, input is taken over, parked shooters are dispatched automatically, and time scale ramps up to 4x.
- **Object pooling.** Bullets, pixel cubes, one-shot audio voices, and travel splines come from a prewarmed `ObjectPool`.
- **Level select.** The main menu scrolls a recycled window of level buttons (locked, current, completed) driven by the highest unlocked level.
- **Responsive camera.** `GridCameraFitter` frames the play area against a tall reference resolution.
- **Persistence and settings.** `SaveManager` writes `player_data.json` with a temp file and a backup. Sound is applied through an `AudioMixer`; vibration is stored alongside it.

## 🧩 Gameplay Flow

1. **Boot.** `InitializeScene` runs `InitBootstrap`, which loads `MainMenuScene`.
2. **Menu.** The player scrolls the level list, picks an unlocked level, and starts the game. Settings (sound and vibration) can be opened from the menu.
3. **Build.** `GameScene` loads. `RuntimeLevel` reads the selected `LevelData` and builds cells, shooters, tunnels, slots, plates, the environment frame, and the pixel image.
4. **Dispatch.** The player taps a shooter that has a path to the exit. It leaves its cell, which can unlock neighbors and the next shooter inside a tunnel.
5. **Queue.** The shooter sits in a free waiting slot until a plate and a conveyor gap are available.
6. **Fire.** On the belt, the shooter claims matching edge pixels and spends one shot per hit. An empty shooter leaves the belt.
7. **Win.** When no opaque pixels remain and the conveyor is clear, the completed screen is shown and the next level is unlocked.
8. **Fail.** If the waiting area is full and shooters can no longer make progress, gameplay stops and the fail screen appears after a short delay. The player can retry or return home.

## 🏗️ Architecture

Dependency injection is Zenject. There is no singleton service locator. Scene flow uses three scenes; level content is constructed in place from data.

| Installer | Scope | What it binds |
| --- | --- | --- |
| `ProjectInstaller` | Project context | `SceneChanger`, `ObjectPool`, `LevelDatabase`, `SaveManager`, `SettingsManager`, `LevelManager`, hand cursor, 60 FPS cap |
| `InitSceneInstaller` | `InitializeScene` | `InitBootstrap` |
| `MainMenuSceneInstaller` | `MainMenuScene` | Menu `UIManager` |
| `GameSceneInstaller` | `GameScene` | Runtime managers, Zenject factories, HUD pages, `GameAudio`, `WinFastForward` |

`GameSceneInstaller` exposes factories for `Cell`, `Shooter`, `Slot`, `Tunnel`, and `Plate`. `RuntimeLevel` (`IInitializable`) is the only place that constructs a level: it calls `Construct` on the grid, slot, shooter, tunnel, plate, and image systems in order.

**ScriptableObjects**

- `LevelData` — shooter grid, tunnels, question shooters, slot layout, shot plan, and baked pixel grid. Edited with an Odin `TableMatrix`.
- `LevelDatabase` — ordered level list. `Assets/_Project/Data/LevelDatabase.asset`.
- `ProjectSettings` — palette and hand-cursor options. `Assets/Resources/Settings/ProjectSettings.asset`.
- `GameAudioCatalog` — pooled sound cues.

**Runtime managers**

- `LevelManager` — current level index, lock state, and completion.
- `CellManager` / `ShooterManager` / `TunnelManager` — board layout and occupancy.
- `SlotManager` — waiting row and the stuck-area fail signal.
- `PlateQueueManager` / `ConveyorManager` — boarding and spline travel.
- `ImageManager` / `ImageGridRenderer` — palette claims and pooled cube visuals.
- `LevelFlowController` — win and fail presentation.
- `UIManager` — game, fail, completed, and settings pages.
- `SaveManager` / `SettingsManager` — disk persistence and mixer state.

Async delays, conveyor laps, cube pops, and the fail-screen pause use `UniTask` with cancellation tokens. Runtime tints on shooters, projectiles, pixel cubes, and the conveyor never touch `renderer.material`.

## 📁 Project Structure (Simplified)

```text
PixelShooter/
├── Assets/
│   ├── _Project/
│   │   ├── Audios/                  # Clips and GameAudioCatalog
│   │   ├── Data/
│   │   │   ├── LevelDatabase.asset
│   │   │   └── Levels/              # Level_1 … Level_7
│   │   ├── Fonts/
│   │   ├── Materials/
│   │   ├── Models/                  # Shooter, pixel cube, plate, tunnel
│   │   ├── PixelImages/             # Source textures baked into levels
│   │   ├── Prefabs/
│   │   │   ├── Game/                # Shooter, Cell, Slot, Tunnel, Plate, PixelCube, Bullet
│   │   │   ├── UI/                  # Home, game, fail, completed, settings
│   │   │   └── Pool.prefab
│   │   ├── Scenes/
│   │   │   ├── InitializeScene.unity
│   │   │   ├── MainMenuScene.unity
│   │   │   └── GameScene.unity
│   │   ├── Scripts/
│   │   │   ├── Core/                # Boot, scene names, init installer
│   │   │   ├── Data/                # Pixel bake, shooter and tunnel data
│   │   │   ├── Editor/              # Level inspector and save-data menu
│   │   │   ├── Game/                # Win/fail flow and fast-forward
│   │   │   ├── LevelData/           # LevelData, database, shot plan
│   │   │   ├── Managers/
│   │   │   ├── Pooling/
│   │   │   ├── Save/
│   │   │   ├── Settings/
│   │   │   └── UI/
│   │   ├── Shaders/                 # URP toon and instanced image shaders
│   │   ├── Sprites/
│   │   └── Textures/
│   ├── Plugins/
│   │   ├── Zenject/
│   │   ├── Demigiant/               # DOTween and DOTween Pro
│   │   ├── Dreamteck/Splines/
│   │   ├── Sirenix/Odin Inspector/
│   │   └── Toony Colors Pro 2/
│   └── Resources/Settings/          # ProjectSettings
├── Packages/                        # URP, UniTask, uGUI
└── ProjectSettings/
```

## 🛠️ Tech Stack

| Technology | Role in this project |
| --- | --- |
| Unity `6000.4.5f1` | Editor and player |
| Universal Render Pipeline `17.4.0` | Rendering |
| Zenject (Extenject) | Installers, injection, prefab factories |
| UniTask | Delays, conveyor travel, pop animations |
| DOTween / DOTween Pro | Shooter, tunnel, plate, and UI tweens |
| Dreamteck Splines | Conveyor path and `SplineFollower` motion |
| Odin Inspector | `LevelData` grid editor and inspector groups |
| TextMeshPro | Ammo counts, level labels, tunnel counts |
| Toony Colors Pro 2 | Toon materials and FBX import processing |
| uGUI | Menu, HUD, popups |
| `JsonUtility` | `player_data.json` save with backup |
| `MaterialPropertyBlock` | Per-instance colors without material clones |

## 📁 Note:

1. Open this folder in **Unity `6000.4.5f1`**. Package resolution pulls **URP 17.4.0** and **UniTask**. Zenject, DOTween, Dreamteck Splines, Odin Inspector, and Toony Colors Pro are already under `Assets/Plugins`.
2. Build order is `InitializeScene` → `MainMenuScene` → `GameScene`. Enter Play Mode from **`Assets/_Project/Scenes/InitializeScene.unity`**. That scene loads the main menu; the menu loads `GameScene`.
3. Level order lives in **`Assets/_Project/Data/LevelDatabase.asset`**. Individual levels are **`Assets/_Project/Data/Levels/Level_1.asset`** through **`Level_7.asset`**. Create new assets with **Create → PixelShooter → LevelData**.
4. Source pictures used by the pixel bake are in **`Assets/_Project/PixelImages`**. Shared palette and cursor options are in **`Assets/Resources/Settings/ProjectSettings.asset`**.
5. Progress is stored as `player_data.json` under `Application.persistentDataPath`. In the Editor, use **PixelShooter → Open Persistent Data Path** and **PixelShooter → Clear Persistent Data** to inspect or reset it.
