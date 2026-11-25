# Development and publishing

## GitHub approval and pull requests

- Develop changes on a local feature branch. Keep updates as pull requests for review; do not push directly to the default branch.
- Prepare and validate changes, push the feature branch, and open or update its pull request for review without requesting separate push confirmation.
- The pull request is the approval step. Keep it open for review; do not merge into the default branch, publish release tags, or publish a release without explicit user approval.
- Do not enable automatic publishing workflows without user authorization.

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
