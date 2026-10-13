<p align="center"><img src="https://raw.githubusercontent.com/cocopuff2u-gaming/regrove/main/package/icon.png" alt="Regrove" width="160"></p>

<h1 align="center">Regrove</h1>

<p align="center"><b>Chopped forests grow back on their own.</b></p>

Install it on your dedicated server and the stumps your players leave behind
turn back into saplings of the same tree, which grow into new trees.
Your players don't install anything. It runs only on the server.

The name is regrow + grove: what was cut down comes back.

---

## What you get

| | |
|---|---|
| **Removed stumps leave a sapling** | Dig out or chop a stump and a sapling of its tree appears in its place a few seconds later. Beech, birch, fir, pine and oak: each stump gives back its own kind of tree. |
| **Left stumps regrow too** | A stump nobody touches turns into a sapling by itself after a few in-game days, so old clearings fill back in without anyone having to tidy up. |
| **Your bases stay clear** | No saplings inside an active ward or close to anything a player built, so the ground you cleared for your base stays clear. |
| **Grows like a planted tree** | The saplings grow like the ones players plant, and the time they take can be changed. They don't need cultivated ground and don't wither when other trees stand close. |
| **Server only** | Nothing for players to install. It uses the game's own objects, so a vanilla game sees ordinary saplings. |
| **Light on your server** | Stumps are checked a slice of the world at a time, every few minutes. Left stumps only change where no player is, at most 50 per round. |

---

## Install

You need a Valheim **dedicated server** with **BepInEx** on it
([BepInExPack for Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/)).
Most server hosts have a one-click option for BepInEx.

### Step by step

1. **Download** `Regrove-<version>.zip` from
   [Releases](https://github.com/cocopuff2u-gaming/regrove/releases/latest).
2. **Unzip it.** Inside is a `plugins/Regrove` folder.
3. **Copy** the `Regrove` folder into your server's `BepInEx/plugins` folder,
   so you end up with:

   ```
   <server>/BepInEx/plugins/Regrove/Regrove.dll
   ```

4. **Start the server.** In the server log you should see:

   ```
   Regrove 1.0.0 loaded
   ```

   and, once the world is up, which stump grows back as which sapling:

   ```
   Regrove: Beech_Stub: Beech1 -> Beech_Sapling
   ```

That's it.

**Using a mod manager** (r2modman, Gale)? Use its "import local mod" option
with the zip. It puts the files in the right place.

> **Using Treesurrection** (from ServersideQoL)? It does the same job, so
> remove one of the two. While both are installed, Regrove stays switched off
> and says so in the log.

---

## Settings

Settings live in `BepInEx/config/com.cocopuff2u.regrove.cfg`. The file
appears after the first start. Edit it while the server is stopped, or
restart the server afterwards.

| Setting | Default | What it does |
|---|---|---|
| `Enabled` | `true` | Turns the whole mod on or off. |
| `SaplingChance` | `100` | Chance in percent that a removed stump leaves a sapling. |
| `SaplingDelaySeconds` | `5` | Seconds before the sapling appears, so the last axe swing doesn't hit it. |
| `GrowTimeMultiplier` | `1` | How long a sapling takes to grow, compared with one a player plants. `0.5` is twice as fast. |
| `StumpRegrowDays` | `3` | In-game days (30 minutes each) a stump can stand before it turns into a sapling by itself. `0` turns this off. |
| `InsideWards` | `true` | No saplings inside an active ward. |
| `BuildRadius` | `20` | No saplings within this many metres of anything a player built. `0` turns this off. |

---

## Good to know

* **Time only runs while someone is playing.** A dedicated server stops the
  world's clock when nobody is online, so `StumpRegrowDays` counts days played,
  the same clock the saplings grow on.
* **Saplings grow while someone is nearby**, like every plant in Valheim.
  Come back after the grow time and the sapling turns into a tree within seconds.
* **Stumps already in your world count too.** The first time Regrove sees a
  stump it notes the time; after `StumpRegrowDays` from then, it regrows.
* **A stump near a base is kept** and checked again after another
  `StumpRegrowDays`, in case the building has gone by then.
* **Which trees come back**: beech, birch, fir, pine and oak, the trees players
  can plant. The snowy mountain firs and the autumn birches come back as plain
  firs and birches. Saplings only appear where they can grow, so snowy pines
  high in the mountains, the swamp's ancient trees, the Mistlands' Yggdrasil
  shoots and the Ashlands' trees leave their stumps.

---

## Troubleshooting

| Problem | Fix |
|---|---|
| Nothing in the log about Regrove | BepInEx isn't running. Check that `BepInEx/LogOutput.log` exists and the server starts through BepInEx. |
| `Treesurrection is installed` in the log | Remove ServersideQoL.Treesurrection, or Regrove. |
| No sapling where a stump was | It's near a ward or a build (see `InsideWards` and `BuildRadius`), or `SaplingChance` is below 100. |
| A sapling inside my base | The base has no ward and nothing built within `BuildRadius` of that spot. Raise `BuildRadius`, or place a ward. |

Still stuck? [Open an issue](https://github.com/cocopuff2u-gaming/regrove/issues)
and attach `BepInEx/LogOutput.log`.

---

## More

* [CHANGELOG.md](CHANGELOG.md): what changed in each version.
* **Building from source**: `./build.sh --managed <server>/valheim_server_Data/Managed`
  (needs the .NET SDK 8+). The zip lands in `dist/`.

## Credits

Regrove is made by [Cody Keats](https://github.com/cocopuff2u), who also makes
[Munin Map](https://github.com/cocopuff2u-gaming/munin-map). The idea comes
from Treesurrection, part of ArgusMagnus's
[ServersideQoL](https://github.com/ArgusMagnus/ValheimServersideQoL).
Thank you.

## Licence

MIT. See [LICENSE](LICENSE).
