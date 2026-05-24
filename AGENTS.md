# TradingView Pine Script Indicators

Flat repo of standalone Pine Script v6 indicators. Each `.pine` file is a complete TradingView indicator — no build, test, lint, or dependency management.

## Indicators

| File | Indicator Name | Description |
|------|---------------|-------------|
| `chart-patterns-indicator.pine` | Chart Pattern Scanner | Candle pattern detection (engulfing, hammer, shooting star, morning/evening star) on a user-selectable timeframe |
| `daily-key-levels-indicator.pine` | Daily Key Levels | PDH/PDL, PWH/PWL, Initial Balance (9:30-10:30), ORB-30 (9:30-10:00) with ET timezone session logic |
| `es-nq-divergence-indicator.pine` | ES/NQ Divergence & Relative Strength | Multi-timeframe divergence analysis between ES and NQ futures, relative strength comparison, price ratio tracking |
| `al-brooks-price-action.pine` | Al Brooks Price Action | Pure price action patterns (SBL/SBS, inside/outside bars, ii patterns, traps, double top/bottom, wedges, H1/H2/L1/L2, final flags) with yellow signal candles and alerts |
| `cvd-absorption-exhaustion.pine` | CVD Absorption Exhaustion Divergence | Cumulative Volume Delta divergence detection — absorption (price leads, CVD doesn't confirm) and exhaustion (CVD leads, price doesn't confirm) with table and chart labels |
| `volume-profile-levels.pine` | Prior RTH Volume Profile Auto-Plotter | Multi-session volume profile (RTH, Globex, Prior Week) with VAH/POC/VAL, PDH/PDL/ONH/ONL, and developing (live) RTH levels. Custom VP engine with configurable rows (20–200) and value area % (50–95%). All levels drawn as lines with labels on the chart. |

## Usage

Copy any `.pine` file content into TradingView's Pine Editor and save as a new indicator. No additional files or setup needed.

## Conventions

- All files use `//@version=6`
- Indicators use `overlay=true` (draw on price chart, not a separate pane)
- Table-based output rendered in `barstate.islast` blocks
- `request.security()` for multi-timeframe / multi-ticker data
- ET timezone (`"America/New_York"`) for session-based logic
- Input groups organized by category (Display, Colors, Timeframes, etc.)
