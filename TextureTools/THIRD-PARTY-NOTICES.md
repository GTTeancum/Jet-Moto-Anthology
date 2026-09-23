# Neural authoring tool notices

`neural_models.py` contains modified, inference-only, checkpoint-compatible implementations of:

* Real-ESRGAN `SRVGGNetCompact`: copyright (c) 2021 Xintao Wang; BSD-3-Clause. Source: https://github.com/xinntao/Real-ESRGAN/blob/master/realesrgan/archs/srvgg_arch.py
* BasicSR `ResidualDenseBlock`, `RRDB`, `RRDBNet`: copyright 2018-2022 BasicSR Authors; Apache-2.0. Source: https://github.com/XPixelGroup/BasicSR/blob/master/basicsr/archs/rrdbnet_arch.py

Changes in this adaptation: fixed supported x4 network shapes, omitted registry/training initialization, added safe checkpoint loading and general-model parameter interpolation. Complete applicable licenses are in `licenses/`.

Official inference reference for model architecture and general-model denoising interpolation: https://github.com/xinntao/Real-ESRGAN/blob/master/inference_realesrgan.py

Model files were supplied by the user via the included collector metadata. The three SHA-256 values verify this specific upload; they are not publisher signatures. `MODEL-SOURCES.json` records the release URLs. This package does not redistribute model weights or Python/PyTorch binaries. The game does not run the neural network.

The software notices above do not grant rights to Jet Moto game artwork or trademarks. The PNGs are transformations of the supplied original assets, not artwork owned by the upscaler authors.
