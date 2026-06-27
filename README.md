# Card Factory — Prototype

A 2.5D clone of Voodoo's **Card Factory** core loop, built for the **Voodoo Marketing Developer Test**.
The game is **generated from code** rather than hand-authored in the scene: `GameBootstrap` builds the camera,
lighting, conveyor, sources, bins, dock and UI — at runtime on Play, or baked into the scene via an Editor tool.
**After baking, a few in-scene details (object positions, framing, anchor layout) are fine-tuned by hand in the
Editor** and saved with the scene; the generated world is the starting point, not the final word.

- **Engine:** Unity `6000.3.17f1` · Windows · URP (Universal 3D)
- **Target:** mobile portrait; captured in the Editor at **1080×1920**
- **No external assets/packages** except `com.unity.recorder` (for video capture). All meshes are runtime
  primitives / procedural rounded cubes, all materials are `URP/Lit` created in code, all SFX are procedural.
- **Project source (GitHub):** the submission package contains this README, the two videos and the benchmark —
  **the Unity project itself is delivered via GitHub**, not bundled in the ZIP. Clone it from
  **[github.com/KaanGezgin/CardFactoryCaseStudy](https://github.com/KaanGezgin/CardFactoryCaseStudy)**
  (`git clone https://github.com/KaanGezgin/CardFactoryCaseStudy.git`). It was kept out of the ZIP on purpose:
  stripping `Library/` from a copied project reverted some of the hand-tuned, baked scene state, so the repo is the
  faithful, source-of-truth copy.

---

## How to run

1. Open the project in Unity **6000.3.17f1**.
2. Open `Assets/_Project/Scenes/Main.unity` (or any scene) and press **Play** — the world builds itself.
   - If you see a pink/magenta material, add **Universal Render Pipeline/Lit** and **Universal Render Pipeline/Unlit**
     to *Project Settings → Graphics → Always Included Shaders*.
3. Play in the **Game view at a 1080×1920 (portrait)** aspect for the intended framing.

> **If the scene doesn't look like the final video** (object framing/positions, belt color, post-processing tint),
> it's the **baked-world state**: the hand-tuned look lives in the saved scene, and a fresh import can occasionally
> drop it. Fix it in Edit mode with **Tools → Card Factory → Bake World Into Scene** (then **Ctrl+S**), or, to keep
> your manual tweaks, **Tools → Card Factory → Rebake Belt + Color (Keep Rest)**. See *Persisting the world* below.

### Controls
- **Left click / tap** a card stack → sends the top same-color group onto the conveyor.
- **Win/Lose panel** → `NEXT` (next level) / `X` (restart). Each build/restart generates a fresh random level.
- **`A` key** → starts the **marketability ad sequence** (auto-played; see *Marketability* below).
  It begins after a short ~3 s delay (a recording buffer), so nothing happening immediately after the
  key press is expected.

> A custom 3D "hand" pointer replaces the system cursor during play (it follows the mouse and shows taps).

---

## Core gameplay

- **Sources:** 4 stacks at the bottom, each a column of mixed-color cards (12 cards each, **48 total**; every color
  appears 12 times). Tapping a stack sends its **top same-color group** onto the belt.
- **Conveyor:** a U-shaped path. The entry gate shows an **X/20** counter — the number of cards currently on the belt.
  A send that would exceed 20 is **blocked** with a red warning flash.
- **Bins:** **2 active bins**, always different target colors, **capacity 6** each. Matching cards passing along the
  belt slot into a bin (rising fill bar). When a bin is full it ships and is replaced by the next queued color.
- **Dock (overflow tray):** cards that reach the end of the belt without matching fall into the dock (capacity 20).
  Cards in the dock are **grouped by color** (a new card slides in next to its color, pushing others over).
- **Win:** all stacks emptied and no cards left on the belt. **Lose:** the dock fills up → **LEVEL FAILED**.

Levels are generated **randomly but always solvable with zero dock usage** (colors come in blocks of 3–5; bins ship
only at a full 6, so perfect play never needs the dock — good play wins, sloppy play overflows the dock and fails).

---

## Architecture — code-driven world

Everything is created in `GameBootstrap` via `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)]`. The world is split
into two layers:

- **Persistent world** (`CardFactoryWorld`): camera, light, ground, the U-path belt visuals, gate, dock, HUD, and
  empty **anchors** (`StackAnchor_0..3`, `BinAnchor_0..1`) that position the dynamic content.
- **Dynamic content** (`CardFactoryLevel`): the managers, conveyor, cards and bins — rebuilt every level/restart.

### Persisting the world (Bake workflow) — important
Runtime-created objects are discarded when you exit Play. To make the world a **permanent part of the scene** (so it
survives Play and so you can tune it in the Editor / for video), use the **Editor bake tool**:

> **Tools → Card Factory → Bake World Into Scene**, then **Ctrl+S**.

This rebuilds the world from code in **Edit mode** as real scene objects. The bake **preserves your anchor layout**.
Once baked, the world is **fine-tuned by hand in the Inspector** (object positions, framing, dock/offer details, etc.)
and those tweaks are saved with the scene. `WorldPersistence.rebuildOnPlay` only previews code changes during a Play
session — it is **not** persistent, and a full re-bake resets non-anchor manual tweaks (so anything you want kept
long-term should ideally live in code defaults).

### Editor tools
- **Tools → Card Factory → Bake World Into Scene / Clear Baked World**
- **Tools → Card Factory → Add SSAO (Depth AO)** — adds the SSAO renderer feature to the URP renderers.

---

## Presentation & juice

- Camera: top-down 2.5D angle framed for portrait; URP post-processing (bloom, vignette, color grading, tonemapping).
- Satisfying feedback: scale-pop / squash, particle bursts on ship, camera punch, and a full set of **procedural
  SFX synthesized in code** (no audio files) + haptics — punchy click/whoosh/plop cues, a soft *tick* as each card
  enters the belt, a lively ka-ching on ship, a bright ringing win fanfare, and a gentle descending "you failed" sting —
  plus a dock "tension" pulse as it fills.
- The **fail sting is deliberately designed** from researched fail-sound principles: a *descending* pitch (culturally
  read as defeat) on a consonant **C-minor** arpeggio that resolves into a low note **drooping downward** (a resigned
  "sad-trombone" sink), played on a warm sine-based voice with the harsh highs and sub-bass mud rolled off, a soft
  ~20 ms attack and a long decay, kept below the other cues in the mix — so it clearly says "you lost" without being
  jarring or annoying.
- Match-Factory-style polish: soft contact shadows under objects, glossy bins with a rising segmented fill bar +
  status lamp, identified conveyor with flowing chevrons, thicker cards, an animated **background factory**
  (running belts, moving boxes, silos), and a gate with an entry slot + front exit mouth + an X/20 progress bar.
- Upright cards **reorient to follow the belt** — they smoothly rotate toward the path tangent around the
  U-turns (fanning around the corners) instead of staying axis-aligned.

---

## Marketability — ad mode

`AdDirector` plays a single self-contained ad creative (benchmarked against **Match Factory! "Ad Type 3"**):
**fail-bait → success → CTA ("Play now")**, in English, color-psychology driven (red on failure, green on success).
Trigger with the **`A` key** (or `GameConfig.adMode`); it starts after a short ~3 s delay (recording buffer).
It drives the board itself on deterministic demo levels.

---

## Project structure

```
Assets/_Project/
  Scripts/
    Core/        GameBootstrap, GameManager, GameRunner, WorldPersistence, GameState, AdDirector
    Gameplay/    Card, CardStack, BeltPath, BeltFlow, Conveyor, FactoryGate, Bin, BinManager, Dock
    Input/       InputController
    Feedback/    ProcMesh, Juice, Sfx, CameraRig, Billboard, HandPointer, DecorMover
    Data/        CardColor, GameConfig, LevelData, DefaultLevels
    UI/          HudController
    Editor/      CardFactoryWorldBaker, SsaoSetup
  Scenes/        Main.unity (nearly empty; bootstrap builds everything)
  Docs/          HANDOFF.md (full status/handoff notes)
```

All tunable values live in `GameConfig` + `LevelData` + `DefaultLevels` and per-system constants — no code surgery
needed to adjust speed, capacities, palette or juice timings.

---

## Tech notes / constraints

- No prefabs are used and the world is **generated in code**; the bake tool writes that generated world into the
  scene as real objects, which are then **fine-tuned by hand in the Editor (Inspector)** and saved with the scene.
- Legacy `Input` (mouse + touch), Active Input Handling = **Both**.
- The only added package is `com.unity.recorder` (`Packages/manifest.json`) for capturing the videos.
- `Library/`, `Temp/`, `obj/` are git-ignored and can be excluded from the submission ZIP.
