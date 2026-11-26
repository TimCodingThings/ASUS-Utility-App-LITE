# Development and publishing

## GitHub approval and pull requests

- Develop changes on a local feature branch. Keep updates as pull requests for review; do not push directly to the default branch.
- Prepare and validate changes, push the feature branch, and open or update its pull request for review without requesting separate push confirmation.
- The pull request is the approval step. Keep it open for review; do not merge into the default branch, publish release tags, or publish a release without explicit user approval.
- Do not enable automatic publishing workflows without user authorization.
- Reuse the owner's saved Windows-account GitHub session with `tools/Use-GitHubSession.ps1` before GitHub CLI operations. It loads a DPAPI-encrypted token from `%USERPROFILE%/Documents/Codex/.private/ASUS Utility LITE/GitHubAuth`, outside the repository. For native Git, pass `-GitArguments @('push', 'origin', '<feature-branch>')` to the same script.
- The owner requested reusable authentication. Preserve the encrypted cache between pushes; request a device sign-in only if it is missing, cannot be decrypted, or GitHub rejects the credential. After an explicitly authorized new sign-in, save it with `-SaveCurrentLogin`. Never print tokens, commit credentials, or copy the private cache into packages.

## Version naming

- Use `major.minor.patch` (`x.x.x`).
- Small changes and fixes increment the patch: `x.x.#` (for example, `1.2.0` to `1.2.1`).
- Large changes or substantial features increment the minor version: `x.#.x`; reset the patch to zero (for example, `1.2.1` to `1.3.0`).
- Reserve major increments (`#.x.x`) for rare major changes or a substantial accumulation of minor releases; reset minor and patch to zero (for example, `1.9.4` to `2.0.0`).
- Keep `VERSION`, assembly metadata, and release notes consistent.

## Validation and local package

- Run `Build.ps1` and `Test.ps1` for code changes, and verify affected layouts when changing the interface.
- Develop in this Git checkout and synchronize the user's existing installed package in place.
- Retain the supplied logo, GPL-3.0 licensing, and G-Helper attribution.

## Interface preferences

- Keep the main window's dimensions and position unchanged when opening or closing Fans + Power or Advanced.
- Fans + Power stays inside the main window below the main controls, with vertical scrolling and no horizontal scrolling.
- Mouse-wheel input scrolls the page, including over sliders, numeric editors, and fan selection; it must never change those settings.
- Distinguish the selected performance profile and acknowledged commands from independently verified firmware state. Do not use the saved highlight or a successful write as proof of the actual mode; keep readback explicitly unverified until a reliable method is established.
