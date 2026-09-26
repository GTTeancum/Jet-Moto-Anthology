# User-facing deployment requirement

- The playable, current build must be deployed to `D:\Programming\GitHub\Jet-Moto-Recomp\Jet Moto`, beside the user's original CUE/BIN files. An executable in `.build` alone is not delivery.
- Include the self-contained Windows runtime, native dependencies, HD texture/menu/HUD upgrades, and lighting data. Normal executable launch must enable the accepted visuals without development environment flags or an installed SDK.
- Bundle runtime and all managed/native dependencies inside JetMoto.exe. Loose dependency DLLs beside the EXE are not an acceptable delivery. Game artwork and original disc files can remain external. Archive obsolete loose dependencies and smoke-test without them.
- Preserve original disc files, saves, settings, and custom overrides. Use the safe deployment helper and verify deployed artifact hashes.
- Before declaring delivery complete, smoke-test the executable in that actual folder with adjacent-disc discovery and fresh settings/save state, safely backing up and restoring user data. Use only process-local replay and native framebuffer captures; inspect title/menu and gameplay content. Do not use desktop automation or host input.
- Report unverified parts honestly, especially audio listening and interactive controls when validation is headless. Restore test data and update the local deployment record/launcher target.
