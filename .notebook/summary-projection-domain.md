# Summary Projection Domain
> Projection and daily-summary mutation boundaries

Entry: `backend/Summary/src/Infrastructure/ProjectionService.cs:ProjectionService.ApplyAsync()`

Flow: inbox deduplication → `ProjectedEntry.Apply()` → affected-date recalculation → `DailySummary.Refresh()`

- EF Core materializes Summary entities through private parameterless constructors.
- Projection versions must advance; obsolete events are ignored before `Apply()`.
- API freshness changes use `DailySummary.MarkFreshness()`; persistence refreshes use `DailySummary.Refresh()`.

Updated: 2026-09-20
