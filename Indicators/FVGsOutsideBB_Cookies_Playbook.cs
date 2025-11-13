
#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.PropertiesTest;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Windows;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
    public class FVGsOutsideBB_Cookies_Playbook : Indicator
    {
        //Criteria One 2-x consecutive FVGs outside Bollinger Bands based on a 50 EMA Period
        //Make sure to also have the Bollinger_EMA Indicator installed!

        #region Variables
        private Indicators.BarCounter barCounter;


        private StdDev stdDev;        
        private Bollinger_EMA Bollinger1; //<- this is a modification of the original BB Indicator!
        private Indicators.RSI RSI1;

        public bool Initialized = false;

        public double EntryPrice;
        public double StopPrice;
        public double TargetPrice;
        public double BreakEvenPrice;
        public double Risk;
        public double ATREntryCandle;
        public double RSIValue;
       

        // entry signalling and bookkeeping
        int CriteriaFoundCandle;
        bool EntrySignalLong = false;
        bool EntrySignalShort = false;


        public double patternRiskCurrency;


        // evaluation counters
        int EntryCriteriaFoundLong = 0;
        int EntryCriteriaFoundShort = 0;
        private int NumberOfFoundFVGs_bull;
        private List<int> FVGCandle_bull;
        private List<double> FVGHigh_bull;
        private List<double> FVGLow_bull;
        private List<bool> ConsecutivityTrue_bull;

        private int NumberOfFoundFVGs_bear;
        private List<int> FVGCandle_bear;
        private List<double> FVGHigh_bear;
        private List<double> FVGLow_bear;
        private List<bool> ConsecutivityTrue_bear;


        #endregion
        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                 = "Looks for certain Conditions defined in this Playbook";
                Name                        = "FVGsOutsideBB_Cookies_Playbook";
                Calculate                   = Calculate.OnBarClose;
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

                //these plots are currently not used
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
            else if (State == State.DataLoaded)
            {
                // initilizing indicators and lists
                stdDev = StdDev(Period);

                Bollinger1 = Bollinger_EMA(Close, 2, Period);
                Bollinger1.Plots[0].Brush = Brushes.DeepSkyBlue;
                Bollinger1.Plots[1].Brush = Brushes.Goldenrod;
                Bollinger1.Plots[2].Brush = Brushes.DeepSkyBlue;
                

                FVGCandle_bull = new List<int> { };
                FVGHigh_bull = new List<double> { };
                FVGLow_bull= new List<double> { };
                ConsecutivityTrue_bull = new List<bool> { };

                FVGCandle_bear = new List<int> { };
                FVGHigh_bear = new List<double> { };
                FVGLow_bear= new List<double> { };
                ConsecutivityTrue_bear = new List<bool> { };

                RSI1 = RSI(Close, Period, 3);
            }
           
        }

        protected override void OnBarUpdate()
        {
            #region Guards - Strategy should not fire before everything is ready

            if (Bollinger1.Upper == null || Bollinger1.Lower == null|| stdDev == null || BarsInProgress != 0 || CurrentBars[0] < Period || RSI1[0] == 0|| RSI1.Avg[0] == 0)
            {
                return;
            }

            if (!Initialized)
            {
                Initialized = true;
                Print($"FVGsOutsideBB_Cookies_Playbook Initialized {Times[0][0]} / Bar#: {CurrentBar}");
            }

            #endregion


            //Let's draw out what was found so far!
            Draw.TextFixed(this, "Indicator FVGsOutsideBB_Summary",
           $"{EntryCriteriaFoundLong} Long Entries found. \n" +
           $" {EntryCriteriaFoundShort} Short Entries found.",
               TextPosition.BottomRight, TextColor, new Gui.Tools.SimpleFont { Size = 14 },
                               Brushes.Transparent, Brushes.Transparent, 0);

            DateTime barDate = Times[0][0].Date; // session / calendar date for the current bar
            

            if (High[2] < Low[0] && //bullish FVG
                (NeglectBB ||
                (High[2] >= Bollinger1.Upper[2] && Low[0]>=Bollinger1.Upper[0])) // Outside of BB
                && NumberOfFoundFVGs_bull < NumberOfConsecutiveFVGs
                )
            {
                

                FVGCandle_bull.Add(CurrentBar); // we want to store the Candle that forms the FVG
                FVGLow_bull.Add(High[2]);   // we want to store the Values for the Rectangle to draw (because it looks cooler)
                FVGHigh_bull.Add(Low[0]);

                NumberOfFoundFVGs_bull++;
                Print($"Bullish: {NumberOfFoundFVGs_bull} on Candle {FVGCandle_bull[NumberOfFoundFVGs_bull-1]}");

                CheckConsecutiveFVGs(ref NumberOfFoundFVGs_bull, FVGCandle_bull, FVGHigh_bull, FVGLow_bull, ConsecutivityTrue_bull);
                DrawOnChart(ref NumberOfFoundFVGs_bull, FVGCandle_bull, FVGHigh_bull, FVGLow_bull, ConsecutivityTrue_bull, true);


            }
            
            
            if (Low[2] > High[0] && //bearish FVG
                (NeglectBB ||
                (Low[2] <= Bollinger1.Lower[2] && High[0]<=Bollinger1.Lower[0])) // Outside of BB
                && NumberOfFoundFVGs_bear < NumberOfConsecutiveFVGs
                )
            {
                

                FVGCandle_bear.Add(CurrentBar); // we want to store the Candle that forms the FVG
                FVGLow_bear.Add(High[0]);   // we want to store the Values for the Rectangle to draw (because it looks cooler)
                FVGHigh_bear.Add(Low[2]);
                
                
                

                NumberOfFoundFVGs_bear++;
                Print($"Bearish: {NumberOfFoundFVGs_bear} on Candle {FVGCandle_bear[NumberOfFoundFVGs_bear-1]}");

                CheckConsecutiveFVGs(ref NumberOfFoundFVGs_bear, FVGCandle_bear, FVGHigh_bear, FVGLow_bear, ConsecutivityTrue_bear);
                DrawOnChart(ref NumberOfFoundFVGs_bear, FVGCandle_bear, FVGHigh_bear, FVGLow_bear, ConsecutivityTrue_bear, false);
            }




            /*        

            #region For Longs
            if (NumberOfConsecutiveFVGs == 3
                &&


                Close[0] > Close[1] // confirming trend bullish
                  && Close[0] > Open[0] // confirming bullish candle
                  && Low[0] > Bollinger1.Middle[0]
                  && High[0] > Bollinger1.Upper[0]
                  && Low[0] <= High[2] //confirming we are not creating a FVG here
                  && Bollinger1.Upper[0] < Bollinger1.Upper[0]
                  && RSI1[0] < 70 //avoid overbought conditions
                   )
            {
                EntryCandle = CurrentBar;
                //marking the Candles on the Chart
                BarBrushes[0] = BarColor2;

                // Marking the Entry and the SL
                EntryPrice = Close[0];
                StopPrice = Low[0];
                Risk = EntryPrice - StopPrice;
                ATREntryCandle = Math.Round(ATR1[0], 1);

                RSIValue = Math.Round(RSI1[0], 2);

                patternRiskCurrency = Math.Round((Risk * Instrument.MasterInstrument.PointValue), 1);
                TargetPrice = EntryPrice + (Risk * 1); //1:1 RR

                EntrySignalLong = true;

                EntryCriteriaFoundLong += 1;
                Print($"Long Entry Criteria met at {Times[0][0]} / Bar#: {CurrentBar}");

            }
            #endregion




            #region For Shorts
            if (Close[0] < Close[1] // confirming trend bearish
                    && Close[0] < Open[0] // confirming bearish candle
                    && High[0] < EMAMiddle[0]
                    && Low[0] < EMALower[0]
                    && High[0] >= Low[2] //confirming we are not creating a FVG here
                    && Bollinger1.Lower[0] > EMALower[0]
                    && RSI1[0] > 20//avoid oversold conditions
                    )
            {
                EntryCandle = CurrentBar;
                //marking the Candles on the Chart
                BarBrushes[0] = BarColor1;

                // Marking the Entry and the SL
                EntryPrice = Close[0];
                StopPrice = High[0];
                Risk =  StopPrice - EntryPrice;
                ATREntryCandle = ATR1[0];
                RSIValue = Math.Round(RSI1[0], 2);
                patternRiskCurrency = Math.Round((Risk * Instrument.MasterInstrument.PointValue), 1);
                TargetPrice = EntryPrice - (Risk * 1); //1:1 RR
                EntrySignalShort = true;

                EntryCriteriaFoundShort += 1;
                //Print($"Short Entry Criteria met at {Times[0][0]} / Bar#: {CurrentBar}");

            }
            #endregion

            */
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
                    , 3, EntryPrice, TextColor);
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

        private void CheckConsecutiveFVGs(
            ref int FoundFVGs, 
            List<int> FVGCandle,
            List<double> FVGHigh,
            List<double> FVGLow,
            List<bool> ConsecutivityTrue)
            {          
           

            if (FoundFVGs >= NumberOfConsecutiveFVGs) //Found the correct number of FVGs 

            {// we want to check of consecutive FVGs once we reached the required amount
                
                for (int i = 0; i < (FVGCandle.Count -1); i++) // Check if the FVGs are consecutive
                {
                    Print($"{FVGCandle[i]} comparing to {FVGCandle[(i+1)]}");

                    if (FVGCandle[i] + 1 + CandlesbetweenFVGs >= FVGCandle[i+1])
                    {
                        Print("FVG in Range");
                        ConsecutivityTrue.Add(true);
                    }
                    else
                    {
                        FVGCandle.RemoveAt(0);
                        FVGHigh.RemoveAt(0);
                        FVGLow.RemoveAt(0);
                        ConsecutivityTrue.Clear();
                        FoundFVGs--;
                        Print("One did not met the condition and first Entry was removed");
                    }
                   
                }
                
            }
        }

        private void DrawOnChart(
            ref int FoundFVGs,
            List<int> FVGCandle,
            List<double> FVGHigh,
            List<double> FVGLow,
            List<bool> ConsecutivityTrue,
            bool isBull)
            {
            Print($"Checking if we have something to draw at {CurrentBar}; {ConsecutivityTrue.Count} ");
            if (ConsecutivityTrue.TrueForAll(b => b) == true && ConsecutivityTrue.Count == (NumberOfConsecutiveFVGs -1))
            {
                for (int d = 0; d < FVGCandle.Count; d++)
                {
                    if (isBull)
                    { 
                    Draw.Rectangle(this, $"FVG_Rectangle {FVGCandle[d]}", (CurrentBar - FVGCandle[d]), FVGHigh[d], -5, FVGLow[d], FVGColor_bull);
                    Print("Drew one bullish FVG ");
                    }
                    if (!isBull)
                    { 
                    Draw.Rectangle(this, $"FVG_Rectangle {FVGCandle[d]}", (CurrentBar - FVGCandle[d]), FVGHigh[d], -5, FVGLow[d], FVGColor_bear);
                    Print("Drew one bearish FVG");
                    }
                }
                if (isBull)
                    Draw.ArrowDown(this, $"FVG_Criteria_Met {CurrentBar}", false, 0, High[0]+ 10*TickSize, FVGColor_bull);
                if (!isBull)
                    Draw.ArrowUp(this, $"FVG_Criteria_Met {CurrentBar}", false, 0, Low[0]- 10*TickSize, FVGColor_bear);

                Alert($"FVG_Criteria_Met {CurrentBar}", Priority.High, $"Consecutive FVGs found on {Instrument} at {Time}", NinjaTrader.Core.Globals.InstallDir+@"\sounds\Alert1.wav", 10, Brushes.Black, Brushes.Yellow);


                //Let's check if there is more FVG incoming
                FVGCandle.RemoveAt(0);
                FVGHigh.RemoveAt(0);
                FVGLow.RemoveAt(0);
                ConsecutivityTrue.Clear();
                FoundFVGs--;

               

            }
        }

        #region Properties

        // Visual Customization Settings


        //[Display(Name = "Candle Color for Shorts", Description = "Select the color for marking candles", Order = 9, GroupName = "Visual - Choose Transparent if it should not draw.")]
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
        [Display(Name = "Text Color", Description = "Select the color for Texts", Order = 15, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush TextColor { get; set; } = Brushes.CornflowerBlue;

        
        [NinjaScriptProperty]
            [TypeConverter(typeof(BrushConverter))]
            [Display(Name = "FVG Color", Description = "Select the color for bullish FVGs", Order = 14, GroupName = "Visual - Choose Transparent if it should not draw.")]
            public Brush FVGColor_bull { get; set; } = Brushes.MediumPurple;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "FVG Color", Description = "Select the color for bearish FVGs", Order = 15, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush FVGColor_bear { get; set; } = Brushes.LightYellow;




        // Trading Parameters
        [NinjaScriptProperty]
            [Display(Name = "Restart Indicator", Description = "After Changes done in coding, just toggle and reapply", Order = 1, GroupName = "Apply changes from Coding")]
            public bool UseAsIndicatorOnly { get; set; } = true;

            // Bollinger Bands Settings
            [NinjaScriptProperty]
            [Range(0, int.MaxValue)]
            [Display(ResourceType = typeof(Custom.Resource), Name = "NumStdDev", GroupName = "Settings for other Indicators", Order = 200)]
            public double NumStdDev { get; set; } = 2.0;

            [NinjaScriptProperty]
            [Range(1, int.MaxValue)]
            [Display(ResourceType = typeof(Custom.Resource), Name = "Period for Bollinger Bands based on EMA", GroupName = "Settings for other Indicators", Order = 201)]
            public int Period { get; set; } = 50;





            private int EntryCandle;

        [NinjaScriptProperty]
        [Range(2, int.MaxValue)]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Number Of Consecutive FVGs", GroupName = "Criteria Settings", Order = 201)]
        public int NumberOfConsecutiveFVGs { get; set; } = 3;

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Maximum amount of Candles allowed between FVGs", GroupName = "Criteria Settings", Order = 201)]
        public int CandlesbetweenFVGs { get; set; } = 0;


        [NinjaScriptProperty]    
        [Display(ResourceType = typeof(Custom.Resource), Name = "Ignore BB Criteria", GroupName = "Criteria Settings", Order = 201)]

        public bool NeglectBB { get; set; }
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private FVGsOutsideBB_Cookies_Playbook[] cacheFVGsOutsideBB_Cookies_Playbook;
		public FVGsOutsideBB_Cookies_Playbook FVGsOutsideBB_Cookies_Playbook(Brush barColor1, Brush barColor2, Brush textColor, Brush fVGColor_bull, Brush fVGColor_bear, bool useAsIndicatorOnly, double numStdDev, int period, int numberOfConsecutiveFVGs, int candlesbetweenFVGs, bool neglectBB)
		{
			return FVGsOutsideBB_Cookies_Playbook(Input, barColor1, barColor2, textColor, fVGColor_bull, fVGColor_bear, useAsIndicatorOnly, numStdDev, period, numberOfConsecutiveFVGs, candlesbetweenFVGs, neglectBB);
		}

		public FVGsOutsideBB_Cookies_Playbook FVGsOutsideBB_Cookies_Playbook(ISeries<double> input, Brush barColor1, Brush barColor2, Brush textColor, Brush fVGColor_bull, Brush fVGColor_bear, bool useAsIndicatorOnly, double numStdDev, int period, int numberOfConsecutiveFVGs, int candlesbetweenFVGs, bool neglectBB)
		{
			if (cacheFVGsOutsideBB_Cookies_Playbook != null)
				for (int idx = 0; idx < cacheFVGsOutsideBB_Cookies_Playbook.Length; idx++)
					if (cacheFVGsOutsideBB_Cookies_Playbook[idx] != null && cacheFVGsOutsideBB_Cookies_Playbook[idx].BarColor1 == barColor1 && cacheFVGsOutsideBB_Cookies_Playbook[idx].BarColor2 == barColor2 && cacheFVGsOutsideBB_Cookies_Playbook[idx].TextColor == textColor && cacheFVGsOutsideBB_Cookies_Playbook[idx].FVGColor_bull == fVGColor_bull && cacheFVGsOutsideBB_Cookies_Playbook[idx].FVGColor_bear == fVGColor_bear && cacheFVGsOutsideBB_Cookies_Playbook[idx].UseAsIndicatorOnly == useAsIndicatorOnly && cacheFVGsOutsideBB_Cookies_Playbook[idx].NumStdDev == numStdDev && cacheFVGsOutsideBB_Cookies_Playbook[idx].Period == period && cacheFVGsOutsideBB_Cookies_Playbook[idx].NumberOfConsecutiveFVGs == numberOfConsecutiveFVGs && cacheFVGsOutsideBB_Cookies_Playbook[idx].CandlesbetweenFVGs == candlesbetweenFVGs && cacheFVGsOutsideBB_Cookies_Playbook[idx].NeglectBB == neglectBB && cacheFVGsOutsideBB_Cookies_Playbook[idx].EqualsInput(input))
						return cacheFVGsOutsideBB_Cookies_Playbook[idx];
			return CacheIndicator<FVGsOutsideBB_Cookies_Playbook>(new FVGsOutsideBB_Cookies_Playbook(){ BarColor1 = barColor1, BarColor2 = barColor2, TextColor = textColor, FVGColor_bull = fVGColor_bull, FVGColor_bear = fVGColor_bear, UseAsIndicatorOnly = useAsIndicatorOnly, NumStdDev = numStdDev, Period = period, NumberOfConsecutiveFVGs = numberOfConsecutiveFVGs, CandlesbetweenFVGs = candlesbetweenFVGs, NeglectBB = neglectBB }, input, ref cacheFVGsOutsideBB_Cookies_Playbook);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.FVGsOutsideBB_Cookies_Playbook FVGsOutsideBB_Cookies_Playbook(Brush barColor1, Brush barColor2, Brush textColor, Brush fVGColor_bull, Brush fVGColor_bear, bool useAsIndicatorOnly, double numStdDev, int period, int numberOfConsecutiveFVGs, int candlesbetweenFVGs, bool neglectBB)
		{
			return indicator.FVGsOutsideBB_Cookies_Playbook(Input, barColor1, barColor2, textColor, fVGColor_bull, fVGColor_bear, useAsIndicatorOnly, numStdDev, period, numberOfConsecutiveFVGs, candlesbetweenFVGs, neglectBB);
		}

		public Indicators.FVGsOutsideBB_Cookies_Playbook FVGsOutsideBB_Cookies_Playbook(ISeries<double> input , Brush barColor1, Brush barColor2, Brush textColor, Brush fVGColor_bull, Brush fVGColor_bear, bool useAsIndicatorOnly, double numStdDev, int period, int numberOfConsecutiveFVGs, int candlesbetweenFVGs, bool neglectBB)
		{
			return indicator.FVGsOutsideBB_Cookies_Playbook(input, barColor1, barColor2, textColor, fVGColor_bull, fVGColor_bear, useAsIndicatorOnly, numStdDev, period, numberOfConsecutiveFVGs, candlesbetweenFVGs, neglectBB);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.FVGsOutsideBB_Cookies_Playbook FVGsOutsideBB_Cookies_Playbook(Brush barColor1, Brush barColor2, Brush textColor, Brush fVGColor_bull, Brush fVGColor_bear, bool useAsIndicatorOnly, double numStdDev, int period, int numberOfConsecutiveFVGs, int candlesbetweenFVGs, bool neglectBB)
		{
			return indicator.FVGsOutsideBB_Cookies_Playbook(Input, barColor1, barColor2, textColor, fVGColor_bull, fVGColor_bear, useAsIndicatorOnly, numStdDev, period, numberOfConsecutiveFVGs, candlesbetweenFVGs, neglectBB);
		}

		public Indicators.FVGsOutsideBB_Cookies_Playbook FVGsOutsideBB_Cookies_Playbook(ISeries<double> input , Brush barColor1, Brush barColor2, Brush textColor, Brush fVGColor_bull, Brush fVGColor_bear, bool useAsIndicatorOnly, double numStdDev, int period, int numberOfConsecutiveFVGs, int candlesbetweenFVGs, bool neglectBB)
		{
			return indicator.FVGsOutsideBB_Cookies_Playbook(input, barColor1, barColor2, textColor, fVGColor_bull, fVGColor_bear, useAsIndicatorOnly, numStdDev, period, numberOfConsecutiveFVGs, candlesbetweenFVGs, neglectBB);
		}
	}
}

#endregion
