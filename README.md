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
| **Or no stumps at all** | Switch on `RemoveOnFell` and the stump goes as soon as the tree falls, with the sapling planted in its place and the stump's wood dropped on the ground. |
| **Left stumps regrow too** | A stump nobody touches grows back by itself after a few in-game days, so old clearings fill back in without anyone having to tidy up. It becomes a sapling, or, if you like, straight away the tree that stood there, the same kind and size. |
| **Your bases stay clear** | No saplings inside an active ward, close to anything a player built, or on paths, roads and fields, so the ground you cleared stays clear. |
| **Only special wood** | Switch on `SpecialWoodOnly` and logs and stumps drop core wood, fine wood, ancient bark and the like in place of plain wood. |
| **Countdown signs** | Switch on `CountdownSigns` and stumps and saplings near a player get a small sign saying how long they have left. |
| **Pick your trees** | Leave some trees out entirely, say swamp trees or oaks. |
| **Grows like a planted tree** | The saplings grow like the ones players plant, and the time they take can be changed. They don't need cultivated ground and don't wither when other trees stand close. |
| **Server only** | Nothing for players to install. It uses the game's own objects, so a vanilla game sees ordinary saplings and signs. |
| **Change settings while it runs** | Edit the settings file and the change takes effect a second later, no restart. |
| **Light on your server** | Left stumps are checked a slice of the world at a time, a round every 5 minutes. On a world of 2.9 million objects a round costs the server about 0.3 ms a frame for half a minute, its slowest frame 1.3 ms; the log shows the numbers for yours. Left stumps only change where no player is, at most 50 per round. |

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

---

## Settings

Settings live in `BepInEx/config/com.cocopuff2u.regrove.cfg`. The file
appears after the first start. Edit it any time: Regrove picks up the
change a second after you save, and says `Settings reloaded` in the log.

| Setting | Regrove default | Vanilla game | What it does |
|---|---|---|---|
| `Enabled` | `true` | | Turns the whole mod on or off. |
| `SaplingChance` | `100` | `0`: a removed stump leaves nothing | Chance in percent that a removed stump leaves a sapling. |
| `SaplingDelaySeconds` | `5` | | Seconds before the sapling appears, so the last axe swing doesn't hit it. |
| `GrowTimeMultiplier` | `1` | `1`: beech, fir and pine take 50-83 minutes, birch 83-117, oak 100-133 | How long a sapling takes to grow, compared with one a player plants. `0.5` is twice as fast. |
| `RemoveOnFell` | `false` | `false`: stumps stay until someone digs them out | Stumps go as soon as their tree falls, and the sapling is planted right away. The stump's wood is lost. |
| `StumpRegrowDays` | `3` (90 minutes played) | `0`: stumps stand forever | In-game days (30 minutes each) a stump can stand before it grows back by itself. `0` turns this off. |
| `RegrowAs` | `Sapling` | | What a left stump turns into: `Sapling`, which then grows, or `Tree`, the tree that stood there, the same kind and size, at once. |
| `RegrowChance` | `100` | | Chance in percent that a left stump grows back at all. Each stump's answer is fixed, so below 100 some stumps simply stay and old clearings fill in patchily. |
| `MaxPerRound` | `50` | | Left stumps that may grow back in one round. |
| `RoundMinutes` | `5` | | Minutes between rounds, how often the world is checked for left stumps that are due. |
| `InsideWards` | `true` | | No saplings inside an active ward. |
| `BuildRadius` | `20` | | No saplings within this many metres of anything a player built. `0` turns this off. |
| `PathsAndFields` | `true` | | No saplings on paths, paved roads or cultivated ground, or within 2 m of them. |
| `ExcludeTrees` | (empty) | | Trees Regrove leaves alone, by name or the start of a name, comma separated, e.g. `SwampTree, Oak`. The server log lists every tree's name at start. |
| `SpecialWoodOnly` | `false` | `false`: logs drop a mix of plain and special wood, stumps plain wood | Logs and stumps drop their tree's special wood in place of plain wood, one for one (see below). |
| `CountdownSigns` | `false` | | A small sign beside each stump and regrowing sapling near a player, saying how long it has left. |
| `SignsOn` | `Both` | | Which get a countdown sign: `Both`, `Stumps` or `Saplings`. |
| `SignRadius` | `15` | | How close, in metres, a stump or sapling must be to a player to get a sign. |

A blank means the game has nothing like it.

**Special wood by tree** (from the game's own drop lists): pine gives core
wood, birch and oak fine wood, swamp trees ancient bark, Mistlands trees
Yggdrasil wood, Ashlands trees blackwood, and snowy and big firs frost wood.
Beech and plain fir have no special wood and still give plain wood.

Saplings Regrove plants also differ from ones a player plants in two ways
that aren't settings: they grow among other trees (a planted sapling needs
2 m of clear room around it, an oak 3 m), and firs regrow on mountains, where
a fir sapling a player plants stays "Too cold" and never grows.

---

## Good to know

* **Time only runs while someone is playing.** A dedicated server stops the
  world's clock when nobody is online, so `StumpRegrowDays` counts days played,
  the same clock the saplings grow on.
* **Saplings grow while someone is nearby**, like every plant in Valheim.
  Come back after the grow time and the sapling turns into a tree within seconds.
* **Stumps already in your world count too.** The first time Regrove sees a
  stump it notes the time; after `StumpRegrowDays` from then, it regrows.
* **Countdown signs** show time played, the same clock the trees grow on. They
  only stand near players (at most 64 at once), can't be removed or broken, and
  are never saved: they go away with a restart, or a moment after you switch
  `CountdownSigns` off.
* **With `SpecialWoodOnly`**, the player breaking a log makes the drops, and the
  server swaps each plain wood for the special one as it lands. On a very laggy
  connection a player can now and then pick a plain wood up before the swap.
* **A stump near a base is kept** and checked again after another
  `StumpRegrowDays`, in case the building has gone by then.
* **Which trees come back as saplings**: beech, birch, fir, pine and oak, the
  trees players can plant. The snowy mountain firs and the autumn birches come
  back as plain firs and birches. Saplings only appear where they can grow, so
  snowy pines high in the mountains, the swamp's ancient trees, the Mistlands'
  Yggdrasil shoots and the Ashlands' trees leave their stumps.
* **With `RegrowAs = Tree`**, every left stump comes back as the very tree that
  stood there, swamp, Mistlands and Ashlands trees included. Regrove notes which
  tree fell as it falls; for older stumps it goes by the trees around them.

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
* **Self-test**: build with `dotnet build src/Regrove.csproj -c Release -p:SelfTest=true`,
  put that DLL on a test server, create an empty file `BepInEx/config/regrove.selftest`
  and start the server. On an empty patch of Meadows it acts out what players do
  (digging stumps, breaking logs, felling trees, standing near stumps), logs
  `Regrove TEST PASS` or `FAIL` for each feature, then removes what it made.
  The normal build has none of this in it.

## Credits

Regrove is made by [Cody Keats](https://github.com/cocopuff2u), who also makes
[Munin Map](https://github.com/cocopuff2u-gaming/munin-map). The idea comes
from Treesurrection, part of ArgusMagnus's
[ServersideQoL](https://github.com/ArgusMagnus/ValheimServersideQoL).
Thank you.

## Licence

MIT. See [LICENSE](LICENSE).
