# ECC rules

Rule packs copied from [affaan-m/ECC](https://github.com/affaan-m/ECC) at commit `b38be6a` (ECC 2.2.3):
`common/` (language-agnostic) and `csharp/` (loaded for `*.cs` files). They are byte-identical to the copy in the
desktop repo (`external/samaat.io/.claude/rules/ecc/`), so both repos follow the same rules.

The desktop repo enables the ECC plugin itself (agents, skills, commands, hooks) through its `.claude/settings.json`.
This repo doesn't have that file yet; see `docs/decisions/2026-11-05-mobile-improvements.md`. Plugins can't ship
rules, which is why these live here.

Where a rule conflicts with this repository's own conventions (`README.md`, `docs/TRY-IT.md`, and the desktop repo's
`CONTRIBUTING.md`, which this repo follows), the repository wins. To update, copy the same folders from the desktop repo
or from a newer ECC checkout.
