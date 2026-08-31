# Character Profile System — Planning Documents

Deliverables for the "Character Profile System Feature Improvements" brief. Work modes are binding:
PLAN items stop at their gates (no implementation before sign-off); SCRUTINIZE items are analysis
documents, not diffs.

| Item | Mode | Document | Gate status |
|---|---|---|---|
| 1. Stable IDs for representations | PLAN (heavy) | [Item1-StableRepresentationIDs.md](Item1-StableRepresentationIDs.md) | **Implemented** (signed off; see doc's implementation notes) |
| 2. Loud fallbacks + TryGetRepresentation | PLAN (heavy) | [Item2-LoudFallbacks.md](Item2-LoudFallbacks.md) | **Implemented** (signed off) |
| 3. Reduce extension burden on CharacterRepresentationBase | PLAN (heavy) | [Item3-ReduceExtensionBurden.md](Item3-ReduceExtensionBurden.md) | Awaiting sign-off |
| 4. WitWeaverCharacterExpression overlap | SCRUTINIZE only | [Item4-CharacterExpressionAnalysis.md](Item4-CharacterExpressionAnalysis.md) | Analysis — decision after review |
| 5. ProcessExpression contract | SCRUTINIZE + document | [Item5-ProcessExpressionContract.md](Item5-ProcessExpressionContract.md) | XML docs **implemented**; tightening proposal awaiting review |
| 6. Centralize representation resolution | SCRUTINIZE + PLAN | [Item6-CentralizedResolution.md](Item6-CentralizedResolution.md) | Awaiting sign-off |
| 7. CharacterID validation | Implement after plan approval | [Item7-CharacterIDValidation.md](Item7-CharacterIDValidation.md) | Awaiting approval |

## Sequencing (binding, from the brief + plan dependencies)

```
Item 1 (stable IDs)  ──►  Item 2 (loud fallbacks)  ──►  Item 6 (central resolution)
Items 4, 5 (analyses)     — parallel with anything
Item 3                    — independent
Item 7                    — last (absorbs checks from Items 1 and 6)
```

Cross-cutting rules honored throughout: no new dependencies, Unity 2021+ only, no reflection-based
runtime scanning, public APIs preserved except where a plan explicitly justifies a break with
migration cost listed. Standard smoke pass (sample asset serialization, CreateAssetMenu entries,
asmdef compile, YAML and Excel round trips) applies to every implemented change.
