# Placeholder font atlases

Stand-ins in the format `tools/` exports (`<name>.png` plus `<name>.json`, spec section 3
"Fonts"), so the mock mode and the screenshots show real text before the game fonts exist.
They are **not** the game's fonts and never ship: the overlay reads `assets/fonts` first and
falls back here only under `?mock=1`.

Delete this directory once `assets/fonts` is exported, and drop `DEV_FONT_ROOT` with it.
