# Open questions

Things I genuinely cannot decide alone. I route around these and keep working —
answer whenever convenient, in any order. Nothing here is blocking today.

---

*(none yet)*

---

## Answered

**Q: How much autonomy?** — Full; bypass prompts, work unattended. (2026-08-07)

**Q: Behaviour when blocked?** — Log it here, pick a defensible default, keep
going on unblocked work. (2026-08-07)

**Q: How is "can complete a lap" verified?** — Build the automated harness first
(screenshot capture, scripted pad input, memory assertions), then debug
gameplay against it. (2026-08-07)

## Jet Moto 3 visual correctness has no reference to check against

Raised 2026-08-17, after the `jm3-ontrack.txt` harness defect (see DECISIONS.md)
invalidated the whole missing-geometry investigation.

The recurring failure in this project is judging "does this frame look right?"
with nothing to compare against. It has now produced four wrong conclusions in
both directions: geometry declared missing that was a camera inside a wall,
geometry declared correct on the same bad evidence, dark wedges declared to be a
river, and oversized polygons declared pathological when a 400x300 "stretched"
threshold on a 320x240 frame flags any surface the camera is near.

Attract mode (`harness/jm3-demo.txt`) fixed the viewpoint problem — the game
drives itself along the developers' racing line — and it is deterministic, so a
given frame index is reproducible. What it does not give is ground truth for how
those frames should look.

**The question for the user:** is it acceptable to install a reference PS1
emulator on this machine to run the same attract demo and diff frame-for-frame?
That converts every one of these arguments into a pixel comparison. It is the
single highest-value unblock available and it is not something to decide
unilaterally — it means putting third-party software on the user's machine, and
CLAUDE.md's autonomy grant covers work *inside this repo*, not that.

Until answered, JM3 visual claims should be stated as unverified.

## The remaining dark regions are 0x0C20, not black

Measured 2026-08-17 on `/tmp/fixed` frame-0470 (320x240), after the texture
modulation saturation fix landed:

- Dominant colour is **(0,8,24) = 0x0C20 BGR555**, 16.2% of the frame.
- It is *not* pure black, and no row is more than 90% pure black. So this is a
  specific colour being drawn or cleared, not an empty framebuffer.

That matters because it makes the question answerable without guessing at
coordinates. `RECOMPONE_HUNT_COLOUR` takes a 15-bit target and points the pixel
watcher at a matching pixel itself:

```
RECOMPONE_HEADLESS=1 RECOMPONE_NO_HLE=1 RECOMPONE_HUNT_COLOUR=3104 \
RECOMPONE_HUNT_AFTER=250 RECOMPONE_INPUT=@harness/jm3-demo.txt \
RECOMPONE_UNPACED=1 dotnet JetMoto3/bin/Release/net10.0/JetMoto3.dll "Jet Moto 3 (USA).cue"
```

3104 is 0x0C20. Note the earlier run of this tool with the auto target (0xFFFF)
produced zero `[pixel]` lines and the cause was never established -- verify the
hunt actually fires before trusting a null result from it. That rule is now
written down because five investigations here died on probes that failed
silently.

**Do not compare frames across runs by index.** The attract demo cycles tracks,
so two runs land on different courses. A wireframe run and a normal run at the
same frame number showed different tracks. Pairs must be aligned by HUD content.

### 0x0C20 is painted by neither the rasteriser nor a fill rect

Two probes, both proven to fire before their results were believed:

- `RECOMPONE_LOG_COLOUR=3104` (new, self-targeting: reports the primitives that
  produce a given 15-bit output colour, no coordinates involved). Verified
  against a known answer first -- targeting black gives 40 hits in seconds.
  Targeting 0x0C20 through a race: **zero hits**.
- `RECOMPONE_LOG_FILL=1` across a full 400 s demo run: **one** fill rectangle,
  colour (0,0,0). `FillRect` writes VRAM directly and bypasses `Plot`, which is
  why the colour logger cannot see fills and why both probes were needed.

So the colour arrives by neither path. What is left:

1. A VRAM upload (`LoadImage`/CPU-to-VRAM DMA) painting a backdrop.
2. Nothing writes those pixels that frame, and what shows is stale framebuffer
   content.

Option 2 deserves the first look, because a single fill in 400 seconds means
**this game does not clear its framebuffer with fill rectangles**. Whatever it
clears with, we may be dropping it. Instrument `LoadImage` first to separate the
two, since that is one cheap probe that distinguishes them.

Caveat on the null: the traced run may simply have been on a track without that
colour, since attract cycles tracks. Confirm the colour is present in the same
run's dumps before treating the zero as meaningful.

### Uploads do land in the framebuffer region, as 24-pixel vertical strips

`RECOMPONE_LOG_UPLOAD=1` over a full demo run: 753 distinct uploads, **216** of
them landing at x<512, y<480. The largest are **24x256 and 24x240 vertical
strips** at x=72, 96 and similar, written to both y=0 and y=256.

Strip-wise column uploads at a fixed 24-pixel pitch are what a scrolling
panoramic backdrop looks like. That makes option 1 from the previous entry live:
something *is* uploaded into that region, so "stale framebuffer content" is no
longer the only candidate.

**Do not run ahead of this.** Two things are genuinely unresolved and were not
determined:

- Whether x=72..120 is inside the *display* window at the moment those uploads
  happen. At 320 wide the display buffer spans x 0..319, so these coordinates
  are ambiguous between framebuffer and texture memory without knowing the
  display origin at that instant. Log the display origin alongside the upload
  before concluding anything.
- Whether the strips are correct. A wrong stride, a wrong destination, or
  skipped strips would all show as banding, and the observed defect is wedges
  below a horizon rather than vertical bands. That mismatch is a reason for
  caution, not a reason to dismiss it.

Next probe: log display origin and width at each upload, then check whether any
upload actually intersects the live display window.
