# Jet Moto / RecompOne Build 12 Report

Build 12 replaces the Build 11 lighting/water attempt instead of tuning it.
The failed water-base development path was removed and the shipped renderer now
uses audited original-world water/source-face coverage, layered stationary
water detail, broken highlights and source-hue-preserving brightness control.

Rider/bike shadows now use a silhouette casting path for recognizable receiver
shadows while the rider/moto receiver is protected from the whole-object
darkening reported during validation. Wake/spray replacement art also has
shader-driven internal motion rather than only static retexturing.

Post-package correction: the first Build 12 ZIP made Island ocean and wave
highlights too bright. The refreshed package reduces water body/highlight gain
and anchors Island underlay hue to the audited original fill color while keeping
native textured wave strips source-hue based.

Validation is recorded in `reports/build12/VALIDATION.md`. The key accepted
native framebuffer captures are:

- `reports/build12/build12-final-visual-validation/frame-003000.png`
- `reports/build12/build12-final-visual-validation/frame-004800.png`
- `reports/build12/build12-final-visual-validation/frame-005520.png`

Build 11 was preserved as a separate release folder.
