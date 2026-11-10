# Glossy Horizons

A 3D sandbox game that takes players through three zones, each dedicated to an aesthetic of the Frutiger family: **Frutiger Aqua**, **Dark Aero** and **Frutiger Evil**. Every zone includes a short minigame and an in-game encyclopedia that explains where the aesthetic comes from and what defines it.

> Student project built by a team of three. First delivery: November 15, 2026. Final release: December 10, 2026.

## About the Game

The player controls a floating glass character made of separate spheres (head, body, hands and feet), available in a male or female version with basic customization. The journey moves from light, to night, to something unsettling.

### Requirements

- **Unity 6000.6.6f1** (the exact version, installed through Unity Hub)
- [Git](https://git-scm.com/) and [Git LFS](https://git-lfs.com/)

### Setup

```bash
git lfs install
git clone https://github.com/valu420/glossy-horizons.git
```

1. Open **Unity Hub** and choose *Add → Add project from disk*.
2. Select the cloned `glossy-horizons` folder.
3. Open it with Unity `6000.6.6f1`. The first launch takes a few minutes while Unity rebuilds the `Library` folder.

## Project Structure
Assets/
├── _Project/
│ ├── Art/
│ │ ├── Models/
│ │ ├── Textures/
│ │ ├── Materials/
│ │ └── Animations/
│ ├── Prefabs/
│ ├── Scenes/
│ ├── Scripts/
│ ├── Audio/
│ └── UI/
└── ThirdParty/

## Team Workflow

- Nobody works directly on `main`. Use one branch per feature, for example `feature/aqua-minigame`.
- Open a **Pull Request** and get a review from a teammate before merging.
- Run `git pull origin main` before every work session.
- **One scene, one person at a time.** Scene and prefab merge conflicts are painful, so announce when you are editing a scene.
- Never add or delete `.meta` files separately from their assets.
- Commit small and commit often.

### Roles

| Role | Responsibilities |
|---|---|
| **Programming / Gameplay** | Controller, procedural animation, hub and portals, minigame base script, saving, menus, final build |
| **3D Art / Character** | Character and accessories in Blender, glass material, customization, Frutiger Aqua zone |
| **Levels / UI / Content** | Research and text for the three aesthetics, information panels, Dark Aero and Frutiger Evil zones, audio |

## License

To be defined before the first public release.

## Team

- **Name 1:** Programming / Gameplay
- **Name 2:** 3D Art / Character
- **Name 3:** Levels / UI / Content
