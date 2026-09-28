# BL23 audio sources and licences

Record of where BL23 audio comes from. Append a section per source; never remove entries.

## BL23 composer (original)

disc_theme, film_* and disc_hit_* — original compositions and sounds rendered by BL23 composer; no third-party samples.
The same holds for disc_theme_ii, disc_theme_iii, disc_hit_4 and disc_hit_5 (added 2026-09-27).

| file (Assets/BASSLINE/BL23/Resources/…) | notes |
|---|---|
| `Music/disc_theme.wav` | "Wax Hymn", seamless 137.14 s loop, −18 LUFS |
| `Sfx/Film/film_sting.wav`, `Sfx/Film/film_sting_cues.txt` | discovery sting (hit at 0.300 s) and its cue file |
| `Sfx/Film/disc_hit_1.wav`, `disc_hit_2.wav`, `disc_hit_3.wav` (+ `disc_hit_N_cues.txt`) | insert hits for the discovery film (iron, glass, bone), each with a cue file giving its audible onset; revision 2 on 2026-09-27 |
| `Sfx/Film/film_heartbeat.wav`, `film_breath_in.wav`, `film_breath_out.wav`, `film_tinnitus.wav` | first-person film effects (tinnitus is a 6.0 s loop) |
| `Music/disc_theme_ii.wav` | "Wax Hymn II: Vigil" (glass armonica and a synthesised humming voice), seamless 137.14 s loop, −18 LUFS; a rotation partner for disc_theme, inert until registered (added 2026-09-27) |
| `Music/disc_theme_iii.wav` | "Wax Hymn III: Iron Psalm" (chant in organum, synthesised string cluster), seamless 137.14 s loop, −18 LUFS; a rotation partner for disc_theme, inert until registered (added 2026-09-27) |
| `Sfx/Film/disc_hit_4.wav`, `disc_hit_5.wav` (+ `disc_hit_4_cues.txt`, `disc_hit_5_cues.txt`) | optional insert hits (wax seal, coffin wood) matched to hits 1-3; unused until the film loads them (added 2026-09-27) |

- Tool, score and verification reports: `C:/Users/리오/BL23Lab/composer/`. See `NOTES.md` for the structure of each piece and `SOURCES.md` for the licence.
- `render all` re-creates every file bit-identically (SHA-256 in `composer/out/sha256.txt`).
