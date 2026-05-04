# TradingView Pine Script Indicators

Flat repo of standalone Pine Script v6 indicators. Each `.pine` file is a complete TradingView indicator — no build, test, lint, or dependency management.

## Indicators

| File | Indicator Name | Description |
|------|---------------|-------------|
| `chart-patterns-indicator.pine` | Chart Pattern Scanner | Candle pattern detection (engulfing, hammer, shooting star, morning/evening star) on a user-selectable timeframe |
| `daily-key-levels-indicator.pine` | Daily Key Levels | PDH/PDL, PWH/PWL, Initial Balance (9:30-10:30), ORB-30 (9:30-10:00) with ET timezone session logic |
| `es-nq-divergence-indicator.pine` | ES/NQ Divergence & Relative Strength | Multi-timeframe divergence analysis between ES and NQ futures, relative strength comparison, price ratio tracking |

## Usage

Copy any `.pine` file content into TradingView's Pine Editor and save as a new indicator. No additional files or setup needed.

## Conventions

- All files use `//@version=6`
- Indicators use `overlay=true` (draw on price chart, not a separate pane)
- Table-based output rendered in `barstate.islast` blocks
- `request.security()` for multi-timeframe / multi-ticker data
- ET timezone (`"America/New_York"`) for session-based logic
- Input groups organized by category (Display, Colors, Timeframes, etc.)
