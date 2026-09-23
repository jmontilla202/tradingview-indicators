
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows.Media;
using System.Xml.Serialization;
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using SharpDX;
using SharpDX.Direct2D1;
using SharpDX.DirectWrite;
using SolidColorBrush = SharpDX.Direct2D1.SolidColorBrush;
#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
	public enum TablePosition { TopLeft, TopCenter, TopRight, MiddleLeft, MiddleRight, BottomLeft, BottomCenter, BottomRight }
    public enum FontSizeEnum { Tiny = 10, Small = 12, Normal = 14, Large = 16, Huge = 20 }

	public class FuturesConfluenceTable : Indicator
    {
        #region Fields
        private const int PRIMARY = 0;
        private int _tickSeries;
        private int _mtf1, _mtf2, _mtf3;

        private EMA _emaF, _emaS, _emaT;
        private RSI _rsi;
        private ATR _atr;
        private SMA _volMA;

        private EMA _mtf1EmaF, _mtf1EmaS, _mtf2EmaF, _mtf2EmaS, _mtf3EmaF, _mtf3EmaS;

        // Delta tracking (real, from tick series)
        private List<double> _barDelta = new List<double>();
        private List<double> _barCVD   = new List<double>();
        private double _currentBarDelta;
        private double _cumulativeDelta;
        private double _barDeltaHigh, _barDeltaLow;   // intra-bar extremes for absorption
        private double _barOpenPrice;

        // Sessions
        private double _pdh = double.NaN, _pdl = double.NaN;
        private double _onh = double.NaN, _onl = double.NaN;
        private double _pmh = double.NaN, _pml = double.NaN;
        private double _rthOpen = double.NaN, _rthH = double.NaN, _rthL = double.NaN;

        private double _sessOnH = double.NaN, _sessOnL = double.NaN;
        private double _sessPmH = double.NaN, _sessPmL = double.NaN;
        private double _sessRthH = double.NaN, _sessRthL = double.NaN, _sessRthO = double.NaN;
        private DateTime _lastRthDate = DateTime.MinValue;

        // VWAP (session-anchored to RTH open)
        private double _vwapNum, _vwapDen, _vwapValue;
        private DateTime _vwapAnchorDate = DateTime.MinValue;

        // Pivots
        private double _lastPH = double.NaN, _prevPH = double.NaN;
        private double _lastPL = double.NaN, _prevPL = double.NaN;
        private int _pivotStrength = 3;

        // Render
        private SharpDX.DirectWrite.TextFormat _textFormat;
        private SharpDX.Direct2D1.SolidColorBrush _bWhite, _bGray, _bGreen, _bRed, _bNeutral,
            _bHeaderBg, _bRowBull, _bRowBear, _bRowNeutral, _bBiasBull, _bBiasBear, _bBiasNeutral, _bBorder;

        // Signals
        private List<SignalRow> _rows = new List<SignalRow>();
        private int _bullScore, _bearScore, _confPct, _biasDir;
        private string _biasTxt = "NEUTRAL";

        // Alerts throttle
        private DateTime _lastAlertTime = DateTime.MinValue;

        private struct SignalRow
        {
            public string Name, Dir, Conf, Note;
            public int    DirCode;
        }

        // Liquidity line tags
        private const string TAG_PDH = "FCT_PDH";
        private const string TAG_PDL = "FCT_PDL";
        private const string TAG_ONH = "FCT_ONH";
        private const string TAG_ONL = "FCT_ONL";
        private const string TAG_PMH = "FCT_PMH";
        private const string TAG_PML = "FCT_PML";
        #endregion

        #region Properties
        [NinjaScriptProperty, Display(Name="EMA Fast",  GroupName="Moving Averages", Order=0)] public int EmaFastLen { get; set; } = 9;
        [NinjaScriptProperty, Display(Name="EMA Slow",  GroupName="Moving Averages", Order=1)] public int EmaSlowLen { get; set; } = 21;
        [NinjaScriptProperty, Display(Name="EMA Trend", GroupName="Moving Averages", Order=2)] public int EmaTrendLen{ get; set; } = 50;

        [NinjaScriptProperty, Display(Name="RSI Length",     GroupName="RSI", Order=0)] public int RsiLen { get; set; } = 14;
        [NinjaScriptProperty, Display(Name="RSI Overbought", GroupName="RSI", Order=1)] public int RsiOB { get; set; } = 70;
        [NinjaScriptProperty, Display(Name="RSI Oversold",   GroupName="RSI", Order=2)] public int RsiOS { get; set; } = 30;

        [NinjaScriptProperty, Display(Name="MTF 1 (min)", GroupName="Multi-Timeframe", Order=0)] public int Mtf1Min { get; set; } = 5;
        [NinjaScriptProperty, Display(Name="MTF 2 (min)", GroupName="Multi-Timeframe", Order=1)] public int Mtf2Min { get; set; } = 15;
        [NinjaScriptProperty, Display(Name="MTF 3 (min)", GroupName="Multi-Timeframe", Order=2)] public int Mtf3Min { get; set; } = 60;

        [NinjaScriptProperty, Display(Name="Equal H/L tolerance (ticks)", GroupName="Liquidity", Order=0)] public int EqTolTicks { get; set; } = 4;
        [NinjaScriptProperty, Display(Name="Liquidity lookback bars",     GroupName="Liquidity", Order=1)] public int LiqLookback { get; set; } = 50;
        [NinjaScriptProperty, Display(Name="Max distance to target (ATR)",GroupName="Liquidity", Order=2)] public double LiqNearAtr { get; set; } = 1.5;
        [NinjaScriptProperty, Display(Name="Draw liquidity lines",        GroupName="Liquidity", Order=3)] public bool DrawLiqLines { get; set; } = true;
        [XmlIgnore, Display(Name="PDH/PDL line color", GroupName="Liquidity", Order=4)] public System.Windows.Media.Brush PdColor { get; set; } = System.Windows.Media.Brushes.Yellow;
        [Browsable(false)] public string PdColorSerialize { get { return Serialize.BrushToString(PdColor); } set { PdColor = Serialize.StringToBrush(value); } }
        [XmlIgnore, Display(Name="ONH/ONL line color", GroupName="Liquidity", Order=5)] public System.Windows.Media.Brush OnColor { get; set; } = System.Windows.Media.Brushes.Cyan;
        [Browsable(false)] public string OnColorSerialize { get { return Serialize.BrushToString(OnColor); } set { OnColor = Serialize.StringToBrush(value); } }
        [XmlIgnore, Display(Name="PMH/PML line color", GroupName="Liquidity", Order=6)] public System.Windows.Media.Brush PmColor { get; set; } = System.Windows.Media.Brushes.Orange;
        [Browsable(false)] public string PmColorSerialize { get { return Serialize.BrushToString(PmColor); } set { PmColor = Serialize.StringToBrush(value); } }
        [NinjaScriptProperty, Display(Name="Line width", GroupName="Liquidity", Order=7)] public int LiqLineWidth { get; set; } = 1;

        [NinjaScriptProperty, Display(Name="Absorption vol multiplier",    GroupName="Absorption", Order=0)] public double AbsVolMult { get; set; } = 1.8;
        [NinjaScriptProperty, Display(Name="Absorption max range vs ATR",  GroupName="Absorption", Order=1)] public double AbsRangeMult { get; set; } = 0.7;
        [NinjaScriptProperty, Display(Name="Absorption min wick % range",  GroupName="Absorption", Order=2)] public double AbsWickPct { get; set; } = 0.55;
        [NinjaScriptProperty, Display(Name="Delta absorption threshold",   GroupName="Absorption", Order=3)] public double AbsDeltaThreshold { get; set; } = 1500;
        [NinjaScriptProperty, Display(Name="Absorption max price move (ticks)", GroupName="Absorption", Order=4)] public int AbsMaxTicksMove { get; set; } = 3;

        [NinjaScriptProperty, Display(Name="Premarket start (HHmm ET)", GroupName="Sessions", Order=0)] public int PreStart { get; set; } = 400;
        [NinjaScriptProperty, Display(Name="Premarket end (HHmm ET)",   GroupName="Sessions", Order=1)] public int PreEnd { get; set; } = 930;
        [NinjaScriptProperty, Display(Name="RTH start (HHmm ET)",       GroupName="Sessions", Order=2)] public int RthStart { get; set; } = 930;
        [NinjaScriptProperty, Display(Name="RTH end (HHmm ET)",         GroupName="Sessions", Order=3)] public int RthEnd { get; set; } = 1600;

		[NinjaScriptProperty, Display(Name="Position",  GroupName="Table Display", Order=0)] public NinjaTrader.NinjaScript.Indicators.TablePosition Position { get; set; } = NinjaTrader.NinjaScript.Indicators.TablePosition.TopRight;
		[NinjaScriptProperty, Display(Name="Font Size", GroupName="Table Display", Order=1)] public NinjaTrader.NinjaScript.Indicators.FontSizeEnum FontSize { get; set; } = NinjaTrader.NinjaScript.Indicators.FontSizeEnum.Normal;
        [NinjaScriptProperty, Display(Name="Hide neutral rows",  GroupName="Table Display", Order=2)] public bool HideNeutral { get; set; } = true;
        [NinjaScriptProperty, Display(Name="Show BIAS row",      GroupName="Table Display", Order=3)] public bool ShowBiasRow { get; set; } = true;
        [NinjaScriptProperty, Display(Name="Max rows shown",     GroupName="Table Display", Order=4)] public int MaxRows { get; set; } = 14;
        [NinjaScriptProperty, Display(Name="Background opacity (0-1)", GroupName="Table Display", Order=5)] public double BgOpacity { get; set; } = 0.75;

        [NinjaScriptProperty, Display(Name="Enable alerts",                GroupName="Alerts", Order=0)] public bool EnableAlerts { get; set; } = false;
        [NinjaScriptProperty, Display(Name="Alert confidence threshold %", GroupName="Alerts", Order=1)] public int AlertConfPct { get; set; } = 60;
        [NinjaScriptProperty, Display(Name="Alert cooldown (seconds)",     GroupName="Alerts", Order=2)] public int AlertCooldownSec { get; set; } = 30;
        #endregion

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description   = "Multi-signal confluence table + liquidity levels for ES/NQ intraday.";
                Name          = "Futures Confluence Table";
                Calculate     = Calculate.OnBarClose;
                IsOverlay     = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                PaintPriceMarkers = false;
                IsSuspendedWhileInactive = true;
            }
            else if (State == State.Configure)
            {
                AddDataSeries(BarsPeriodType.Tick, 1); _tickSeries = 1;
                AddDataSeries(BarsPeriodType.Minute, Mtf1Min); _mtf1 = 2;
                AddDataSeries(BarsPeriodType.Minute, Mtf2Min); _mtf2 = 3;
                AddDataSeries(BarsPeriodType.Minute, Mtf3Min); _mtf3 = 4;
            }
            else if (State == State.DataLoaded)
            {
                _emaF  = EMA(BarsArray[PRIMARY], EmaFastLen);
                _emaS  = EMA(BarsArray[PRIMARY], EmaSlowLen);
                _emaT  = EMA(BarsArray[PRIMARY], EmaTrendLen);
                _rsi   = RSI(BarsArray[PRIMARY], RsiLen, 1);
                _atr   = ATR(BarsArray[PRIMARY], 14);
                _volMA = SMA(VOL(BarsArray[PRIMARY]), 20);

                _mtf1EmaF = EMA(BarsArray[_mtf1], EmaFastLen); _mtf1EmaS = EMA(BarsArray[_mtf1], EmaSlowLen);
                _mtf2EmaF = EMA(BarsArray[_mtf2], EmaFastLen); _mtf2EmaS = EMA(BarsArray[_mtf2], EmaSlowLen);
                _mtf3EmaF = EMA(BarsArray[_mtf3], EmaFastLen); _mtf3EmaS = EMA(BarsArray[_mtf3], EmaSlowLen);
            }
            else if (State == State.Terminated)
            {
                DisposeBrushes();
                _textFormat?.Dispose();
            }
        }

        protected override void OnBarUpdate()
        {
            if (BarsInProgress == _tickSeries) { ProcessTick(); return; }
            if (BarsInProgress != PRIMARY) return;
            if (CurrentBars[PRIMARY] < Math.Max(EmaTrendLen, LiqLookback) + 5) return;
            if (CurrentBars[_mtf1] < 5 || CurrentBars[_mtf2] < 5 || CurrentBars[_mtf3] < 5) return;

            // commit bar delta
            _barDelta.Add(_currentBarDelta);
            _cumulativeDelta += _currentBarDelta;
            _barCVD.Add(_cumulativeDelta);
            _currentBarDelta = 0;
            _barDeltaHigh = 0; _barDeltaLow = 0;

            if (_barDelta.Count > 500) { _barDelta.RemoveAt(0); _barCVD.RemoveAt(0); }

            UpdateSessionLevels();
            UpdateVwap();
            UpdatePivots();
            DrawLiquidityLines();
            BuildSignals();

            if (EnableAlerts) FireAlerts();
        }

        #region Delta from ticks
        private void ProcessTick()
        {
            double bid = GetCurrentBid(_tickSeries);
            double ask = GetCurrentAsk(_tickSeries);
            double px  = Closes[_tickSeries][0];
            long   v   = (long)Volumes[_tickSeries][0];
            if (px >= ask)      _currentBarDelta += v;
            else if (px <= bid) _currentBarDelta -= v;

            // track intra-bar delta extremes for absorption
            if (_currentBarDelta > _barDeltaHigh) _barDeltaHigh = _currentBarDelta;
            if (_currentBarDelta < _barDeltaLow)  _barDeltaLow  = _currentBarDelta;
        }
        #endregion

        #region Sessions
        private DateTime EtNow()
        {
            try
            {
                DateTime utc = Times[PRIMARY][0].ToUniversalTime();
                return TimeZoneInfo.ConvertTimeFromUtc(utc, TimeZoneInfo.FindSystemTimeZoneById("Eastern Standard Time"));
            }
            catch { return Times[PRIMARY][0]; }
        }

        private static bool InRange(int hhmm, int start, int end) => hhmm >= start && hhmm < end;
        private bool IsInRTH()    { var et = EtNow(); return InRange(et.Hour*100+et.Minute, RthStart, RthEnd); }
        private bool IsInPre()    { var et = EtNow(); return InRange(et.Hour*100+et.Minute, PreStart, PreEnd); }
        private bool IsInGlobex() => !IsInRTH() && !IsInPre();

        private void UpdateSessionLevels()
        {
            DateTime et = EtNow();
            DateTime day = et.Date;
            double h = Highs[PRIMARY][0], l = Lows[PRIMARY][0], o = Opens[PRIMARY][0];

            bool inRth    = IsInRTH();
            bool inPre    = IsInPre();
            bool inGlobex = IsInGlobex();

            // Rollover at first RTH bar of a new day
            if (inRth && day != _lastRthDate)
            {
                if (!double.IsNaN(_sessRthH)) { _pdh = _sessRthH; _pdl = _sessRthL; }
                if (!double.IsNaN(_sessOnH))  { _onh = _sessOnH;  _onl = _sessOnL;  }
                if (!double.IsNaN(_sessPmH))  { _pmh = _sessPmH;  _pml = _sessPmL;  }
                _sessRthH = h; _sessRthL = l; _sessRthO = o;
                _sessOnH = double.NaN; _sessOnL = double.NaN;
                _sessPmH = double.NaN; _sessPmL = double.NaN;
                _lastRthDate = day;
            }
            else if (inRth)
            {
                _sessRthH = double.IsNaN(_sessRthH) ? h : Math.Max(_sessRthH, h);
                _sessRthL = double.IsNaN(_sessRthL) ? l : Math.Min(_sessRthL, l);
                if (double.IsNaN(_sessRthO)) _sessRthO = o;
            }
            else if (inPre)
            {
                _sessPmH = double.IsNaN(_sessPmH) ? h : Math.Max(_sessPmH, h);
                _sessPmL = double.IsNaN(_sessPmL) ? l : Math.Min(_sessPmL, l);
            }
            else if (inGlobex)
            {
                _sessOnH = double.IsNaN(_sessOnH) ? h : Math.Max(_sessOnH, h);
                _sessOnL = double.IsNaN(_sessOnL) ? l : Math.Min(_sessOnL, l);
            }

            _rthH = _sessRthH; _rthL = _sessRthL; _rthOpen = _sessRthO;
        }
        #endregion

        #region VWAP
        private void UpdateVwap()
        {
            DateTime et = EtNow();
            DateTime anchor = et.Date;
            if (IsInRTH() && _vwapAnchorDate != anchor)
            {
                _vwapNum = 0; _vwapDen = 0;
                _vwapAnchorDate = anchor;
            }
            double typical = (Highs[PRIMARY][0] + Lows[PRIMARY][0] + Closes[PRIMARY][0]) / 3.0;
            double v = Volumes[PRIMARY][0];
            _vwapNum += typical * v;
            _vwapDen += v;
            _vwapValue = _vwapDen > 0 ? _vwapNum / _vwapDen : Closes[PRIMARY][0];
        }
        #endregion

        #region Pivots
        private void UpdatePivots()
        {
            int s = _pivotStrength;
            if (CurrentBar < s * 2 + 1) return;
            int idx = s; // bar `s` back is the potential pivot
            double h = Highs[PRIMARY][idx];
            double l = Lows[PRIMARY][idx];
            bool isPH = true, isPL = true;
            for (int k = 1; k <= s; k++)
            {
                if (Highs[PRIMARY][idx - k] >= h || Highs[PRIMARY][idx + k] >= h) isPH = false;
                if (Lows[PRIMARY][idx - k]  <= l || Lows[PRIMARY][idx + k]  <= l) isPL = false;
            }
            if (isPH) { _prevPH = _lastPH; _lastPH = h; }
            if (isPL) { _prevPL = _lastPL; _lastPL = l; }
        }
        #endregion

        #region Liquidity lines
        private void DrawLiquidityLines()
        {
            if (!DrawLiqLines) return;

            // remove and redraw each bar so lines extend to current price
            DrawLevelLine(TAG_PDH, _pdh, "PDH", PdColor);
            DrawLevelLine(TAG_PDL, _pdl, "PDL", PdColor);
            DrawLevelLine(TAG_ONH, _onh, "ONH", OnColor);
            DrawLevelLine(TAG_ONL, _onl, "ONL", OnColor);
            DrawLevelLine(TAG_PMH, _pmh, "PMH", PmColor);
            DrawLevelLine(TAG_PML, _pml, "PML", PmColor);
        }

        private void DrawLevelLine(string tag, double price, string label, System.Windows.Media.Brush brush)
        {
            if (double.IsNaN(price)) { RemoveDrawObject(tag); RemoveDrawObject(tag + "_lbl"); return; }

            // Line from ~50 bars ago to +10 bars right
            int startBarsAgo = Math.Min(CurrentBar, 200);
            Draw.Line(this, tag, false, startBarsAgo, price, -10, price, brush, DashStyleHelper.Solid, LiqLineWidth);

            // Text label on the right
            Draw.Text(this, tag + "_lbl", label + " " + price.ToString(Bars.Instrument.MasterInstrument.FormatPrice(price)),
                -8, price, brush);
        }
        #endregion

        #region Signal detectors + confluence scoring
        private double AvgVol()
        {
            double v = _volMA[0];
            return v <= 0 ? 1 : v;
        }

        private void BuildSignals()
        {
            _rows.Clear();
            _bullScore = 0; _bearScore = 0;

            double c   = Closes[PRIMARY][0];
            double o   = Opens[PRIMARY][0];
            double h   = Highs[PRIMARY][0];
            double l   = Lows[PRIMARY][0];
            double c1  = Closes[PRIMARY][1];
            double o1  = Opens[PRIMARY][1];
            double h1  = Highs[PRIMARY][1];
            double l1  = Lows[PRIMARY][1];
            double rng = h - l;
            double atr = _atr[0];
            double tick = Bars.Instrument.MasterInstrument.TickSize;
            double vol  = Volumes[PRIMARY][0];
            double volAvg = AvgVol();
            double delta = _barDelta.Count > 0 ? _barDelta[_barDelta.Count - 1] : 0;

            // -------------------- Regime / trend --------------------
            bool trendUp   = _emaF[0] > _emaS[0] && _emaS[0] > _emaT[0] && c > _emaT[0];
            bool trendDown = _emaF[0] < _emaS[0] && _emaS[0] < _emaT[0] && c < _emaT[0];
            int regimeDir = trendUp ? 1 : trendDown ? -1 : 0;

            double atrExp = _atr[0] / SMA(_atr, 50)[0];
            bool chop     = Math.Abs(_emaF[0] - _emaT[0]) / (atr > 0 ? atr : 1) < 0.5 && atrExp < 1.1;
            bool squeeze  = atrExp < 0.85;

            if (regimeDir != 0)   AddRow("Regime", regimeDir, ConfLabel(70), regimeDir > 0 ? "Uptrend stacked" : "Downtrend stacked");
            else if (chop)        AddRow("Regime", 0, "Med", "Chop / balanced");
            else if (squeeze)     AddRow("Regime", 0, "Med", "Volatility squeeze");

            // -------------------- MTF alignment --------------------
            int m1 = _mtf1EmaF[0] > _mtf1EmaS[0] ? 1 : _mtf1EmaF[0] < _mtf1EmaS[0] ? -1 : 0;
            int m2 = _mtf2EmaF[0] > _mtf2EmaS[0] ? 1 : _mtf2EmaF[0] < _mtf2EmaS[0] ? -1 : 0;
            int m3 = _mtf3EmaF[0] > _mtf3EmaS[0] ? 1 : _mtf3EmaF[0] < _mtf3EmaS[0] ? -1 : 0;
            int mtfAlign = m1 + m2 + m3;
            string mtfNote = $"{Mtf1Min}m:{Arrow(m1)} {Mtf2Min}m:{Arrow(m2)} {Mtf3Min}m:{Arrow(m3)}";
            if (Math.Abs(mtfAlign) >= 2)
                AddRow("MTF Align", mtfAlign > 0 ? 1 : -1, ConfLabel(Math.Abs(mtfAlign) * 33), mtfNote);

            // -------------------- VWAP --------------------
            double vwapDist = atr > 0 ? (c - _vwapValue) / atr : 0;
            bool aboveVwap = c > _vwapValue;
            if (Math.Abs(vwapDist) > 0.3)
                AddRow("VWAP", aboveVwap ? 1 : -1, ConfLabel((int)Math.Min(100, Math.Abs(vwapDist) * 40)),
                    $"Dist {vwapDist:F2} ATR");

            bool vwapReclaim = c > _vwapValue && c1 <= _vwapValue;
            bool vwapReject  = c < _vwapValue && c1 >= _vwapValue;
            if (vwapReclaim) AddRow("VWAP Reclaim", 1,  "High", "Cross above VWAP");
            if (vwapReject)  AddRow("VWAP Reject",  -1, "High", "Cross below VWAP");

            // -------------------- RSI --------------------
            double r = _rsi[0];
            bool rsiOB = r >= RsiOB, rsiOS = r <= RsiOS;
            bool rsiBull = r > 50 && !rsiOB;
            bool rsiBear = r < 50 && !rsiOS;
            if (rsiOB)      AddRow("RSI", -1, "Med", $"RSI {r:F1} OB");
            else if (rsiOS) AddRow("RSI",  1, "Med", $"RSI {r:F1} OS");

            // Simple 5-bar RSI divergence
            double loLow = Lows[PRIMARY][1];
            double hiHigh = Highs[PRIMARY][1];
            double loRsi = _rsi[1], hiRsi = _rsi[1];
            for (int k = 2; k <= 5 && k < CurrentBar; k++)
            {
                if (Lows[PRIMARY][k]  < loLow)  loLow  = Lows[PRIMARY][k];
                if (Highs[PRIMARY][k] > hiHigh) hiHigh = Highs[PRIMARY][k];
                if (_rsi[k] < loRsi) loRsi = _rsi[k];
                if (_rsi[k] > hiRsi) hiRsi = _rsi[k];
            }
            bool rsiDivBull = l < loLow && r > loRsi;
            bool rsiDivBear = h > hiHigh && r < hiRsi;
            if (rsiDivBull) AddRow("RSI Divergence", 1,  "High", "Bull div vs price");
            if (rsiDivBear) AddRow("RSI Divergence", -1, "High", "Bear div vs price");

            // -------------------- Real Delta divergence (CVD) --------------------
            // Compare last 10-bar price extreme vs CVD extreme
            if (_barCVD.Count >= 10)
            {
                int n = _barCVD.Count;
                double priceHH10 = h; double priceLL10 = l;
                double cvdAtHH = _barCVD[n-1]; double cvdAtLL = _barCVD[n-1];
                double lastCvd = _barCVD[n-1];
                double prevMaxCvd = double.MinValue, prevMinCvd = double.MaxValue;
                double prevMaxPx = double.MinValue,  prevMinPx = double.MaxValue;
                for (int k = 1; k <= 10 && k < CurrentBar; k++)
                {
                    if (Highs[PRIMARY][k] > prevMaxPx) prevMaxPx = Highs[PRIMARY][k];
                    if (Lows[PRIMARY][k]  < prevMinPx) prevMinPx = Lows[PRIMARY][k];
                    int cvdIdx = n - 1 - k;
                    if (cvdIdx >= 0)
                    {
                        if (_barCVD[cvdIdx] > prevMaxCvd) prevMaxCvd = _barCVD[cvdIdx];
                        if (_barCVD[cvdIdx] < prevMinCvd) prevMinCvd = _barCVD[cvdIdx];
                    }
                }
                bool bearCvdDiv = h > prevMaxPx && lastCvd < prevMaxCvd;
                bool bullCvdDiv = l < prevMinPx && lastCvd > prevMinCvd;
                if (bullCvdDiv) AddRow("Δ Divergence", 1,  "High", "Price LL, CVD higher");
                if (bearCvdDiv) AddRow("Δ Divergence", -1, "High", "Price HH, CVD lower");
            }

            // -------------------- Absorption (REAL, tick-based) --------------------
            double upperWick = h - Math.Max(o, c);
            double lowerWick = Math.Min(o, c) - l;
            bool bigVol      = vol > volAvg * AbsVolMult;
            bool smallRange  = rng < atr * AbsRangeMult;
            bool absorbSellers = bigVol && smallRange && lowerWick > rng * AbsWickPct;
            bool absorbBuyers  = bigVol && smallRange && upperWick > rng * AbsWickPct;

            // Delta absorption: heavy delta pressure but price barely moved
            double barMove = Math.Abs(c - o) / tick;
            bool deltaAbsorbBull = delta < -AbsDeltaThreshold && barMove <= AbsMaxTicksMove && c >= o;
            bool deltaAbsorbBear = delta >  AbsDeltaThreshold && barMove <= AbsMaxTicksMove && c <= o;

            if (absorbSellers)    AddRow("Absorption", 1,  "High", "Buyers absorbing supply (wick)");
            if (absorbBuyers)     AddRow("Absorption", -1, "High", "Sellers absorbing demand (wick)");
            if (deltaAbsorbBull)  AddRow("Δ Absorption", 1,  "High", $"Heavy sell delta, no move ({delta:F0})");
            if (deltaAbsorbBear)  AddRow("Δ Absorption", -1, "High", $"Heavy buy delta, no move ({delta:F0})");

            // -------------------- Liquidity target + engineered liquidity --------------------
            var targets = new List<(double lvl, string name)>();
            if (!double.IsNaN(_pdh)) targets.Add((_pdh, "PDH"));
            if (!double.IsNaN(_pdl)) targets.Add((_pdl, "PDL"));
            if (!double.IsNaN(_onh)) targets.Add((_onh, "ONH"));
            if (!double.IsNaN(_onl)) targets.Add((_onl, "ONL"));
            if (!double.IsNaN(_pmh)) targets.Add((_pmh, "PMH"));
            if (!double.IsNaN(_pml)) targets.Add((_pml, "PML"));

            double nearestDist = double.MaxValue;
            double nearestLvl = double.NaN;
            string nearestName = "";
            foreach (var t in targets)
            {
                double d = Math.Abs(c - t.lvl);
                if (d < nearestDist) { nearestDist = d; nearestLvl = t.lvl; nearestName = t.name; }
            }
            bool liqNear = !double.IsNaN(nearestLvl) && atr > 0 && nearestDist < atr * LiqNearAtr;
            if (liqNear)
            {
                int side = nearestLvl > c ? -1 : 1;
                AddRow("Liq Target", side, "Med",
                    $"{nearestName} @ {nearestLvl.ToString(Bars.Instrument.MasterInstrument.FormatPrice(nearestLvl))} ({nearestDist/atr:F2} ATR)");
            }

            // Equal H/L (resting liq)
            double eqTol = tick * EqTolTicks;
            bool eqHighs = !double.IsNaN(_lastPH) && !double.IsNaN(_prevPH) && Math.Abs(_lastPH - _prevPH) <= eqTol;
            bool eqLows  = !double.IsNaN(_lastPL) && !double.IsNaN(_prevPL) && Math.Abs(_lastPL - _prevPL) <= eqTol;
            if (eqHighs) AddRow("Resting Liq", -1, "Med", $"Equal highs @ {_lastPH:F2}");
            if (eqLows)  AddRow("Resting Liq",  1, "Med", $"Equal lows @ {_lastPL:F2}");

            // Engineered liquidity: sweep + reversal
            bool sweepHigh = !double.IsNaN(_lastPH) && h > _lastPH && c < _lastPH;
            bool sweepLow  = !double.IsNaN(_lastPL) && l < _lastPL && c > _lastPL;
            bool engLiqBear = eqHighs && sweepHigh;
            bool engLiqBull = eqLows  && sweepLow;
            if (engLiqBull) AddRow("Engineered Liq", 1,  "High", "Swept eq-lows + reclaim");
            if (engLiqBear) AddRow("Engineered Liq", -1, "High", "Swept eq-highs + rejection");

            // Also flag sweeps of session extremes
            bool sweepOnhRev = !double.IsNaN(_onh) && h > _onh && c < _onh;
            bool sweepOnlRev = !double.IsNaN(_onl) && l < _onl && c > _onl;
            bool sweepPmhRev = !double.IsNaN(_pmh) && h > _pmh && c < _pmh;
            bool sweepPmlRev = !double.IsNaN(_pml) && l < _pml && c > _pml;
            if (sweepOnhRev || sweepPmhRev) AddRow("Session Sweep", -1, "High", $"Swept {(sweepOnhRev?"ONH":"PMH")} & rejected");
            if (sweepOnlRev || sweepPmlRev) AddRow("Session Sweep",  1, "High", $"Swept {(sweepOnlRev?"ONL":"PML")} & reclaimed");

            // -------------------- Price action patterns --------------------
            bool bullEngulf = c > o && c1 < o1 && c > o1 && o < c1;
            bool bearEngulf = c < o && c1 > o1 && c < o1 && o > c1;
            bool hammer     = rng > 0 && lowerWick > rng * 0.6 && (h - c) < rng * 0.2;
            bool shooter    = rng > 0 && upperWick > rng * 0.6 && (c - l) < rng * 0.2;
            bool insideBar  = h < h1 && l > l1;
            if (bullEngulf) AddRow("PA Pattern", 1,  "Med", "Bullish engulfing");
            if (bearEngulf) AddRow("PA Pattern", -1, "Med", "Bearish engulfing");
            if (hammer)     AddRow("PA Pattern", 1,  "Low", "Hammer / rejection wick");
            if (shooter)    AddRow("PA Pattern", -1, "Low", "Shooting star");

            // -------------------- AMD phase --------------------
            string amdPhase = "";
            int amdDir = 0;
            bool sweptOnh = IsInRTH() && !double.IsNaN(_onh) && h > _onh && c < _onh;
            bool sweptOnl = IsInRTH() && !double.IsNaN(_onl) && l < _onl && c > _onl;
            bool sweptPmh = IsInRTH() && !double.IsNaN(_pmh) && h > _pmh && c < _pmh;
            bool sweptPml = IsInRTH() && !double.IsNaN(_pml) && l < _pml && c > _pml;

            if (IsInGlobex() && atrExp < 0.9) amdPhase = "Accumulation (Globex)";
            if (sweptOnh || sweptPmh) { amdPhase = "Manipulation (Buyside swept)"; amdDir = -1; }
            if (sweptOnl || sweptPml) { amdPhase = "Manipulation (Sellside swept)"; amdDir = 1; }
            if (IsInRTH() && trendUp   && mtfAlign >= 2 && !(sweptOnh || sweptPmh)) { amdPhase = "Distribution (Up)";   amdDir = 1; }
            if (IsInRTH() && trendDown && mtfAlign <= -2 && !(sweptOnl || sweptPml)) { amdPhase = "Distribution (Down)"; amdDir = -1; }
            if (amdPhase != "")
            {
                string cf = amdPhase.Contains("Manipulation") || amdPhase.Contains("Distribution") ? "High" : "Med";
                AddRow("AMD Phase", amdDir, cf, amdPhase);
            }

            // -------------------- Reversal / Continuation setup summary --------------------
            bool reversalBull = (absorbSellers || deltaAbsorbBull || rsiDivBull || engLiqBull || sweepOnlRev || sweepPmlRev || hammer) && c > _emaF[0];
            bool reversalBear = (absorbBuyers  || deltaAbsorbBear || rsiDivBear || engLiqBear || sweepOnhRev || sweepPmhRev || shooter) && c < _emaF[0];
            bool contBull = trendUp   && aboveVwap  && mtfAlign >=  2 && rsiBull;
            bool contBear = trendDown && !aboveVwap && mtfAlign <= -2 && rsiBear;
            if (reversalBull) AddRow("Setup", 1,  "High", "Reversal long conditions");
            if (reversalBear) AddRow("Setup", -1, "High", "Reversal short conditions");
            if (contBull)     AddRow("Setup", 1,  "High", "Trend continuation long");
            if (contBear)     AddRow("Setup", -1, "High", "Trend continuation short");

            // -------------------- Score --------------------
            _bullScore += trendUp ? 2 : 0;         _bearScore += trendDown ? 2 : 0;
            _bullScore += aboveVwap ? 1 : 0;       _bearScore += !aboveVwap ? 1 : 0;
            _bullScore += mtfAlign > 0 ? mtfAlign : 0;  _bearScore += mtfAlign < 0 ? -mtfAlign : 0;
            _bullScore += rsiBull ? 1 : 0;         _bearScore += rsiBear ? 1 : 0;
            _bullScore += (absorbSellers || deltaAbsorbBull) ? 3 : 0;  _bearScore += (absorbBuyers || deltaAbsorbBear) ? 3 : 0;
            _bullScore += (engLiqBull || sweepOnlRev || sweepPmlRev) ? 3 : 0;
            _bearScore += (engLiqBear || sweepOnhRev || sweepPmhRev) ? 3 : 0;
            _bullScore += (bullEngulf || hammer) ? 1 : 0;      _bearScore += (bearEngulf || shooter) ? 1 : 0;
            _bullScore += amdDir > 0 ? 2 : 0;                  _bearScore += amdDir < 0 ? 2 : 0;

            int net = _bullScore - _bearScore;
            const double maxPossible = 20.0;
            _confPct = (int)Math.Min(100, Math.Round(Math.Abs(net) / maxPossible * 100));
            _biasDir = net > 2 ? 1 : net < -2 ? -1 : 0;
            _biasTxt = _biasDir > 0 ? "BULLISH" : _biasDir < 0 ? "BEARISH" : "NEUTRAL";
        }

        private void AddRow(string name, int dirCode, string conf, string note)
        {
            if (HideNeutral && dirCode == 0 && !(conf == "High" || conf == "Med")) return;
            string dir = dirCode > 0 ? "▲" : dirCode < 0 ? "▼" : "•";
            _rows.Add(new SignalRow { Name = name, Dir = dir, Conf = conf, Note = note, DirCode = dirCode });
        }

        private static string ConfLabel(int pct) => pct >= 70 ? "High" : pct >= 40 ? "Med" : "Low";
        private static string Arrow(int d) => d > 0 ? "↑" : d < 0 ? "↓" : "·";

        private void FireAlerts()
        {
            if (_confPct < AlertConfPct) return;
            if ((DateTime.Now - _lastAlertTime).TotalSeconds < AlertCooldownSec) return;
            foreach (var row in _rows)
            {
                if (row.Name == "Setup")
                {
                    string prio = row.DirCode > 0 ? Priority.High.ToString() : Priority.High.ToString();
                    Alert(row.Name + "_" + row.DirCode, Priority.High, $"{_biasTxt} {_confPct}% – {row.Note}",
                        NinjaTrader.Core.Globals.InstallDir + @"\sounds\Alert2.wav", 10, System.Windows.Media.Brushes.Black,
                        row.DirCode > 0 ? System.Windows.Media.Brushes.LimeGreen : System.Windows.Media.Brushes.Red);
                    _lastAlertTime = DateTime.Now;
                }
            }
        }
        #endregion

        #region Rendering (SharpDX table)
        public override void OnRenderTargetChanged()
        {
            DisposeBrushes();
            if (RenderTarget == null) return;

            float bg = (float)Math.Max(0, Math.Min(1, BgOpacity));

            _bWhite       = new SolidColorBrush(RenderTarget, new Color4(1f, 1f, 1f, 1f));
            _bGray        = new SolidColorBrush(RenderTarget, new Color4(0.75f, 0.75f, 0.75f, 1f));
            _bGreen       = new SolidColorBrush(RenderTarget, new Color4(0.30f, 0.85f, 0.40f, 1f));
            _bRed         = new SolidColorBrush(RenderTarget, new Color4(0.95f, 0.35f, 0.35f, 1f));
            _bNeutral     = new SolidColorBrush(RenderTarget, new Color4(0.75f, 0.75f, 0.75f, 1f));

            _bHeaderBg    = new SolidColorBrush(RenderTarget, new Color4(0.10f, 0.10f, 0.12f, bg));
            _bRowBull     = new SolidColorBrush(RenderTarget, new Color4(0.10f, 0.28f, 0.14f, bg));
            _bRowBear     = new SolidColorBrush(RenderTarget, new Color4(0.32f, 0.10f, 0.10f, bg));
            _bRowNeutral  = new SolidColorBrush(RenderTarget, new Color4(0.15f, 0.15f, 0.17f, bg));

            _bBiasBull    = new SolidColorBrush(RenderTarget, new Color4(0.15f, 0.55f, 0.22f, Math.Min(1f, bg + 0.15f)));
            _bBiasBear    = new SolidColorBrush(RenderTarget, new Color4(0.65f, 0.18f, 0.18f, Math.Min(1f, bg + 0.15f)));
            _bBiasNeutral = new SolidColorBrush(RenderTarget, new Color4(0.28f, 0.28f, 0.30f, Math.Min(1f, bg + 0.15f)));

            _bBorder      = new SolidColorBrush(RenderTarget, new Color4(0.35f, 0.35f, 0.38f, 1f));
        }

        private void DisposeBrushes()
        {
            _bWhite?.Dispose();       _bWhite = null;
            _bGray?.Dispose();        _bGray = null;
            _bGreen?.Dispose();       _bGreen = null;
            _bRed?.Dispose();         _bRed = null;
            _bNeutral?.Dispose();     _bNeutral = null;
            _bHeaderBg?.Dispose();    _bHeaderBg = null;
            _bRowBull?.Dispose();     _bRowBull = null;
            _bRowBear?.Dispose();     _bRowBear = null;
            _bRowNeutral?.Dispose();  _bRowNeutral = null;
            _bBiasBull?.Dispose();    _bBiasBull = null;
            _bBiasBear?.Dispose();    _bBiasBear = null;
            _bBiasNeutral?.Dispose(); _bBiasNeutral = null;
            _bBorder?.Dispose();      _bBorder = null;
        }

        private SharpDX.DirectWrite.TextFormat GetTextFormat()
        {
            if (_textFormat == null || (int)_textFormat.FontSize != (int)FontSize)
            {
                _textFormat?.Dispose();
                _textFormat = new SharpDX.DirectWrite.TextFormat(
                    NinjaTrader.Core.Globals.DirectWriteFactory,
                    "Segoe UI",
                    SharpDX.DirectWrite.FontWeight.SemiBold,
                    SharpDX.DirectWrite.FontStyle.Normal,
                    (float)FontSize);
            }
            return _textFormat;
        }

        protected override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (RenderTarget == null || _bWhite == null) return;
            if (_rows == null) return;

            var fmt = GetTextFormat();

            float padX = 8f;
            float padY = 4f;
            float rowH = (float)FontSize + padY * 2;

            string[] headers = new[] { "Signal", "Dir", "Conf", "Note" };
            float[] colW = new float[4];
            for (int i = 0; i < 4; i++) colW[i] = MeasureText(headers[i], fmt) + padX * 2;

            int shownRows = Math.Min(_rows.Count, MaxRows);
            for (int i = 0; i < shownRows; i++)
            {
                var r = _rows[i];
                colW[0] = Math.Max(colW[0], MeasureText(r.Name, fmt) + padX * 2);
                colW[1] = Math.Max(colW[1], MeasureText(r.Dir,  fmt) + padX * 2);
                colW[2] = Math.Max(colW[2], MeasureText(r.Conf, fmt) + padX * 2);
                colW[3] = Math.Max(colW[3], MeasureText(r.Note, fmt) + padX * 2);
            }

            string biasCell = $"BIAS  {_biasTxt}  {ConfLabel(_confPct)} {_confPct}%   Bull {_bullScore}/Bear {_bearScore}";
            float biasW = ShowBiasRow ? MeasureText(biasCell, fmt) + padX * 2 : 0;

            float totalW = Math.Max(colW.Sum(), biasW);
            if (biasW > colW.Sum()) colW[3] += (biasW - colW.Sum());

            int totalRows = shownRows + 1 + (ShowBiasRow ? 1 : 0);
            if (shownRows == 0) totalRows++;
            float totalH = totalRows * rowH;

            float chartW = (float)ChartPanel.W;
            float chartH = (float)ChartPanel.H;
            float chartX = (float)ChartPanel.X;
            float chartY = (float)ChartPanel.Y;
            float margin = 8f;

            float x = chartX + margin, y = chartY + margin;
            switch (Position)
            {
                case NinjaTrader.NinjaScript.Indicators.TablePosition.TopLeft:      x = chartX + margin;                   y = chartY + margin; break;
                case NinjaTrader.NinjaScript.Indicators.TablePosition.TopCenter:    x = chartX + (chartW - totalW) / 2f;   y = chartY + margin; break;
                case NinjaTrader.NinjaScript.Indicators.TablePosition.TopRight:     x = chartX + chartW - totalW - margin; y = chartY + margin; break;
                case NinjaTrader.NinjaScript.Indicators.TablePosition.MiddleLeft:   x = chartX + margin;                   y = chartY + (chartH - totalH) / 2f; break;
                case NinjaTrader.NinjaScript.Indicators.TablePosition.MiddleRight:  x = chartX + chartW - totalW - margin; y = chartY + (chartH - totalH) / 2f; break;
                case NinjaTrader.NinjaScript.Indicators.TablePosition.BottomLeft:   x = chartX + margin;                   y = chartY + chartH - totalH - margin; break;
                case NinjaTrader.NinjaScript.Indicators.TablePosition.BottomCenter: x = chartX + (chartW - totalW) / 2f;   y = chartY + chartH - totalH - margin; break;
                case NinjaTrader.NinjaScript.Indicators.TablePosition.BottomRight:  x = chartX + chartW - totalW - margin; y = chartY + chartH - totalH - margin; break;
            }

            float rowY = y;

            // BIAS row
            if (ShowBiasRow)
            {
                var biasBrush = _biasDir > 0 ? _bBiasBull : _biasDir < 0 ? _bBiasBear : _bBiasNeutral;
                var biasRect  = new RectangleF(x, rowY, totalW, rowH);
                RenderTarget.FillRectangle(biasRect, biasBrush);
                RenderTarget.DrawRectangle(biasRect, _bBorder, 1f);
                DrawTextInCell(biasCell, biasRect, fmt, _bWhite, padX);
                rowY += rowH;
            }

            // Header row
            float cellX = x;
            for (int i = 0; i < 4; i++)
            {
                var rect = new RectangleF(cellX, rowY, colW[i], rowH);
                RenderTarget.FillRectangle(rect, _bHeaderBg);
                RenderTarget.DrawRectangle(rect, _bBorder, 1f);
                DrawTextInCell(headers[i], rect, fmt, _bGray, padX);
                cellX += colW[i];
            }
            rowY += rowH;

            // Data rows
            if (shownRows == 0)
            {
                var rect = new RectangleF(x, rowY, totalW, rowH);
                RenderTarget.FillRectangle(rect, _bRowNeutral);
                RenderTarget.DrawRectangle(rect, _bBorder, 1f);
                DrawTextInCell("No active signals", rect, fmt, _bGray, padX);
            }
            else
            {
                for (int i = 0; i < shownRows; i++)
                {
                    var r = _rows[i];
                    var rowBg = r.DirCode > 0 ? _bRowBull : r.DirCode < 0 ? _bRowBear : _bRowNeutral;
                    var textBrush = r.DirCode > 0 ? _bGreen : r.DirCode < 0 ? _bRed : _bGray;

                    cellX = x;
                    string[] cells = new[] { r.Name, r.Dir, r.Conf, r.Note };
                    for (int c = 0; c < 4; c++)
                    {
                        var rect = new RectangleF(cellX, rowY, colW[c], rowH);
                        RenderTarget.FillRectangle(rect, rowBg);
                        RenderTarget.DrawRectangle(rect, _bBorder, 1f);
                        var brush = c == 1 ? textBrush : _bWhite;
                        DrawTextInCell(cells[c], rect, fmt, brush, padX);
                        cellX += colW[c];
                    }
                    rowY += rowH;
                }
            }
        }

        private float MeasureText(string text, SharpDX.DirectWrite.TextFormat fmt)
        {
            if (string.IsNullOrEmpty(text)) return 0;
            using (var layout = new SharpDX.DirectWrite.TextLayout(
                NinjaTrader.Core.Globals.DirectWriteFactory, text, fmt, 2000f, 100f))
            {
                return layout.Metrics.Width;
            }
        }

        private void DrawTextInCell(string text, RectangleF rect, SharpDX.DirectWrite.TextFormat fmt,
                                    SharpDX.Direct2D1.SolidColorBrush brush, float padX)
        {
            if (string.IsNullOrEmpty(text)) return;
            using (var layout = new SharpDX.DirectWrite.TextLayout(
                NinjaTrader.Core.Globals.DirectWriteFactory, text, fmt,
                rect.Width - padX * 2, rect.Height))
            {
                layout.ParagraphAlignment = SharpDX.DirectWrite.ParagraphAlignment.Center;
                RenderTarget.DrawTextLayout(new Vector2(rect.Left + padX, rect.Top), layout, brush);
            }
        }
        #endregion

    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private FuturesConfluenceTable[] cacheFuturesConfluenceTable;
		public FuturesConfluenceTable FuturesConfluenceTable(int emaFastLen, int emaSlowLen, int emaTrendLen, int rsiLen, int rsiOB, int rsiOS, int mtf1Min, int mtf2Min, int mtf3Min, int eqTolTicks, int liqLookback, double liqNearAtr, bool drawLiqLines, int liqLineWidth, double absVolMult, double absRangeMult, double absWickPct, double absDeltaThreshold, int absMaxTicksMove, int preStart, int preEnd, int rthStart, int rthEnd, NinjaTrader.NinjaScript.Indicators.TablePosition position, NinjaTrader.NinjaScript.Indicators.FontSizeEnum fontSize, bool hideNeutral, bool showBiasRow, int maxRows, double bgOpacity, bool enableAlerts, int alertConfPct, int alertCooldownSec)
		{
			return FuturesConfluenceTable(Input, emaFastLen, emaSlowLen, emaTrendLen, rsiLen, rsiOB, rsiOS, mtf1Min, mtf2Min, mtf3Min, eqTolTicks, liqLookback, liqNearAtr, drawLiqLines, liqLineWidth, absVolMult, absRangeMult, absWickPct, absDeltaThreshold, absMaxTicksMove, preStart, preEnd, rthStart, rthEnd, position, fontSize, hideNeutral, showBiasRow, maxRows, bgOpacity, enableAlerts, alertConfPct, alertCooldownSec);
		}

		public FuturesConfluenceTable FuturesConfluenceTable(ISeries<double> input, int emaFastLen, int emaSlowLen, int emaTrendLen, int rsiLen, int rsiOB, int rsiOS, int mtf1Min, int mtf2Min, int mtf3Min, int eqTolTicks, int liqLookback, double liqNearAtr, bool drawLiqLines, int liqLineWidth, double absVolMult, double absRangeMult, double absWickPct, double absDeltaThreshold, int absMaxTicksMove, int preStart, int preEnd, int rthStart, int rthEnd, NinjaTrader.NinjaScript.Indicators.TablePosition position, NinjaTrader.NinjaScript.Indicators.FontSizeEnum fontSize, bool hideNeutral, bool showBiasRow, int maxRows, double bgOpacity, bool enableAlerts, int alertConfPct, int alertCooldownSec)
		{
			if (cacheFuturesConfluenceTable != null)
				for (int idx = 0; idx < cacheFuturesConfluenceTable.Length; idx++)
					if (cacheFuturesConfluenceTable[idx] != null && cacheFuturesConfluenceTable[idx].EmaFastLen == emaFastLen && cacheFuturesConfluenceTable[idx].EmaSlowLen == emaSlowLen && cacheFuturesConfluenceTable[idx].EmaTrendLen == emaTrendLen && cacheFuturesConfluenceTable[idx].RsiLen == rsiLen && cacheFuturesConfluenceTable[idx].RsiOB == rsiOB && cacheFuturesConfluenceTable[idx].RsiOS == rsiOS && cacheFuturesConfluenceTable[idx].Mtf1Min == mtf1Min && cacheFuturesConfluenceTable[idx].Mtf2Min == mtf2Min && cacheFuturesConfluenceTable[idx].Mtf3Min == mtf3Min && cacheFuturesConfluenceTable[idx].EqTolTicks == eqTolTicks && cacheFuturesConfluenceTable[idx].LiqLookback == liqLookback && cacheFuturesConfluenceTable[idx].LiqNearAtr == liqNearAtr && cacheFuturesConfluenceTable[idx].DrawLiqLines == drawLiqLines && cacheFuturesConfluenceTable[idx].LiqLineWidth == liqLineWidth && cacheFuturesConfluenceTable[idx].AbsVolMult == absVolMult && cacheFuturesConfluenceTable[idx].AbsRangeMult == absRangeMult && cacheFuturesConfluenceTable[idx].AbsWickPct == absWickPct && cacheFuturesConfluenceTable[idx].AbsDeltaThreshold == absDeltaThreshold && cacheFuturesConfluenceTable[idx].AbsMaxTicksMove == absMaxTicksMove && cacheFuturesConfluenceTable[idx].PreStart == preStart && cacheFuturesConfluenceTable[idx].PreEnd == preEnd && cacheFuturesConfluenceTable[idx].RthStart == rthStart && cacheFuturesConfluenceTable[idx].RthEnd == rthEnd && cacheFuturesConfluenceTable[idx].Position == position && cacheFuturesConfluenceTable[idx].FontSize == fontSize && cacheFuturesConfluenceTable[idx].HideNeutral == hideNeutral && cacheFuturesConfluenceTable[idx].ShowBiasRow == showBiasRow && cacheFuturesConfluenceTable[idx].MaxRows == maxRows && cacheFuturesConfluenceTable[idx].BgOpacity == bgOpacity && cacheFuturesConfluenceTable[idx].EnableAlerts == enableAlerts && cacheFuturesConfluenceTable[idx].AlertConfPct == alertConfPct && cacheFuturesConfluenceTable[idx].AlertCooldownSec == alertCooldownSec && cacheFuturesConfluenceTable[idx].EqualsInput(input))
						return cacheFuturesConfluenceTable[idx];
			return CacheIndicator<FuturesConfluenceTable>(new FuturesConfluenceTable(){ EmaFastLen = emaFastLen, EmaSlowLen = emaSlowLen, EmaTrendLen = emaTrendLen, RsiLen = rsiLen, RsiOB = rsiOB, RsiOS = rsiOS, Mtf1Min = mtf1Min, Mtf2Min = mtf2Min, Mtf3Min = mtf3Min, EqTolTicks = eqTolTicks, LiqLookback = liqLookback, LiqNearAtr = liqNearAtr, DrawLiqLines = drawLiqLines, LiqLineWidth = liqLineWidth, AbsVolMult = absVolMult, AbsRangeMult = absRangeMult, AbsWickPct = absWickPct, AbsDeltaThreshold = absDeltaThreshold, AbsMaxTicksMove = absMaxTicksMove, PreStart = preStart, PreEnd = preEnd, RthStart = rthStart, RthEnd = rthEnd, Position = position, FontSize = fontSize, HideNeutral = hideNeutral, ShowBiasRow = showBiasRow, MaxRows = maxRows, BgOpacity = bgOpacity, EnableAlerts = enableAlerts, AlertConfPct = alertConfPct, AlertCooldownSec = alertCooldownSec }, input, ref cacheFuturesConfluenceTable);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.FuturesConfluenceTable FuturesConfluenceTable(int emaFastLen, int emaSlowLen, int emaTrendLen, int rsiLen, int rsiOB, int rsiOS, int mtf1Min, int mtf2Min, int mtf3Min, int eqTolTicks, int liqLookback, double liqNearAtr, bool drawLiqLines, int liqLineWidth, double absVolMult, double absRangeMult, double absWickPct, double absDeltaThreshold, int absMaxTicksMove, int preStart, int preEnd, int rthStart, int rthEnd, NinjaTrader.NinjaScript.Indicators.TablePosition position, NinjaTrader.NinjaScript.Indicators.FontSizeEnum fontSize, bool hideNeutral, bool showBiasRow, int maxRows, double bgOpacity, bool enableAlerts, int alertConfPct, int alertCooldownSec)
		{
			return indicator.FuturesConfluenceTable(Input, emaFastLen, emaSlowLen, emaTrendLen, rsiLen, rsiOB, rsiOS, mtf1Min, mtf2Min, mtf3Min, eqTolTicks, liqLookback, liqNearAtr, drawLiqLines, liqLineWidth, absVolMult, absRangeMult, absWickPct, absDeltaThreshold, absMaxTicksMove, preStart, preEnd, rthStart, rthEnd, position, fontSize, hideNeutral, showBiasRow, maxRows, bgOpacity, enableAlerts, alertConfPct, alertCooldownSec);
		}

		public Indicators.FuturesConfluenceTable FuturesConfluenceTable(ISeries<double> input , int emaFastLen, int emaSlowLen, int emaTrendLen, int rsiLen, int rsiOB, int rsiOS, int mtf1Min, int mtf2Min, int mtf3Min, int eqTolTicks, int liqLookback, double liqNearAtr, bool drawLiqLines, int liqLineWidth, double absVolMult, double absRangeMult, double absWickPct, double absDeltaThreshold, int absMaxTicksMove, int preStart, int preEnd, int rthStart, int rthEnd, NinjaTrader.NinjaScript.Indicators.TablePosition position, NinjaTrader.NinjaScript.Indicators.FontSizeEnum fontSize, bool hideNeutral, bool showBiasRow, int maxRows, double bgOpacity, bool enableAlerts, int alertConfPct, int alertCooldownSec)
		{
			return indicator.FuturesConfluenceTable(input, emaFastLen, emaSlowLen, emaTrendLen, rsiLen, rsiOB, rsiOS, mtf1Min, mtf2Min, mtf3Min, eqTolTicks, liqLookback, liqNearAtr, drawLiqLines, liqLineWidth, absVolMult, absRangeMult, absWickPct, absDeltaThreshold, absMaxTicksMove, preStart, preEnd, rthStart, rthEnd, position, fontSize, hideNeutral, showBiasRow, maxRows, bgOpacity, enableAlerts, alertConfPct, alertCooldownSec);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.FuturesConfluenceTable FuturesConfluenceTable(int emaFastLen, int emaSlowLen, int emaTrendLen, int rsiLen, int rsiOB, int rsiOS, int mtf1Min, int mtf2Min, int mtf3Min, int eqTolTicks, int liqLookback, double liqNearAtr, bool drawLiqLines, int liqLineWidth, double absVolMult, double absRangeMult, double absWickPct, double absDeltaThreshold, int absMaxTicksMove, int preStart, int preEnd, int rthStart, int rthEnd, NinjaTrader.NinjaScript.Indicators.TablePosition position, NinjaTrader.NinjaScript.Indicators.FontSizeEnum fontSize, bool hideNeutral, bool showBiasRow, int maxRows, double bgOpacity, bool enableAlerts, int alertConfPct, int alertCooldownSec)
		{
			return indicator.FuturesConfluenceTable(Input, emaFastLen, emaSlowLen, emaTrendLen, rsiLen, rsiOB, rsiOS, mtf1Min, mtf2Min, mtf3Min, eqTolTicks, liqLookback, liqNearAtr, drawLiqLines, liqLineWidth, absVolMult, absRangeMult, absWickPct, absDeltaThreshold, absMaxTicksMove, preStart, preEnd, rthStart, rthEnd, position, fontSize, hideNeutral, showBiasRow, maxRows, bgOpacity, enableAlerts, alertConfPct, alertCooldownSec);
		}

		public Indicators.FuturesConfluenceTable FuturesConfluenceTable(ISeries<double> input , int emaFastLen, int emaSlowLen, int emaTrendLen, int rsiLen, int rsiOB, int rsiOS, int mtf1Min, int mtf2Min, int mtf3Min, int eqTolTicks, int liqLookback, double liqNearAtr, bool drawLiqLines, int liqLineWidth, double absVolMult, double absRangeMult, double absWickPct, double absDeltaThreshold, int absMaxTicksMove, int preStart, int preEnd, int rthStart, int rthEnd, NinjaTrader.NinjaScript.Indicators.TablePosition position, NinjaTrader.NinjaScript.Indicators.FontSizeEnum fontSize, bool hideNeutral, bool showBiasRow, int maxRows, double bgOpacity, bool enableAlerts, int alertConfPct, int alertCooldownSec)
		{
			return indicator.FuturesConfluenceTable(input, emaFastLen, emaSlowLen, emaTrendLen, rsiLen, rsiOB, rsiOS, mtf1Min, mtf2Min, mtf3Min, eqTolTicks, liqLookback, liqNearAtr, drawLiqLines, liqLineWidth, absVolMult, absRangeMult, absWickPct, absDeltaThreshold, absMaxTicksMove, preStart, preEnd, rthStart, rthEnd, position, fontSize, hideNeutral, showBiasRow, maxRows, bgOpacity, enableAlerts, alertConfPct, alertCooldownSec);
		}
	}
}

#endregion
