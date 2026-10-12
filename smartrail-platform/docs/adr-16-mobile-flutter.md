# ADR-16 · Mobile applications built with Flutter

**Status:** Accepted by the owner on 12 Oct 2026 (the training covered Flutter).

## Decision
Build the passenger app and the operator app with Flutter, as two apps sharing one design-token package generated from `smartrail-tokens.json`.

## Options compared
| Option | For | Against |
|---|---|---|
| Flutter (chosen) | Covered in training; one codebase for Android and iOS; strong control over layout, which suits the status-chip and script (Sinhala, Tamil) rules; good offline support libraries | Dart; Material look needs the brand tokens applied |
| React Native | Shares skills with a React web app | Not what the training covered; time lost learning |
| Native Android only | Simple | Loses iOS, two codebases if added later |

## Reasons
Time. The owner already knows Flutter, and the design is token-driven, so the tokens file feeds the theme directly.

## Limits
- The web dashboard and the platform display are separate web apps, so the token package is generated for CSS as well.
- The operator app needs an offline queue; the plan is a local database (for example Drift or Isar) with retry.

## Fallback rule
If Flutter blocks a Must requirement near the freeze (for example offline sync), cut the Should and Could screens first, not the Must ones.
