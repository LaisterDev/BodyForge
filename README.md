<div align="center">
  <img src="editor/icon.svg" width="96" alt="BodyForge icon">
  <h1>BodyForge</h1>
</div>

<div align="center">
  <h3>Support the project</h3>
  <p>If BodyForge made your Viking better, you can help support its development.</p>
  <a href="https://buymeacoffee.com/laister">
    <img src="editor/assets/donations/buy-me-a-coffee.png" width="200" alt="Buy me a coffee">
  </a>
  <p><sub>Bitcoin: <code>BC1QG668UZLAY5C5KHAAM86GKS7TLY587UY5C0R9DA</code> &middot; Lightning: <code>curlypostbox723@walletofsatoshi.com</code></sub></p>
</div>

<div align="center">
  <h3>See BodyForge in action</h3>
  <a href="https://www.youtube.com/watch?v=T4Eo0G4Hui8">
    <img src="https://img.youtube.com/vi/T4Eo0G4Hui8/maxresdefault.jpg" width="640" alt="Watch the BodyForge showcase on YouTube">
  </a>
  <p><sub>Click the preview to watch the showcase on YouTube.</sub></p>
</div>

BodyForge is a live character editor for Valheim.
Change your Viking in-game to the way you like.

BodyForge can edit:

- Body model, hair, beard, skin color and hair color.
- Overall body scale and 52 additional skeleton bones.
- Left and right limbs together with symmetry, or independently.
- Exact X, Y and Z scale values for every proportion control.

BodyForge is an unofficial community project. It is not affiliated with Iron
Gate AB or Coffee Stain Publishing.

## Download

Download the portable ZIP for your operating system from the
[latest GitHub Release](../../releases/latest):

- `BodyForge-<version>-Windows-x86_64-Portable.zip`
- `BodyForge-<version>-Linux-x86_64-Portable.zip`

Extract the complete ZIP into a folder you can keep. BodyForge is portable: it
does not install anything into Windows or Linux and can be moved or removed by
moving or deleting its folder.

Do not download the repository source code unless you want to develop or build
BodyForge yourself.

## Requirements

- A legitimate PC installation of Valheim.
- Windows, or Linux running the Windows version of Valheim through Proton.
- BepInEx 5.4.23.5 or newer. The first-start wizard can install the recommended
  BepInExPack Valheim 5.4.2350 for you.

BodyForge never bundles or redistributes Valheim files. BepInEx is independent,
third-party open-source software licensed under LGPL-2.1.

## Installation

1. Extract the portable ZIP.
2. Run the BodyForge executable from the extracted folder.
3. Confirm or select your Valheim installation folder.
4. Read and accept the BepInEx dependency notice.
5. If needed, choose **Download and install compatible BepInEx**. BodyForge
   downloads the recommended community package from Thunderstore and verifies its
   pinned SHA-256 before extracting it.
6. Complete the platform-specific step shown below.
7. Choose **Install BodyForge and continue**.
8. Start or restart Valheim so BepInEx loads the installed plugin.

The wizard preserves existing BepInEx plugins and configuration files when it
installs or updates the loader.

### Linux

On Linux/Proton, open:

**Steam > Library > Valheim > Properties > General > Launch Options**

Enter exactly:

```text
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

This option is required, without it BepInEx and BodyForge will not load.

If the Linux executable is not marked as executable after extraction, run:

```bash
chmod +x BodyForge-*-Linux-x86_64-Portable
```

## Using BodyForge

1. Start BodyForge and Valheim.
2. Select a character and enter a world.
3. Open Valheim's pause menu so your character remains visible.
4. Use the **Appearance** and **Proportions** tabs in BodyForge.
5. Choose **Save character** when finished.

After finishing and saving your character, the editor can be closed and does 
not need to be opened again.

### Import and export

**Export** creates a `.bodyforge.json` package containing appearance, all bone
scales and the symmetry preference. **Import** applies a package to the character
currently active in Valheim. Choose **Save character** afterward to persist the
imported result.

## Multiplayer

BodyForge is fully multiplayer-compatible; no server-side changes are required.

Players with the mod installed will see characters with the modifications applied,
while players without the mod will see them with the original
settings—all without bugs or issues.

## Uninstalling

1. Close BodyForge and Valheim.
2. Delete the portable BodyForge folder.
3. Delete `Valheim/BepInEx/plugins/BodyForge/BodyForge.dll`.
4. Optionally delete `Valheim/BepInEx/config/BodyForge/` to remove saved
   proportions and bridge files.

Do not remove all of BepInEx if other installed mods use it. Removing BodyForge
does not modify or delete vanilla Valheim character saves.


## License

BodyForge code is available under the [MIT License](LICENSE). BepInEx is
third-party LGPL-2.1 software and is downloaded separately after user consent.
See [`THIRD_PARTY_NOTICES.md`](THIRD_PARTY_NOTICES.md) for details.
