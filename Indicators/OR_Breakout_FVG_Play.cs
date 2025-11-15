#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.FIX;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.MarketAnalyzerColumns;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Media;
using System.Xml.Serialization;





#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class OR_Breakout_FVG_Play : Indicator
    {

        // Strategy from Kaz | Community coded by Tigertrades
        // Published at https://www.reddit.com/r/tradingmillionaires/comments/1owajri/i_made_2700_in_october_and_6500_so_far_in/
        // Quick Summary:
        // Trades on NQ, GC, TSLA and XAUUSD
        // Take the 15min opening range, wait for a candle that breaks the Range to either side AND forms a FVG within the Range
        // Entry at Candle Closure, SL at high/low of the first Candle that formed the FVG
        // Profit Target is dependend on the Risk, if Risk is 30 points or less, it's a 2R, otherwise a 1R
        // stop loocking for entries after 1,5h/90 mins
        // a premarket bias might help to not enter in the wrong direction even so the pattern occures!
        // Risk should be maximum 0.5-2% per trade
        // Move stop to breakeven once price clears structure -> not implemented, maybe if a new swing was created?
        // if the pattern fails once, try again. If it hits target, go enjoy life!




        private ATR_Ticks ATR1;
        private RSI RSI1;


        public double EntryPrice;
        public double StopPrice;
        public double TargetPrice;
        public double Risk;

        private Indicators.OpeningRange OpenRange;


        int BeginningBar;


        double StoredOpenRangeHigh = 0;
        double StoredOpenRangeLow =0;
        double EntryCandle;

        private double ATREntryCandle;
        private double patternRiskCurrency;
        public double RSIValue;

        // evaluation counters
        int EntryCriteriaFoundLong = 0;
        int EntryCriteriaFoundShort = 0;

        bool EntrySignalLong = false;
        bool EntrySignalShort = false;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Uses the Opening Range Indicator and checks for a FVG within accompanied by a candle closing below or above the Range to signal an Entry with 2R. ~ Tigertrades";
                Name = "OR_Breakout_FVG_Play";

                IsOverlay                   = true;
                IsAutoScale                 = false;
                PaintPriceMarkers           = false;
                DisplayInDataBox            = true;
                DrawOnPricePanel            = true;
                DrawHorizontalGridLines     = true;
                DrawVerticalGridLines       = true;
                MaximumBarsLookBack         = MaximumBarsLookBack.Infinite;
                PaintPriceMarkers           = true;
                ScaleJustification          = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                //Disable this property if your indicator requires custom values that cumulate with each new market data event. 
                //See Help Guide for additional information.
                IsSuspendedWhileInactive    = true;

                AddPlot(new Stroke(Brushes.Yellow, 1), PlotStyle.Line, "Entry");
                AddPlot(new Stroke(Brushes.Red, 1), PlotStyle.Line, "Stop");
                AddPlot(new Stroke(Brushes.Green, 1), PlotStyle.Line, "Target");
                AddPlot(new Stroke(Brushes.CornflowerBlue, 5), PlotStyle.TriangleUp, "Long");
                AddPlot(new Stroke(Brushes.CornflowerBlue, 5), PlotStyle.TriangleDown, "Short");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "Entry Bar ATR");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "Money to Risk");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "Entry Bar RSI");
                AddPlot(new Stroke(Brushes.Transparent, 1), PlotStyle.Line, "Initialized");
            }



            else if (State == State.Configure)
            {
              
            }
            else if (State == State.DataLoaded)
            {
                OpenRange = OpeningRange(Close, OpeningRangeMinutes, Asia, London, Frankfurt, US, Market, HighlightColor, HighlightAreaColor, HighlightOpacity, GapColor, ShowHighlight, TextColor, HowLongToPlot, PlotTheGap);
                RSI1 = RSI(Close, Period, 3);
                ATR1 = ATR_Ticks(1);
            }
        }

        protected override void OnBarUpdate()
        {
            if (RSI1[0] == 0|| RSI1.Avg[0] == 0 )
            {return;
            }

            if (OpenRange.Value.IsValidDataPoint(0)
                && StoredOpenRangeHigh != OpenRange.Upper[0]) 
            {
                StoredOpenRangeHigh = OpenRange.Upper[0];
                StoredOpenRangeLow = OpenRange.Lower[0];
                Print(Times[0][0]);
                Print(StoredOpenRangeHigh);
                Print(StoredOpenRangeLow);
                BeginningBar = CurrentBar;

            }

            if (StoredOpenRangeHigh > 0 && CurrentBar <= BeginningBar + MinutesToLookForEntry)
            {
                //For Longs
                if (Low[0] <= StoredOpenRangeHigh && Close[0] >= StoredOpenRangeHigh   //breaking ORH
                && Low[0] > High[2]) //Candle created a FVG
                
                {
                    EntryCandle=CurrentBar;
                    Draw.Rectangle(this, $"FVG_Rectangle {EntryCandle}", 1, High[2], -5, Low[0], FVGColor);
                    BarBrushes[0] = BarColor2;

                    EntryPrice = Close[0];
                    StopPrice = Low[2]; //at the high of the 1 Candle creating the FVG
                    Risk = Math.Abs(EntryPrice-StopPrice);
                    patternRiskCurrency = Math.Round((Risk * Instrument.MasterInstrument.PointValue), 1);

                    ATREntryCandle = Math.Round(ATR1[0], 1);

                    RSIValue = Math.Round(RSI1[0], 2);


                    if (Risk < 30* PointValue) //if the Stop Loss is 30 points away  - 1 Point == 20$ for NQ
                    {
                        TargetPrice = EntryPrice + 2*Risk;

                    }
                    else
                        TargetPrice = EntryPrice + Risk;

                    EntrySignalLong = true;
                    EntryCriteriaFoundLong += 1;
                    Print($"Long Entry Criteria met at {Times[0][0]}");
                }
                    
                //For Shorts
                if (High[0] >= StoredOpenRangeLow && Close[0] <= StoredOpenRangeLow //breaking ORL
                && Low[2] > High[0]) //Candle created a FVG
                {
                    EntryCandle=CurrentBar;
                    Draw.Rectangle(this, $"FVG_Rectangle {EntryCandle}", 1, High[0], -5, Low[2], FVGColor);
                    BarBrushes[0] = BarColor1;

                    EntryPrice = Close[0];
                    StopPrice = High[2]; //at the high of the 1 Candle creating the FVG
                    Risk = Math.Abs(EntryPrice-StopPrice);
                    patternRiskCurrency = Math.Round((Risk * Instrument.MasterInstrument.PointValue), 1);

                    ATREntryCandle = Math.Round(ATR1[0], 1);

                    RSIValue = Math.Round(RSI1[0], 2);


                    if (Risk < 30* PointValue) //if the Stop Loss is 30 points away  - 1 Point == 20$ for NQ
                    {
                        TargetPrice = EntryPrice - 2*Risk;

                    }
                    else
                        TargetPrice = EntryPrice - Risk;

                    EntrySignalShort = true;

                    EntryCriteriaFoundShort += 1;
                    Print($"Short Entry Criteria met at {Times[0][0]}");


                }



            }
            if(StoredOpenRangeHigh!=0 && CurrentBar > BeginningBar + MinutesToLookForEntry)
            {
                StoredOpenRangeHigh = 0;
                StoredOpenRangeLow = 0;

            }


            #region Plotting, making Values available

            Values[8][0] = Low[0]; //if this plots the strategy is ready

            //Plotting Entry, Stop and Target and Entry Signals
            if (CurrentBar == EntryCandle)
            {

                Values[0][0] = EntryPrice;
                Values[1][0] = StopPrice; //Stop = StopPrice;
                Values[2][0] = TargetPrice;                          //
                Values[5][0] = ATREntryCandle; //CandleATR = ATREntryCandle;                
                Values[6][0] = patternRiskCurrency;// RiskMoney = patternRiskCurrency;                
                Values[7][0] = RSIValue; //CandleRSI = RSIValue;

                if (EntrySignalLong)
                {
                    Values[3][0] =  Low[0]; //Long = 1; // Long Entry Signal
                    Values[4][0] = 0; //Short = 0;

                }
                if (EntrySignalShort)
                {
                    Values[4][0] = High[0]; //Short = 1; // Short Entry Signal
                    Values[3][0] = 0; //Long = 0;

                }

                Draw.Text(this, $"allPlotted_{CurrentBar}", $"{Values[4][0]} Short \n {Values[3][0]} Long \n" +

                    $"Money at Risk with one Contract {Values[6][0]} $$ \n" +
                     $"ATR {Values[5][0]}" +
                    $" / RSI {Values[7][0]}"
                    , 6, EntryPrice, TextColor);
            }
            else
            {//no Entry Signal
                Values[4][0] = 0;
                Values[3][0] = 0;
            }

            if (CurrentBar < (EntryCandle + 10) && CurrentBar > EntryCandle)
            {
                Values[0][0] = EntryPrice;
                Values[1][0] = StopPrice;
                Values[2][0] = TargetPrice;

            }


            //Resetting
            if ((CurrentBar > EntryCandle + 2) && (EntrySignalLong || EntrySignalShort))
            {
                EntrySignalLong = false;
                EntrySignalShort = false;
            }
            #endregion



        }
        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "Restart Indicator", Description = "After Changes done in coding, just toggle and reapply", Order = 1, GroupName = "Apply changes from Coding")]
        public bool UseAsIndicatorOnly { get; set; } = true;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "How many Minutes to check for Entry Signal", Order = 6, GroupName = "My Settings - everything is based on the 1 Minute Range!")]
        public int MinutesToLookForEntry { get; set; } = 90;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Candle Color for Shorts", Description = "Select the color for marking candles", Order = 10, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush BarColor1 { get; set; } = Brushes.Blue;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Candle Color for Longs", Description = "Select the color for marking candles", Order = 10, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush BarColor2 { get; set; } = Brushes.Yellow;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "FVG Color", Description = "Select the color for marking the FVG", Order = 10, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush FVGColor { get; set; } = Brushes.Yellow;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Period for RSI", GroupName = "Settings for other Indicators", Order = 201)]
        public int Period { get; set; } = 9;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Point Value", GroupName = "Settings for other Indicators", Order = 201)]

        public int PointValue { get; set; } = 20;

        #region Setting for the Opening Range Indicator
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Minutes for Opening Range to look at", Order = 6, GroupName = "1 - Settings - everything is based on the 1 Minute Range!")]
        public int OpeningRangeMinutes { get; set; } = 5;

        [NinjaScriptProperty]
        [Display(Name = "Asia", Order = 1, GroupName = "2 - Which Opening")]
        public bool Asia { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "London", Order = 2, GroupName = "2 - Which Opening")]
        public bool London { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Frankfurt", Order = 3, GroupName = "2 - Which Opening")]
        public bool Frankfurt { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "US", Order = 4, GroupName = "2 - Which Opening")]
        public bool US { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Market Global Open", Order = 4, GroupName = "2 - Which Opening")]
        public bool Market { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Region Highlight Color", Order = 5, GroupName = "3 - Visuals")]
        public Brush HighlightColor { get; set; } = Brushes.Transparent;

        [NinjaScriptProperty]
        [Display(Name = "Region Highlight Color", Order = 6, GroupName = "3 - Visuals")]
        public Brush HighlightAreaColor { get; set; } = Brushes.SteelBlue;


        [NinjaScriptProperty]
        [Range(0, 100)]
        [Display(Name = "Region Highlight Area Opacity", Order = 6, GroupName = "3 - Visuals")]
        public int HighlightOpacity { get; set; } = 10;

        [NinjaScriptProperty]
        [Display(Name = "Gap Color", Order = 5, GroupName = "3 - Visuals")]
        public Brush GapColor { get; set; } = Brushes.SpringGreen;


        [NinjaScriptProperty]
        [Display(Name = " Show Region Highlight", Order = 7, GroupName = "3 - Visuals")]
        public bool ShowHighlight { get; set; } = true;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Text Color", Description = "Select the color for Texts", Order = 15, GroupName = "3 - Visuals")]
        public Brush TextColor { get; set; } = Brushes.CornflowerBlue;


        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "How many Bars should we plot?", Order = 6, GroupName = "1 - Settings - everything is based on the 1 Minute Range!")]
        public int HowLongToPlot { get; set; } = 30;

        [NinjaScriptProperty]        
        [Display(Name = "Show the Gap between Close of previous Day and Market Open", Order = 6, GroupName = "2 - Which Opening")]
        public bool PlotTheGap { get; set; } = true;
        #endregion








        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private OR_Breakout_FVG_Play[] cacheOR_Breakout_FVG_Play;
		public OR_Breakout_FVG_Play OR_Breakout_FVG_Play(bool useAsIndicatorOnly, int minutesToLookForEntry, Brush barColor1, Brush barColor2, Brush fVGColor, int period, int pointValue, int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return OR_Breakout_FVG_Play(Input, useAsIndicatorOnly, minutesToLookForEntry, barColor1, barColor2, fVGColor, period, pointValue, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}

		public OR_Breakout_FVG_Play OR_Breakout_FVG_Play(ISeries<double> input, bool useAsIndicatorOnly, int minutesToLookForEntry, Brush barColor1, Brush barColor2, Brush fVGColor, int period, int pointValue, int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			if (cacheOR_Breakout_FVG_Play != null)
				for (int idx = 0; idx < cacheOR_Breakout_FVG_Play.Length; idx++)
					if (cacheOR_Breakout_FVG_Play[idx] != null && cacheOR_Breakout_FVG_Play[idx].UseAsIndicatorOnly == useAsIndicatorOnly && cacheOR_Breakout_FVG_Play[idx].MinutesToLookForEntry == minutesToLookForEntry && cacheOR_Breakout_FVG_Play[idx].BarColor1 == barColor1 && cacheOR_Breakout_FVG_Play[idx].BarColor2 == barColor2 && cacheOR_Breakout_FVG_Play[idx].FVGColor == fVGColor && cacheOR_Breakout_FVG_Play[idx].Period == period && cacheOR_Breakout_FVG_Play[idx].PointValue == pointValue && cacheOR_Breakout_FVG_Play[idx].OpeningRangeMinutes == openingRangeMinutes && cacheOR_Breakout_FVG_Play[idx].Asia == asia && cacheOR_Breakout_FVG_Play[idx].London == london && cacheOR_Breakout_FVG_Play[idx].Frankfurt == frankfurt && cacheOR_Breakout_FVG_Play[idx].US == uS && cacheOR_Breakout_FVG_Play[idx].Market == market && cacheOR_Breakout_FVG_Play[idx].HighlightColor == highlightColor && cacheOR_Breakout_FVG_Play[idx].HighlightAreaColor == highlightAreaColor && cacheOR_Breakout_FVG_Play[idx].HighlightOpacity == highlightOpacity && cacheOR_Breakout_FVG_Play[idx].GapColor == gapColor && cacheOR_Breakout_FVG_Play[idx].ShowHighlight == showHighlight && cacheOR_Breakout_FVG_Play[idx].TextColor == textColor && cacheOR_Breakout_FVG_Play[idx].HowLongToPlot == howLongToPlot && cacheOR_Breakout_FVG_Play[idx].PlotTheGap == plotTheGap && cacheOR_Breakout_FVG_Play[idx].EqualsInput(input))
						return cacheOR_Breakout_FVG_Play[idx];
			return CacheIndicator<OR_Breakout_FVG_Play>(new OR_Breakout_FVG_Play(){ UseAsIndicatorOnly = useAsIndicatorOnly, MinutesToLookForEntry = minutesToLookForEntry, BarColor1 = barColor1, BarColor2 = barColor2, FVGColor = fVGColor, Period = period, PointValue = pointValue, OpeningRangeMinutes = openingRangeMinutes, Asia = asia, London = london, Frankfurt = frankfurt, US = uS, Market = market, HighlightColor = highlightColor, HighlightAreaColor = highlightAreaColor, HighlightOpacity = highlightOpacity, GapColor = gapColor, ShowHighlight = showHighlight, TextColor = textColor, HowLongToPlot = howLongToPlot, PlotTheGap = plotTheGap }, input, ref cacheOR_Breakout_FVG_Play);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.OR_Breakout_FVG_Play OR_Breakout_FVG_Play(bool useAsIndicatorOnly, int minutesToLookForEntry, Brush barColor1, Brush barColor2, Brush fVGColor, int period, int pointValue, int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OR_Breakout_FVG_Play(Input, useAsIndicatorOnly, minutesToLookForEntry, barColor1, barColor2, fVGColor, period, pointValue, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}

		public Indicators.OR_Breakout_FVG_Play OR_Breakout_FVG_Play(ISeries<double> input , bool useAsIndicatorOnly, int minutesToLookForEntry, Brush barColor1, Brush barColor2, Brush fVGColor, int period, int pointValue, int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OR_Breakout_FVG_Play(input, useAsIndicatorOnly, minutesToLookForEntry, barColor1, barColor2, fVGColor, period, pointValue, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.OR_Breakout_FVG_Play OR_Breakout_FVG_Play(bool useAsIndicatorOnly, int minutesToLookForEntry, Brush barColor1, Brush barColor2, Brush fVGColor, int period, int pointValue, int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OR_Breakout_FVG_Play(Input, useAsIndicatorOnly, minutesToLookForEntry, barColor1, barColor2, fVGColor, period, pointValue, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}

		public Indicators.OR_Breakout_FVG_Play OR_Breakout_FVG_Play(ISeries<double> input , bool useAsIndicatorOnly, int minutesToLookForEntry, Brush barColor1, Brush barColor2, Brush fVGColor, int period, int pointValue, int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OR_Breakout_FVG_Play(input, useAsIndicatorOnly, minutesToLookForEntry, barColor1, barColor2, fVGColor, period, pointValue, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}
	}
}

#endregion
