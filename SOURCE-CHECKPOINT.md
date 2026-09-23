# Jet Moto Anthology Source Checkpoint

This repository starts from the cumulative Build 12 development folder, based
on recovered Build 11. It is a work-in-progress checkpoint, not a validated
Build 12 release. Historical build reports and provenance describe their
original builds; current acceptance status is in reports/build12/VALIDATION.md.

The user approved the ocean/spray appearance in
reports/build12/fine-airborne-spray-verified-runtime/frame-003554.png.
Preserve that baseline while addressing wake stability and swamp water.
The swamp uses original fake mirrored scenery; retain its material identity.

Disc images, generated game code, vendor source, SDK/cache contents, saves,
compiled binaries and the local Native4x-Test texture pack are excluded.
Building from a fresh clone requires the original game and local asset inputs
described by the existing build tooling; this is not a self-contained release.
The known-good Build 11 release and local excluded inputs remain untouched.

Routine game validation must use headless and muted execution with logging.
Use only bounded native in-game captures when visual verification is needed.
Never drive the desktop or inject host input. Compilation and test passes alone
do not establish visual acceptance.
