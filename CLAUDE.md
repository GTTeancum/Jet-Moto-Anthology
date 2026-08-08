# Jet Moto Recomp — working agreement

## Goal

A native PC build of **Jet Moto (USA), SCUS-94309** via [RecompOne](https://github.com/BlackLabelHQ/RecompOne),
playable to the point that **a rider can complete laps**. That is the definition of done.
Everything else is a means to it.

## Operating rules (set by the user, 2026-08-07)

- **Full autonomy.** Work unattended. Do not stop to ask permission for ordinary
  build/debug/edit/commit work inside this repo.
- **Never block on a judgment call.** Pick the most defensible option, log it in
  `DECISIONS.md`, keep moving. Reversals are cheap because everything is committed.
- **Questions accumulate, they do not interrupt.** Anything genuinely undecidable
  goes to `OPEN-QUESTIONS.md`; route around it and continue on unblocked work.
- **`STATUS.md` is the user-facing surface.** Keep it current enough that a
  15-second glance answers "where is this?".
- **Verification before gameplay debugging.** The automated harness (screenshot
  capture + scripted pad input + memory assertions) is built *before* chasing
  gameplay bugs. It is what makes the rest of this project unsupervised.

## Hard boundaries

- **Never commit disc content.** `JetMotoPS1image/`, `JetMoto/disc/`, and every
  extracted asset stay gitignored. The port is code; the user supplies the disc.
- **No upstream PRs to RecompOne.** The maintainer rejects AI-authored PRs
  outright. Runtime fixes live in our local checkout under `tools/RecompOne/`.
  If that checkout gets patched, record it in `DECISIONS.md` so it survives a
  re-clone, and keep the diffs minimal and rebasable.
- **Nothing leaves this machine** without asking — no publishing, no uploads.

## Layout

```
JetMotoPS1image/        user's disc dump (gitignored)
JetMoto/
  config/jetmoto.json   RecompOne config
  config/funcmaps/      address -> name maps (the critical path)
  patches/              hand-written C# replacing/hooking recompiled functions
  generated/            recompiler output (gitignored, ~10 MB main.cs)
  Program.cs            entry point
tools/RecompOne/        upstream checkout (gitignored)
harness/                automated verification: capture, input scripts, assertions
```

## Rebuild from scratch

```bash
git clone --depth 1 https://github.com/BlackLabelHQ/RecompOne.git tools/RecompOne
dotnet build tools/RecompOne -c Release
dotnet run --project tools/RecompOne/RecompOne.Recompiler -c Release --no-build -- JetMoto/config/jetmoto.json
dotnet build JetMoto/JetMoto.csproj -c Release
```

## The critical path, in one paragraph

RecompOne routes PSYQ SDK calls to its own runtime implementations by **matching
function names as strings** (`RecompOne.Recompiler/CodeGen/SdkPatches.cs`). There
is no Jet Moto decompilation, so every function is currently `func_800XXXXX` and
**zero** SDK calls are routed. Naming the SDK functions — by PSYQ library
signature matching against the 1996-era SDK releases — is what makes the port
render, read the controller, and stream from the CD. Everything downstream
depends on it.
