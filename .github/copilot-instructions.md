# Copilot Instructions

## Project Guidelines
- User prefers generated Unity player objects to have organized component hierarchies (especially audio sources grouped cleanly rather than many components on root).
- User wants to stop using the player generator tool and instead iterate directly on existing player objects/scripts in the scene.
- User prefers master volume control to allow headroom above their typical default mix level (treating setting value 1 as about 50%) to compensate for quiet game mixes.
- User prefers bootstrap startup flow to initialize important game systems first, then marked systems, then hold a visible post-load buffer before gameplay starts.
- User prefers new gameplay systems to be organized and modular when adding features.
- User wants all game data persistence to use one consistent storage approach to avoid mismatch/inconsistency errors; prefer JSON-based save storage for save slots.