# TradingView Pine Script Indicators

Flat repo of standalone Pine Script v6 indicators. Each `.pine` file is a complete TradingView indicator — no build, test, lint, or dependency management.

## Indicators

| File | Indicator Name | Description |
|------|---------------|-------------|
| `cvd-divergence-scanner.pine` | CVD Divergence Scanner | Pivot-based CVD divergence detection (price vs cumulative volume delta) with configurable sensitivity presets, dual delta methods (Close vs Open / Range Weighted), candle highlight on divergence, and alert conditions. |
| `es-context.pine` | ES Context Filter | Multi-session context dashboard with regime classification (bull/bear/range), EMA 21/8 levels, RTH VWAP, initial balance, manual volume profile levels, location proximity detection, and trade-plan logic. |
| `cvd-divergence-table.pine` | CVD Divergence Table | Table-based CVD divergence dashboard with regular/hidden divergence detection, VWAP context, trade window filtering, LONG/SHORT OK verdict, multi-preset support (scalp/context/RTH open), and per-type alert conditions. |
| `daily-key-levels.pine` | Daily Key Levels | Multi-level session key levels (PDH/PDL, PWH/PWL, Initial Balance, ORB-30, WMH/WML, OVH/OVL, RTH Open) with per-level color, style, and line-width controls. Configurable line style (solid/dashed/dotted) and width. |
| `es-market-sanity-dashboard.pine` | — | Empty stub — not yet implemented. |
| `es-nq-correlations-dashboard.pine` | ES/NQ Correlation Dashboard | 17-asset correlation matrix (ES, NQ, RTY, YM, VIX, DXY, US10Y, US02Y, ZN, SMH, XLK, XLF, RSP, NVDA, AAPL, MSFT, AMZN) comparing directional bias against expected correlation sign (±1) with summary counts and configurable disagreement alerts. |
| `timeframe-classifier.pine` | Trend Classifier | Multi-timeframe trend classification (Weekly → 1-min) using MA position, MA slope, and RSI, with ranging detection. Table output with color-coded Bullish/Bearish/Ranging per timeframe. |
| `volume-profile-levels.pine` | Volume Profile Plotter | Multi-session volume profile (RTH, Globex, Prior Week) with VAH/POC/VAL, PDH/PDL/ONH/ONL, and developing (live) RTH levels. Custom VP engine with configurable rows (20–200) and value area % (50–95%). All levels drawn as lines with labels on the chart. |

## Usage

Copy any `.pine` file content into TradingView's Pine Editor and save as a new indicator. No additional files or setup needed.

## Conventions

- Most files use `//@version=6` (the correlation dashboard is `//@version=5`)
- Indicators use `overlay=true` (draw on price chart, not a separate pane)
- Table-based output rendered in `barstate.islast` blocks
- `request.security()` for multi-timeframe / multi-ticker data
- ET timezone (`"America/New_York"`) for session-based logic
- Input groups organized by category (Display, Colors, Timeframes, etc.)

## Tooling

- Dev environment is WSL2: use `clip.exe` (or pipe to it) to copy file contents to the Windows clipboard, e.g. `clip.exe < file.pine`.
- CAVEAT: `clip.exe < file` runs the WSL→Windows interop pipe through the ANSI code page and **corrupts non-ASCII UTF-8** (e.g. `—` em-dashes become mojibake). For files containing non-ASCII, use PowerShell instead and verify byte-for-byte:
  `powershell.exe -NoProfile -Command 'Set-Clipboard -Value (Get-Content -Path "<abs-win-path>\file.pine" -Raw -Encoding UTF8)'`