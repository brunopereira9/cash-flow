# Playwright execution plan

## Preconditions

Run `dotnet run --project backend/Core/src/Core.Api --urls http://localhost:5080` and `npm --prefix front run dev -- --host 0.0.0.0`.

## Scenarios

1. Open `/`; assert dashboard heading, balance card, recent activity and `Novo lançamento`.
2. Resize to 390px; assert no horizontal overflow and primary controls remain visible.
3. Stop the API; assert an error state and that balance is not labelled `current`.
4. Submit a valid entry; assert success feedback and one activity row.
5. Submit the same idempotency key twice; assert no duplicate activity row.

## Evidence

Capture a screenshot per scenario, console errors, failed requests and final URL. PASS requires every listed assertion.
