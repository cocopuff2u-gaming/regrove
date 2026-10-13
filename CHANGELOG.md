# Changelog

## 1.0.1

**Lighter on players' games**
* Countdown signs no longer pop in and out at the edge: a sign appears within `SignRadius` and only goes once you're 10 m further off.
* Walking into a logged area makes at most 4 new signs every 2 seconds, so a clearing full of stumps doesn't add 30 objects at once.
* Waits over an hour show in 5-minute steps ("about 1h 30m"), so a sign's text changes every 5 minutes instead of every minute.
* New saplings carry only the settings that differ from the game's own sapling, so there's a little less for each player's game to apply when it loads one. Saplings planted by 1.0.0 keep working as before.

## 1.0.0

First release.

* A stump a player removes leaves a sapling of its own tree a few seconds later.
* A stump left standing turns into a sapling by itself after `StumpRegrowDays` in-game days.
* `RemoveOnFell`: the stump goes as soon as its tree falls and the sapling is planted right away.
* `RegrowAs = Tree`: a left stump comes back as the tree that stood there, the same kind and size, swamp, Mistlands and Ashlands trees too.
* No saplings inside active wards or near anything a player built; `PathsAndFields` (off by default) keeps paths, roads and fields clear too.
* `ExcludeTrees`: trees Regrove leaves alone.
* `SpecialWoodOnly`: logs and stumps drop their tree's special wood (core wood, fine wood, ...) in place of plain wood.
* `CountdownSigns`: signs beside stumps and saplings near players, with the time left (`SignsOn`: both, stumps or saplings).
* `RegrowChance`: some left stumps can stay for good. `MaxPerRound` and `RoundMinutes` set how many grow back and how often.
* The round's cost on the server is in the log (about 0.3 ms a frame on a world of 2.9 million objects).
* Settings take effect when the file is saved, no restart.
* Snowy mountain firs and autumn birches come back as plain firs and birches. A sapling only appears where it can grow.
* Server only: players need nothing.
