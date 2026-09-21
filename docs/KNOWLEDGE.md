# KNOWLEDGE — BodyForge (Phase 0: format research)

Authoritative document of the `.fch` format (Valheim character saves),
validated by decompiling `assembly_valheim.dll` v1.0.15 (DeepNorth, patch
1.0.15) and by a real parser/validator running against the owner's three saves.

Validation tool: `research/fch_validate.py`
(envelope parse + outer walk + blob walk + splice/round-trip).

## `.fch` envelope (PlayerProfile.SavePlayerToDisk)

```
i32  dataLen
data (dataLen bytes = ZPackage stream)
i32  64
sha512(data)
```

The game does not verify the hash on load (kept for compatibility); the editor
MUST recompute sha512 on every write.

## ZPackage codec (ZPackage.cs, .NET BinaryWriter/Reader, little-endian)

| Type     | Layout                                              |
|----------|------------------------------------------------------|
| bool     | 1 byte (≠0)                                           |
| byte/u8  | 1 byte                                                |
| u16      | 2 bytes LE                                             |
| i32/u32  | 4 bytes LE                                             |
| i64      | 8 bytes LE                                             |
| f32      | 4 bytes LE (IEEE-754)                                  |
| string   | 7-bit length prefix (BinaryReader.ReadString) + UTF-8  |
| byte[]   | i32 length + bytes                                     |
| Vector3  | 3× f32                                                 |
| NumItems | 1 byte (≤127) or 2 bytes `(0x80|hi, lo)` — NOT the same 7-bit as strings |

## v46 outer envelope (Version.Player.DeepNorth = 46)

```
i32 46
i32 205            # stats per row
i32 10             # rows
per row (10×):
  205 × f32         # m_playerStats[i].m_stats[PlayerStatType]
  i32 count + (zstr, f32)*   # knownWorlds
  i32 count + (zstr, f32)*   # knownWorldKeys
  i32 count + (zstr, f32)*   # knownCommands
  i32 5                      # enemyStats groups
     per group: i32 count + (zstr, f32)*
  i32 count + (zstr, f32)*   # itemPickupStats
  i32 count + (zstr, f32)*   # itemCraftStats
  i32 count + (zstr, f32)*   # pickableStats
  i32 count + (zstr, f32)*   # foodEatenStats
  i32 count + (zstr, f32)*   # piecesPlacedStats
bool                   # m_firstSpawn (always present on v46)
i32 count + per world:
  i64 worldUID
  bool haveCustomSpawn; Vector3 spawnPoint
  bool haveLogout;     Vector3 logoutPoint
  bool haveDeath;      Vector3 deathPoint
  Vector3 homePoint
  bool hasMap;  if true: byte[] mapData
zstr name
i64 playerID
zstr seed
bool usedCheats
i64 dateCreated (unix seconds)
bool   # hasPlayerData
    if true: byte[] playerData (inner blob)
```

Important: there is NO "seconds since last save" field on v46 — the order right
after `seed` is `usedCheats → dateCreated`. `m_knownWorlds` etc. hold FLOAT
values (`(string, f32)`), not strings.

### Pre-46
- Stats2 era (v38–v43): a SINGLE `i32 count + count× f32` stats block before
  `firstSpawn/worlds`.
- `firstSpawn` only exists for `version >= Player.FirstSpawn` (40).
- Versions before DeepNorth write `knownWorlds/knownWorldKeys/knownCommands`
  (and since CallToArms=42 also `enemyStats + itemPickupStats + itemCraftStats`)
  AFTER `dateCreated`, before the `hasPlayerData` bool.

## Inner playerData blob (Player.Save / Version.PlayerData = 33)

```
i32 33
f32 maxHealth
f32 health
f32 maxStamina
f32 timeSinceDeath
zstr guardianPower
f32 guardianPowerCooldown
INVENTORY (see below)
i32 knownRecipes   + zstr*
i32 knownStations  + (zstr, i32)*
i32 knownMaterial  + zstr*
i32 shownTutorials + zstr*        # if <RemoveTutorials(19) or >=ReAddTutorials(21)
i32 uniques        + zstr*
i32 trophies       + zstr*
i32 knownBiome     + zstr*        # >=ChunkedNorth(33) or ==AbandonedDN(31);
                                  # otherwise (v18–32) a count of enum i32 (biomes)
i32 knownTexts     + (zstr, zstr)*
zstr beard        # <- APPEARANCE (starts here)
zstr hair
Vector3 skinColor
Vector3 hairColor
i32 modelIndex
[– everything below is copied verbatim by the editor –]
i32 foods + (zstr m_name, f32 m_time)*
skills, customData (i32 + (zstr,zstr)*), stamina f32, maxEitr f32, eitr f32,
buildUi byte[] (Hud.instance.m_buildUi.SaveToBinary())
```

### v27 blob (Ashlands era, e.g. the 20240525 backup):
same order, but `legacy firstSpawn` is read AFTER maxStamina
(if `>=FirstSpawn(8) && <MovedFirstSpawn(28)`), a 1-byte bool.
The inventory uses the legacy format (below). Appearance also starts after
knownTexts.

### Inventory (Inventory.Save / Version.Item)
- `i32 itemVersion`
- If `itemVersion >= Smaller(108)`: `u16 count` + items in new format.
- Otherwise (LoadOld): `i32 count` + legacy items.

**New format (item ≥ 108):**
```
i32 durability*100
u8 gridPos.x ; u8 gridPos.y ; u8 worldLevel ; u8 flags
flags&4  -> u16 quality
flags&8  -> u16 stack
flags&0x10 -> i32 variant
flags&0x20 -> i64 crafterID + zstr crafterName
flags&0x40 -> i32 dropPrefabHash
flags&0x80 -> NumItems + (zstr,zstr)* customData
if itemVersion >= 109 (or ==107): u8 cheated
```

**Legacy LoadOld format (item < 108):**
```
zstr name (prefab)
i32 stack
f32 durability
i32 gridPos.x ; i32 gridPos.y
bool equipped
if >=101 (Quality):    i32 quality
if >=102 (Variant):    i32 variant
if >=103 (CrafterID):  i64 crafterID + zstr crafterName
if >=104 (CustomData): i32 + (zstr,zstr)*
if >=105 (WorldLevel): i32 worldLevel
if >=106 (PickedUp):   bool pickedUp
if >=109 (or ==107):   bool cheated
```

## BodyForge mutation strategy on `.fch`

1. Detect envelope: parse dataLen, enter the ZPackage.
2. Walk the outer stream up to `hasPlayerData`, extract `byte[] playerData`.
3. Walk the blob to the appearance (prefix = everything before `beard`).
4. Replace ONLY `beard`, `hair`, `skinColor`, `hairColor`, `modelIndex`
   preserving variable string lengths (re-encode the 7-bit prefix).
5. Rebuild `data`, recompute `sha512`, write `.bak` before overwriting.
6. Godot editor: colors + hair + beard + model/gender in the `.fch`;
   per-bone proportions via the character's own `<char>.vhforges.json` applied
   at runtime by the BepInEx plugin (the vanilla game has NO bone-scaling
   system — there are no proportion fields in `.fch`, confirmed across
   `Player/Character/VisEquipment/Humanoid`).

## Relevant versions (Version.cs v1.0.15)

- Player: Stats=28, MapData=29, DeathPoint=30, Stats2=38, Ashlands=39,
  FirstSpawn=40, BogWitch=41, CallToArms=42, Celebration=43, AbandonedDN=44,
  Chunked=45, DeepNorth=46.
- PlayerData: ...=EitrStamina 26, AshlandMaterials 27, MovedFirstSpawn 28,
  BogWitch 29, AbandonedDN 31, ChunkedSaves 32, ChunkedNorth 33.
- Item: Quality=101, Variant=102, CrafterID=103, CustomData=104, WorldLevel=105,
  PickedUp=106, AbandonedDN=107, Smaller=108, ChunksNCheats=109.

## Runtime integration API (confirmed via reflection on 1.0.15 assemblies)

- `PlayerProfile.GetFilename()` (lowercase `n` — the property `m_filename` is
  `public string` too). Older notes that said `GetFileName()` are wrong for
  current builds; all three letter-cases are also disambiguated by reflection:
  only `GetFilename` exists.
- Current profile: `Game.instance.GetPlayerProfile()`; `Game.SetProfile(...)`
  is static for writes.
- Character path: `SaveSystem.GetCharacterFolderPath(FileSource)` (static) and
  `SaveSystem.GetCharacterPath(FileSource, String)`; `SaveSystem
  .IsCharacterSaveExtension(String)` checks `.fch`.
- Version: `Version` is a static class; `Version.CurrentVersion` property of
  type `GameVersion` (fields `m_major/m_minor/m_patch`). There is **no**
  `VersionInfo` type in current assemblies.
- Body: `VisEquipment.m_bodyModel` (`SkinnedMeshRenderer`, public), fields
  `m_isPlayer` (public bool) and `m_nview` (private) and `m_boneMap`
  (private `Dictionary<string, Transform>`), built in `Awake`.
  Blend shapes are not used for proportions (`SkinnedMeshRenderer.bones` +
  `bone.localScale` is the only lever).
- Networking: skip non-local bodies with `ZNetView.IsOwner()`.

## Licenses

- Valheim game: property of Iron Gate; none of the game code is copied.
- The decompilation here is documentary research; BodyForge does not
  redistribute any game assembly, it only reads the player's files.
- The BepInEx plugin (our code) is a separate work, distributed under MIT;
  BepInEx itself is LGPL-2.1, never embedded — downloaded from the official
  pack at install time.