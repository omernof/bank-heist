<p align="center"><img src="docs/banner.svg" alt="Bank Heist" width="100%"></p>

<p align="center">
  <img src="https://img.shields.io/badge/Unity-6000.5.7f1-black?logo=unity&logoColor=white" alt="Unity">
  <img src="https://img.shields.io/badge/Render%20pipeline-URP-blue" alt="URP">
  <img src="https://img.shields.io/badge/Language-C%23-512bd4?logo=csharp&logoColor=white" alt="C#">
  <img src="https://img.shields.io/badge/Genre-Action%20%2F%20Heist-b91c1c" alt="Genre">
</p>

<p align="center"><b>Pick a side, plan the job, and survive the city.</b><br>
A GTA-style 3D heist game across three connected scenes: the city streets, the bank and the vault.</p>

---

## 🎬 Gameplay video

<p align="center">
  <a href="https://github.com/omernof/bank-heist/releases/download/v1.0/BankHeist-Gameplay.mp4">
    <img src="docs/video-thumbnail.jpg" alt="Watch the gameplay video" width="760">
  </a>
  <br>
  <a href="https://github.com/omernof/bank-heist/releases/download/v1.0/BankHeist-Gameplay.mp4"><b>▶ Watch the full run</b></a> · 2:58 · <a href="https://github.com/omernof/bank-heist/releases/tag/v1.0">release page</a>
</p>

---

## 📸 Screenshots

<table>
  <tr>
    <td width="50%"><img src="docs/screenshots/menu.jpg" alt="Main menu"><br><sub><b>Main menu</b> – choose Robber, Police or Bystander</sub></td>
    <td width="50%"><img src="docs/screenshots/street.jpg" alt="City street"><br><sub><b>The city</b> – heading to the bank with the crew</sub></td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/bank-entrance.jpg" alt="Bank entrance"><br><sub><b>The bank</b> – going in through the front door</sub></td>
    <td><img src="docs/screenshots/vault-door.jpg" alt="Vault door"><br><sub><b>The vault door</b> – burning it open while the squad covers</sub></td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/vault.jpg" alt="Inside the vault"><br><sub><b>Inside the vault</b> – emptying the cash racks as SWAT is dispatched</sub></td>
    <td><img src="docs/screenshots/getaway.jpg" alt="Getaway"><br><sub><b>The getaway</b> – out to the van with the police arriving</sub></td>
  </tr>
  <tr>
    <td><img src="docs/screenshots/police-chase.jpg" alt="Police chase"><br><sub><b>Police chase</b> – five wanted stars on the way to the safehouse</sub></td>
    <td><img src="docs/screenshots/mission-passed.jpg" alt="Mission passed"><br><sub><b>Mission passed</b> – got away with the money</sub></td>
  </tr>
</table>

## 🎮 Gameplay

- **Three sides to play** – a **Robber** leading the crew, a **Cop** leading the response squad, or a **Bystander** caught in the middle
- **Three connected scenes** – City → Bank → Vault, with HP and loot carried between them
- **Commandable AI squad** – order your crew to follow, attack, throw grenades, retreat or hold (`1`–`5`, or the `Tab` command wheel); squad members keep their distance, take hits and can be killed
- **Reactive bank customers** – a queue of NPCs that lie down when the robbers break in
- **Drivable getaway vehicles** with an escalating police response: cruisers, a SWAT truck and a helicopter
- **GTA-style HUD** – wanted stars, cash counter, ammo, minimap, objectives and win / lose screens

## ⌨️ Controls

| Action | Key |
|---|---|
| Move / sprint | `W` `A` `S` `D` / `Left Shift` |
| Aim / shoot | Right mouse / Left mouse |
| Reload / grenade | `R` / `G` |
| Interact (hold for doors, vault, cash) | `E` |
| Enter / exit vehicle | `F` |
| Drive / handbrake | `W` `A` `S` `D` / `Space` |
| Lie down | `C` |
| Squad orders | `1` Follow · `2` Attack · `3` Grenade · `4` Retreat · `5` Hold |
| Command wheel | hold `Tab` |
| Free / lock the cursor | `Esc` |

## 🚀 Getting started

1. Install Unity **6000.5.7f1** with Universal 3D (URP) support.
2. Clone this repository and open the folder in Unity Hub.
3. Restore the large assets listed [below](#-not-included).
4. Choose **Tools ▸ Bank Heist ▸ Build All** to generate the scenes.
5. Press Play, or use the **Test:** menu items to jump straight into a scene.

## 🗂️ Project layout

| Path | Contents |
|---|---|
| `Assets/Heist/Scripts` | Game code: player, squad, AI, health, weapons, vehicles, HUD, scene flow |
| `Assets/Heist/Editor` | Editor tools that build the City, Bank and Vault scenes and the prefabs |
| `Assets/Heist/Props`, `Vehicles`, `Weapons` | 3D models and textures |
| `Assets/Mixamo` | Characters and animations |
| `_ref` | Reference scripts from earlier homework |

## 📦 Not included

To keep the repository small, the following are left out:

- `Library/`, `Logs/`, `Temp/`, `UserSettings/` – Unity regenerates them when the project opens
- Raw screen recordings – the gameplay video is on the [v1.0 release](https://github.com/omernof/bank-heist/releases/tag/v1.0)
- Asset files larger than 20 MB – some Mixamo characters/animations plus a few vehicle and tree models. Download them again into the same folders under `Assets/` ([Mixamo](https://www.mixamo.com)).

## 🙏 Credits

Vehicle and prop credits are in [`Assets/Heist/Vehicles/CREDITS.txt`](Assets/Heist/Vehicles/CREDITS.txt). Fonts: Bebas Neue, Barlow Condensed, Anton (OFL). Characters and animations: [Mixamo](https://www.mixamo.com).
