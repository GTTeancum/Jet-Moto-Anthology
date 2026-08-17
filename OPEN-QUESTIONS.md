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
