#!/usr/bin/env python3
"""FCH validator/parser (Fase 0 — research only).

Decodes Valheim character saves (.fch) using the authoritative format
reverse-engineered from assembly_valheim.dll v1.0.15 (decompiled):

  File = i32(dataLen) + data + i32(64) + sha512(data)
  data = ZPackage stream (BinaryWriter .NET conventions):
      int/float/long/bool/byte/ushort : little-endian primitives
      string                          : 7-bit length prefix + UTF-8 (.NET ReadString)
      byte[]                          : i32 length + raw bytes
      Vector3                         : 3 x f32
      NumItems                        : 1 byte (<128) or 2 bytes (0x80 lsb | hi, lo)

Designed to locate and splice the APPEARANCE fields inside the inner
"playerData" blob, which is the only mutation BodyForge makes to the .fch.

Usage: fch_validate.py <file.fch> [file2.fch ...]
"""
import hashlib
import struct
import sys


class Reader:
    def __init__(self, data, base=0):
        self.d = data
        self.i = base

    def tell(self):
        return self.i

    def skip(self, n):
        self.i += n
        if self.i > len(self.d):
            raise ValueError(f"overrun at {self.i} len={len(self.d)}")
        return self.i

    def take(self, n):
        b = self.d[self.i:self.i + n]
        self.skip(n)
        return b

    def u8(self):
        return self.take(1)[0]

    def i8(self):
        return struct.unpack("<b", self.take(1))[0]

    def u16(self):
        return struct.unpack("<H", self.take(2))[0]

    def i16(self):
        return struct.unpack("<h", self.take(2))[0]

    def u32(self):
        return struct.unpack("<I", self.take(4))[0]

    def i32(self):
        return struct.unpack("<i", self.take(4))[0]

    def f32(self):
        return struct.unpack("<f", self.take(4))[0]

    def i64(self):
        return struct.unpack("<q", self.take(8))[0]

    def boolean(self):
        return self.take(1)[0] != 0

    def vec3(self):
        return [self.f32(), self.f32(), self.f32()]

    def zstr(self):
        """7-bit length-prefixed UTF-8 string (.NET BinaryReader.ReadString)."""
        n = 0
        shift = 0
        while True:
            b = self.u8()
            n |= (b & 0x7F) << shift
            if not (b & 0x80):
                break
            shift += 7
        return self.take(n).decode("utf-8", errors="replace")

    def numitems(self):
        n = self.u8()
        if n & 0x80:
            n = ((n & 0x7F) << 8) | self.u8()
        return n

    def bytearray(self):
        n = self.i32()
        return self.take(n)


# Version.PlayerData enum (Version.cs)
P_ORIGINAL = 2
P_SKINHAIR = 4
P_SKINHAIRCOLOR = 5
P_UNIQUES = 6
P_MAXHEALTH = 7
P_FIRSTSPAWN = 8
P_TROPHIES = 9
P_MAXSTAMINA = 10
P_PLAYERMODEL = 11
P_FOOD = 12
P_FOOD2 = 14
P_STATIONS = 15
P_SKILLS = 17
P_KNOWNBIOMES = 18
P_REMOVETUTORIALS = 19
P_TIMESINCEDEATH = 20
P_READDTUTORIALS = 21
P_KNOWNTEXTS = 22
P_GUARDIANPOWER = 23
P_GUARDIANPOWERCOOLDOWN = 24
P_EITRSTAMINA = 26
P_MOVEDFIRSTSPAWN = 28
P_ABANDONEDDN = 31
P_CHUNKEDNORTH = 33

# Version.Player enum (Version.cs) — current numbering
V_STATS2 = 38
V_ASHANDS = 39
V_FIRSTSPAWN = 40
V_CALLTOARMS = 42
V_ABANDONEDDN = 44
V_DEEPNORTH = 46


def read_itemdata_old(r, item_version):
    """Inventory.LoadOld item layout (version < Item.Smaller=108):
       ReadString name, int stack, float durability, Vector2i pos,
       bool equipped, then version-gated quality/variant/crafter/pairs,
       worldLevel, pickedUp, cheated."""
    r.zstr()                 # name (prefab)
    r.i32()                  # stack
    r.f32()                  # durability
    r.i32()                  # gridPos x
    r.i32()                  # gridPos y
    r.boolean()              # equipped
    if item_version >= 101:  # Version.Item.Quality
        r.i32()
    if item_version >= 102:  # Version.Item.Variant
        r.i32()
    if item_version >= 103:  # Version.Item.CrafterID
        r.i64()
        r.zstr()             # crafterName
    if item_version >= 104:  # Version.Item.CustomData
        n = r.i32()
        for _ in range(n):
            r.zstr()
            r.zstr()
    if item_version >= 105:  # Version.Item.WorldLevel
        r.i32()
    if item_version >= 106:  # Version.Item.PickedUp
        r.boolean()
    if item_version >= 109 or item_version == 107:  # ChunksNCheats/AbandonedDN
        r.boolean()          # cheated


def read_itemdata(r, item_version):
    """ItemDrop.ItemData.Load — faithful byte walk (unused fields skipped)."""
    r.i32()                      # durability*100
    r.u8()                       # gridPos x
    r.u8()                       # gridPos y
    r.u8()                       # worldLevel
    flags = r.u8()
    if flags & 0x04:
        r.u16()                  # quality
    if flags & 0x08:
        r.u16()                  # stack
    if flags & 0x10:
        r.i32()                  # variant
    if flags & 0x20:
        r.i64()                  # crafterID
        r.zstr()                 # crafterName
    if flags & 0x40:
        r.i32()                  # dropPrefab hash
    if flags & 0x80:
        n = r.numitems()
        for _ in range(n):
            r.zstr()
            r.zstr()
    if item_version >= 109 or item_version == 107:  # ChunksNCheats / AbandonedDN
        r.u8()                   # cheated flags


def read_inventory(r):
    """Inventory.Load — i32 version; >= Smaller(108) uses u16 count + new
    ItemData format; older saves use LoadOld (i32 count + legacy fields)."""
    item_version = r.i32()
    if item_version >= 108:  # Version.Item.Smaller
        n = r.u16()
        for _ in range(n):
            read_itemdata(r, item_version)
    else:
        n = r.i32()
        for _ in range(n):
            read_itemdata_old(r, item_version)


def read_str_list(r):
    n = r.i32()
    for _ in range(n):
        r.zstr()


def read_str_pair_list(r):
    n = r.i32()
    for _ in range(n):
        r.zstr()
        r.zstr()


def read_str_float_list(r):
    n = r.i32()
    for _ in range(n):
        r.zstr()
        r.f32()


def read_str_int_list(r):
    n = r.i32()
    for _ in range(n):
        r.zstr()
        r.i32()


def walk_playerdata_blob(blob):
    """Walk the inner playerData blob; return offsets of appearance fields.

    Returns dict(start/end of beard, hair, skin, haircolor, model).
    Does NOT parse post-appearance sections (copied verbatim by caller).
    """
    r = Reader(blob)
    v = r.i32()
    out = {"version": v, "offsets": {}}

    if v >= P_MAXHEALTH:
        r.f32()  # maxHealth
        r.f32()  # health
    if v >= P_MAXSTAMINA:
        r.f32()  # maxStamina
    if v >= P_FIRSTSPAWN and v < P_MOVEDFIRSTSPAWN:
        r.boolean()  # firstSpawn (legacy)
    if v >= P_TIMESINCEDEATH:
        r.f32()  # timeSinceDeath
    if v >= P_GUARDIANPOWER:
        r.zstr()  # guardianPower
    if v >= P_GUARDIANPOWERCOOLDOWN:
        r.f32()  # guardianPowerCooldown
    if v == P_ORIGINAL:
        r.i64()
        r.u32()  # ZDOID

    read_inventory(r)  # inventory (always present)

    read_str_list(r)  # knownRecipes
    if v >= P_STATIONS:
        read_str_int_list(r)  # knownStations
    else:
        read_str_list(r)
    read_str_list(r)  # knownMaterial
    if v < P_REMOVETUTORIALS or v >= P_READDTUTORIALS:
        read_str_list(r)  # shownTutorials
    if v >= P_UNIQUES:
        read_str_list(r)  # uniques
    if v >= P_TROPHIES:
        read_str_list(r)  # trophies
    if v >= P_CHUNKEDNORTH or v == P_ABANDONEDDN:
        read_str_list(r)  # knownBiome (names, new)
    elif v >= P_KNOWNBIOMES:
        n = r.i32()
        for _ in range(n):
            r.i32()  # knownBiome (enum, old)
    if v >= P_KNOWNTEXTS:
        read_str_pair_list(r)  # knownTexts

    o = {}
    if v >= P_SKINHAIR:
        s = r.tell()
        beard = r.zstr()
        e = r.tell()
        o["beard"] = (s, e, beard)
        s = r.tell()
        hair = r.zstr()
        e = r.tell()
        o["hair"] = (s, e, hair)
    if v >= P_SKINHAIRCOLOR:
        s = r.tell()
        skin = r.vec3()
        e = r.tell()
        o["skin"] = (s, e, skin)
        s = r.tell()
        hcol = r.vec3()
        e = r.tell()
        o["haircolor"] = (s, e, hcol)
    if v >= P_PLAYERMODEL:
        s = r.tell()
        model = r.i32()
        e = r.tell()
        o["model"] = (s, e, model)
    out["offsets"] = o
    out["after_appearance"] = r.tell()
    return out


def walk_outer(data):
    """Walk the outer v46 envelope; return header fields + playerData blob."""
    r = Reader(data)
    version = r.i32()
    out = {"version": version, "rows": 0, "stats_per_row": 0}
    if version >= V_DEEPNORTH or version == V_ABANDONEDDN:
        stats_per_row = r.i32()
        rows = r.i32()
        out["stats_per_row"] = stats_per_row
        out["rows"] = rows
        for _ in range(rows):
            for _ in range(stats_per_row):
                r.f32()
            read_str_float_list(r)  # knownWorlds (string->float)
            read_str_float_list(r)  # knownWorldKeys
            read_str_float_list(r)  # knownCommands
            groups = r.i32()        # 5 enemy stat groups
            for _ in range(groups):
                read_str_float_list(r)
            read_str_float_list(r)  # itemPickupStats
            read_str_float_list(r)  # itemCraftStats
            read_str_float_list(r)  # pickableStats
            read_str_float_list(r)  # foodEatenStats
            read_str_float_list(r)  # piecesPlacedStats
    elif version >= V_STATS2:
        # Stats2..DeepNorth era: ONE count + that many stats floats
        # (single row into m_playerStats[0]).
        n = r.i32()
        for _ in range(n):
            r.f32()
    if version >= V_FIRSTSPAWN:
        r.boolean()                 # m_firstSpawn (added at Player.FirstSpawn=40)
    n = r.i32()                     # worldData entries
    for _ in range(n):
        r.i64()                     # world key
        r.boolean()                 # haveCustomSpawnPoint
        r.vec3()                    # spawnPoint
        r.boolean()                 # haveLogoutPoint
        r.vec3()                    # logoutPoint
        r.boolean()                 # haveDeathPoint
        r.vec3()                    # deathPoint
        r.vec3()                    # homePoint
        if r.boolean():             # hasMapData
            r.bytearray()
    name = r.zstr()
    pid = r.i64()
    seed = r.zstr()
    if version >= V_STATS2:         # player >= Stats2 (38): both v46 and v39
        r.boolean()                 # usedCheats
        r.i64()                     # dateCreated unix seconds
        if version < V_DEEPNORTH and version != V_ABANDONEDDN:
            # Pre-DeepNorth stores knownWorlds/Keys/Commands after dateCreated
            # (and, since CallToArms, also enemyStats + itemPickup + itemCraft).
            read_str_float_list(r)  # knownWorlds (string->float)
            read_str_float_list(r)  # knownWorldKeys
            read_str_float_list(r)  # knownCommands
            if version >= V_CALLTOARMS:
                groups = r.i32()    # enemyStats (1 group pre-DeepNorth)
                for _ in range(groups):
                    read_str_float_list(r)
                read_str_float_list(r)  # itemPickupStats
                read_str_float_list(r)  # itemCraftStats
    has_data = r.boolean()
    if has_data:
        blob = r.bytearray()
    else:
        blob = None
    if r.tell() != len(data):
        raise ValueError(f"outer tail bytes left: {len(data) - r.tell()}")
    out.update({"name": name, "playerID": pid, "seed": seed,
                "has_playerdata": has_data, "blob": blob})
    return out


def splite_appearance(blob, a, values):
    """Re-emit blob with appearance replaced. values dict of new beard/hair/
    skin/haircolor/model. Offsets from walk_playerdata_blob."""
    parts = []
    cursor = 0
    order = [("beard", True), ("hair", True), ("skin", False),
             ("haircolor", False), ("model", False)]
    for key, is_str in order:
        if key not in a:
            continue
        s, e, _ = a[key]
        parts.append(blob[cursor:s])
        new = values[key]
        if is_str:
            parts.append(encode_zstr(new))
        elif isinstance(new, (int,)):
            parts.append(struct.pack("<i", new))
        else:
            parts.append(struct.pack("<fff", *new))
        cursor = e
    parts.append(blob[cursor:])
    return b"".join(parts)


def encode_zstr(s):
    b = s.encode("utf-8")
    n = len(b)
    out = bytearray()
    while True:
        byte = n & 0x7F
        n >>= 7
        if n:
            out.append(byte | 0x80)
        else:
            out.append(byte)
            break
    return bytes(out) + b


def parse_fch(path):
    raw = open(path, "rb").read()
    r = Reader(raw)
    data_len = r.u32()
    data = r.take(data_len)
    hash_len = r.u32()
    digest = r.take(hash_len)
    if r.tell() != len(raw):
        raise ValueError(f"trailing bytes: {len(raw) - r.tell()}")
    if hash_len == 64 and digest != hashlib.sha512(data).digest():
        print("  [warn] SHA-512 mismatch (game does not validate it)")
    return data


def main():
    ok = True
    for path in sys.argv[1:]:
        print(f"== {path} ==")
        try:
            data = parse_fch(path)
            outer = walk_outer(data)
            print(f"  outer version={outer['version']} rows={outer['rows']} "
                  f"stats/row={outer['stats_per_row']}")
            print(f"  name='{outer['name']}' playerID={outer['playerID']} "
                  f"seed='{outer['seed']}' hasPlayerData={outer['has_playerdata']}")
            if not outer["blob"]:
                print("  (no player data blob)")
                continue
            a = walk_playerdata_blob(outer["blob"])
            print(f"  playerData version={a['version']}")
            for k in ("beard", "hair", "skin", "haircolor", "model"):
                if k in a["offsets"]:
                    s, e, val = a["offsets"][k]
                    val = list(val) if isinstance(val, list) and any(
                        isinstance(x, float) for x in val) else val
                    print(f"    {k}: offset {s}-{e} {val}")
            rebuilt = splite_appearance(
                outer["blob"], a["offsets"],
                {k: a["offsets"][k][2] for k in a["offsets"]})
            if rebuilt == outer["blob"]:
                print("  roundtrip: OK (splice with same values == original)")
            else:
                print("  roundtrip: MISMATCH")
                ok = False
        except Exception as exc:  # noqa: BLE001
            print(f"  ERROR: {exc}")
            ok = False
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main())