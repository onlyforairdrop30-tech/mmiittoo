# Asset licensing record

All shipped assets must appear in this table. "Generated" means produced by code in this repository
and therefore owned by the project.

| Asset | Source | License | Notes |
| --- | --- | --- | --- |
| Character, enemies, boss, environment meshes | `Assets/Editor/PrototypeSceneBuilder.cs` (Unity primitives assembled at build time) | Project-owned | Replace with Blender originals before release |
| `LegacyRuntime.ttf` (UI font) | Unity built-in | Unity Personal/Pro EULA | Placeholder only — license a UI font (e.g. SIL OFL) before release |
| Procedural SFX (hit, swing, level-up) | `Assets/Scripts/Audio/ProceduralSfx.cs` (runtime synthesis) | Project-owned | No sampled audio |
| URP / Input System / Cinemachine / Addressables / AI Navigation | Unity Package Registry | Unity Companion License | See `Packages/manifest.json` |
| `ws` (backend WebSocket server) | npm | MIT | Dev/reference server only |

## Rules

1. No asset from an anime, game, or mod is ever copied in — names, silhouettes, music included.
2. Before release, every row must have a license file or invoice on record under `docs/licenses/`.
3. Placeholder assets are marked "Placeholder only" and are tracked in `docs/ROADMAP.md` phase 12.
4. Contributors must add a row in the same commit that adds the asset.
