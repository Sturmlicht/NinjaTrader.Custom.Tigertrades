#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.CQG.ProtoBuf;
using NinjaTrader.Data;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.Indicators.j2;
using NinjaTrader.NinjaScript.OptimizationFitnesses;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

//This namespace holds Strategies in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Strategies
{
	public class CRTandFVG : Strategy
	{
        //This is one of the Strategy TG_Capital shared with us. Feel free to test and optimize it, or post suggestions in the Chart Fanatics Discord (mention "Tigertrades" so I see it).
        //I am not a profeesional coder nor trader.        
        //Please use at your own risk. Your money is at risk. 
        
        // Version Nov.6, 2025

        private Indicators.BarCounter barCounter;
        
        private EMA EMA1;

        double oversoldLow = 0;
        bool oversoldFound = false;
        int RSICandle = 0;

        double EntryPrice;
        double StopPrice;
        double TargetPrice;
        double BreakEvenPrice;
        double Risk;
        bool BreakevenExecuted = false;

        bool criteriaFoundLong = false;
        bool criteriaFoundShort = false;
        int CriteriaFoundCandle;
        bool EntrySignalLong = false;
        bool EntrySignalShort = false;

        double FVGLow;
        double FVGHigh;

        int Quantity;
        private double patternRiskCurrency;
        private bool TradingDay;


        //evaluation variables
        int FirstCriteriaFoundLong = 0;
        int EntryCriteriaFoundLong = 0;
        int EntryCriteriaFoundShort = 0;
        int FirstCriteriaFoundShort = 0;
        private double ValueToReach;

        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description									= @"CRTandFVG from TG Capital";
				Name										= "CRTandFVG";
				Calculate									= Calculate.OnPriceChange;
				EntriesPerDirection							= 1;
				EntryHandling								= EntryHandling.AllEntries;
				IsExitOnSessionCloseStrategy				= true;
				ExitOnSessionCloseSeconds					= 30;
				IsFillLimitOnTouch							= false;
				MaximumBarsLookBack							= MaximumBarsLookBack.TwoHundredFiftySix;
				OrderFillResolution							= OrderFillResolution.Standard;
				Slippage									= 0;
				StartBehavior								= StartBehavior.WaitUntilFlat;
				TimeInForce									= TimeInForce.Gtc;
				TraceOrders									= false;
				RealtimeErrorHandling						= RealtimeErrorHandling.StopCancelClose;
				StopTargetHandling							= StopTargetHandling.PerEntryExecution;
				BarsRequiredToTrade							= 20;
				// Disable this property for performance gains in Strategy Analyzer optimizations
				// See the Help Guide for additional information
				IsInstantiatedOnEachOptimizationIteration	= true;

               
            }
			else if (State == State.Configure)
			{
                AddDataSeries(Data.BarsPeriodType.Minute, 1); //1 -> this is only used for BarCounter, Please note, as this is in use, this Strategy won't work for Charts using a smaller Timeframe!

            }
			else if (State == State.DataLoaded)
			{
                barCounter = BarCounter(Closes[1], true, Brushes.Gray, 14, 50, true); 
                //you can change this "Closes[1]" to "Closes[0]" to use the Bar Counter with tha values on your primary chart,
                //this will make using the Strategy available for timeframes lower than 1Min!
                
                if (ShowBarCounterOnChart) AddChartIndicator(barCounter);		

                EMA1                = EMA(Close, 50);
                AddChartIndicator(EMA1);
                AddChartIndicator(EMA (Close, 50));

                
            }
        }

        protected override void OnBarUpdate() //OnMarketData(MarketDataEventArgs e)
        {
            if (BarsInProgress != 0)
                return;

            if (CurrentBars[0] < 3)
                return;

            //Let's draw out what was found so far!
            Draw.TextFixed(this, "Strategy CRT with inverted FVG is active", 
                $"{EntryCriteriaFoundLong} Long Entries found of {FirstCriteriaFoundLong} first Criterias met. " + 
                $" {EntryCriteriaFoundShort} Short Entries found of {FirstCriteriaFoundShort} first Criterias met.", 
                TextPosition.BottomRight, TextColor, new Gui.Tools.SimpleFont { Size = 14 },
                                    Brushes.Transparent, Brushes.Transparent, 0);

            


            //Check for Correct Time for Trading Days and Bars
            System.DayOfWeek currentDay = Times[0][0].DayOfWeek;            
            TradingDay = currentDay switch
            {
                System.DayOfWeek.Monday => Monday,
                System.DayOfWeek.Tuesday => Tuesday,
                System.DayOfWeek.Wednesday => Wednesday,
                System.DayOfWeek.Thursday => Thursday,
                System.DayOfWeek.Friday => Friday,
                System.DayOfWeek.Saturday => Saturday,
                System.DayOfWeek.Sunday => Sunday,
                _ => false
            };
            
            bool TradingTime =
                (
                (
                   (US &&   barCounter[0] > USBarStart && barCounter[0] < USBarEnd)
                || (Asia && barCounter[0] > AsiaBarStart && barCounter[0] < AsiaBarEnd)
                || (London && barCounter[0] > LondonBarStart && barCounter[0] < LondonBarEnd)
                || (Frankfurt && barCounter[0] > FrankfurtBarStart && barCounter[0] < FrankfurtBarEnd)
                || (Custom && barCounter[0] > CustomBarStart && barCounter[0] < CustomBarEnd)
                )
                &&  TradingDay
                );
         
            //reset bools if outside trading time
            if(!TradingTime)
            {
                criteriaFoundLong = false;
                criteriaFoundShort = false;
                EntrySignalLong = false;
                EntrySignalShort = false;
            }


            #region For Longs

            if (TradingTime
                && ((Low[2] > EMA1[0] && Low[1] > EMA1[0] && Low[0] > EMA1[0]) || neglectEMA) //everything above EMA 50
                && Open[1] > Close[1] //bearish candle
                && (Open[0] <= Close[0] || ignoreFVGCreatingCandleBullOrBear) //bullish candle creats the FVG
                && Low[2] > High[0] //FVG
                && (Low[0] < Low[1] || neglectCRTSweep) // CRT sweep
                )
            {   
                criteriaFoundLong = true;
                CriteriaFoundCandle = CurrentBar;
                FVGLow = High[0]; Draw.Line(this, $"FVGLow_{CurrentBar}", false, 2, FVGLow, -1,FVGLow, FVGColor, DashStyleHelper.Dash, 2);
                FVGHigh = Low[2]; Draw.Line(this, $"FVGHigh_{CurrentBar}", false, 2, FVGHigh, -1, FVGHigh, FVGColor, DashStyleHelper.Dash, 2);
                Draw.Text(this, $"FVG_{CurrentBar}", $"FVG", 2, (FVGLow + 15 * TickSize), FVGColor);
                //marking the Candles on the Chart
                BarBrushes[0] = BarColor1; 
                BarBrushes[1] = BarColor1;
                BarBrushes[2] = BarColor1;

                ValueToReach = Open[1]; //open of the bearish candle

                FirstCriteriaFoundLong += 1;

            }



            if (TradingTime
                && criteriaFoundLong && (ConfirmingCandleNotImmediate || CurrentBar == CriteriaFoundCandle+1) //let's check for the Confirming Candle
                && Close[0] > Open[0] // is bullish
                && ( Close[0] < ValueToReach  // Closes within range of the bearish candle
                || ( ConfirmingCandleCanCloseAnywhere && Close[0] > ValueToReach) // or above its open
                || (ConfirmingCandleCanCloseOnTheOpen && Close[0] == ValueToReach)) //  or on its open
                && Close[0] > FVGHigh // Confirming Candle inverts FVG - note: it does not open below it
                                     


                )
            { BarBrushes[0] = BarColor2; // let's mark it in a different color
                
                criteriaFoundLong = false;

                // Marking the Entry and the SL
                EntryPrice = Close[0];
                StopPrice = Low[0]; //be aware, this might not be a good idea if the confirming candle is not the immediate candle
                Risk = EntryPrice - StopPrice;

                Draw.Line(this, $"Entry_{CurrentBar}", false, 0, EntryPrice, -10, EntryPrice, EntryLineColor, DashStyleHelper.Dash, 2);
                Draw.Line(this, $"Stop_{CurrentBar}", false, 0, StopPrice, -10, StopPrice, StopLineColor, DashStyleHelper.Dash, 2);

                // Setting the Target
                TargetPrice = EntryPrice + (Risk * RewardMultiplyer);
                Draw.Line(this, $"Target_{CurrentBar}", false, 0, TargetPrice, -10, TargetPrice, TargetLineColor, DashStyleHelper.Dash, 2);

                // Calculating Quantity based on Risk
                patternRiskCurrency = Risk * Instrument.MasterInstrument.PointValue;
                int calculatedQuantity = (int)Math.Floor(RiskMaxMoney / patternRiskCurrency);
                Quantity = Math.Max(calculatedQuantity, 1); // Ensure at least 1 contract                                
                Draw.Text(this, $"Entry_{CurrentBar}", $"Quantity set: {Quantity} from Risk {patternRiskCurrency}", 0, Low[0] - 15 * TickSize, TextColor);

                EntrySignalLong = true; Draw.Text(this, $"Entry_{CurrentBar}", "Go Long", 0, Low[0] + 10 * TickSize, TextColor);
                EntryCriteriaFoundLong += 1;
            }

            //TRADING HERE!
            if ( EntrySignalLong  
                && !UseAsIndicatorOnly //if this is false, we will enter Trades!
                && Position.MarketPosition == MarketPosition.Flat
                && (patternRiskCurrency < RiskMaxMoney || !NoTradeIfRiskTooHigh)
                )
            {
                EntrySignalLong = false;

                // Entering Long and Setting Stops and Targets
                EnterLong(Quantity, $"Long");
                SetStopLoss($"Long", CalculationMode.Price, StopPrice, false);
                SetProfitTarget($"Long", CalculationMode.Price, TargetPrice);

                Print($"Long Entry: {Quantity} contracts at {EntryPrice}. Stop at {StopPrice}, Target at {TargetPrice}.");

            }




            #endregion


            #region For Shorts

            if (TradingTime
                && ((High[2] < EMA1[0] && High[1] < EMA1[0] && High[0] < EMA1[0]) || neglectEMA) //everything below EMA 50
                && Open[1] < Close[1] //bullish candle
                && (Open[0] >= Close[0] || ignoreFVGCreatingCandleBullOrBear) //bearish candle creats the FVG
                && High[2] < Low[0] //FVG
                && (High[0] > High[1] || neglectCRTSweep) // CRT sweep
                )
            {
                criteriaFoundShort = true;
                CriteriaFoundCandle = CurrentBar;
                FVGLow = High[2]; Draw.Line(this, $"FVGLow_{CurrentBar}", false, 2, FVGLow, -1, FVGLow, FVGColor, DashStyleHelper.Dash, 2);
                FVGHigh = Low[0]; Draw.Line(this, $"FVGHigh_{CurrentBar}", false, 2, FVGHigh, -1, FVGHigh, FVGColor, DashStyleHelper.Dash, 2);
                Draw.Text(this, $"FVG_{CurrentBar}", $"FVG", 2, (FVGLow + 15 * TickSize), FVGColor);
                //marking the Candles on the Chart
                BarBrushes[0] = BarColor1; 
                BarBrushes[1] = BarColor1;
                BarBrushes[2] = BarColor1;

                ValueToReach = Open[1]; //open of the bullish candle
                FirstCriteriaFoundShort += 1;
            }




            if (TradingTime
                && criteriaFoundShort && (ConfirmingCandleNotImmediate || CurrentBar == CriteriaFoundCandle+1) //let's check for the Confirming Candle
                && Close[0] < Open[0] // is bearish
                && (Close[0] > ValueToReach // Closes within range of the bullish candle
                || (ConfirmingCandleCanCloseAnywhere && Close[0] < ValueToReach) //or  below its open
                || (ConfirmingCandleCanCloseOnTheOpen && Close[0] == ValueToReach))  // or on its open
                && Close[0] < FVGLow // Confirming Candle inverts FVG - note: it does not open above it
                                     

                )
            {
                BarBrushes[0] = BarColor2; // let's mark it in a different color
                criteriaFoundShort = false;

                // Marking the Entry and the SL
                EntryPrice = Close[0];
                StopPrice = High[0]; //be aware, this might not be a good idea if the confirming candle is not the immediate candle
                Risk =  StopPrice - EntryPrice;

                Draw.Line(this, $"Entry_{CurrentBar}", false, 0, EntryPrice, -10, EntryPrice, EntryLineColor, DashStyleHelper.Dash, 2);
                Draw.Line(this, $"Stop_{CurrentBar}", false, 0, StopPrice, -10, StopPrice, StopLineColor, DashStyleHelper.Dash, 2);
                // Setting the Target
                TargetPrice = EntryPrice - (Risk * RewardMultiplyer);
                Draw.Line(this, $"Target_{CurrentBar}", false, 0, TargetPrice, -10, TargetPrice, TargetLineColor, DashStyleHelper.Dash, 2);

                // Calculating Quantity based on Risk
                patternRiskCurrency = Risk * Instrument.MasterInstrument.PointValue;

                 
                int calculatedQuantity = (int)Math.Floor(RiskMaxMoney / patternRiskCurrency);
                Quantity = Math.Max( Math.Min(calculatedQuantity, MaxQuantity), 1); // Ensure Maximum Quantity and at least 1 contract                                
                Draw.Text(this, $"Entry_{CurrentBar}", $"Quantity set: {Quantity} from Risk {patternRiskCurrency}", 0, High[0] + 15 * TickSize, TextColor);

                EntrySignalShort = true; Draw.Text(this, $"Entry_{CurrentBar}", "Go Short", 0, High[0] + 10 * TickSize, TextColor);
                EntryCriteriaFoundShort += 1;
            }

            //TRADING HERE!
            if (EntrySignalShort
                && !UseAsIndicatorOnly //if this is false, we will enter Trades!
                && Position.MarketPosition == MarketPosition.Flat
                && (patternRiskCurrency < RiskMaxMoney || !NoTradeIfRiskTooHigh)
                )
            {
                EntrySignalShort = false;
                               
                // Entering Short and Setting Stops and Targets
                EnterShort(Quantity, $"Short");
                SetStopLoss($"Short", CalculationMode.Price, StopPrice, false);
                SetProfitTarget($"Short", CalculationMode.Price, TargetPrice);
                

                Print($"Short Entry: {Quantity} contracts at {EntryPrice}. Stop at {StopPrice}, Target at {TargetPrice}.");
            }
            #endregion







        }

        #region Properties

        // Criteria Settings for the Playbook
        [NinjaScriptProperty]
        [Display(Name = "Neglect EMA Condition", Description = "If true, the EMA condition will be neglected.", Order = 1, GroupName = "Criteria Settings - ofc we trust TG Capital but we want to test anyways!")]
        public bool neglectEMA { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Neglect CRT Sweep Condition", Description = "If true, the CRT Sweep condition will be neglected.", Order = 2, GroupName = "Criteria Settings - ofc we trust TG Capital but we want to test anyways!")]
        public bool neglectCRTSweep { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Ignore FVG Creating Candle is Bull or Bear", Description = "If true, the candle creating the FVG can be bull or bear.", Order = 3, GroupName = "Criteria Settings - ofc we trust TG Capital but we want to test anyways!")]
        public bool ignoreFVGCreatingCandleBullOrBear { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Confirming Candle Not Immediate", Description = "If true, the confirming candle can be any candle after the FVG creating candle.", Order = 4, GroupName = "Criteria Settings - ofc we trust TG Capital but we want to test anyways!")]
        public bool ConfirmingCandleNotImmediate { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Confirming Candle Can Close Above/Below Open", Description = "If true, the confirming candle can close above the open of the FVG creating candle.", Order = 5, GroupName = "Criteria Settings - ofc we trust TG Capital but we want to test anyways!")]
        public bool ConfirmingCandleCanCloseAnywhere { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Confirming Candle Can Close On The Open", Description = "If true, the confirming candle can close exactly on the open of the FVG creating candle.", Order = 6, GroupName = "Criteria Settings - ofc we trust TG Capital but we want to test anyways!")]
        public bool ConfirmingCandleCanCloseOnTheOpen { get; set; } = false;








        // Visual Customization Settings
        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Bar Color for First Criteria Bars", Description = "Select the color for marking bars", Order = 9, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush BarColor1 { get; set; } = Brushes.Blue;
        
        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Confirming Candle Bar Color", Description = "Select the color for marking bars", Order = 10, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush BarColor2 { get; set; } = Brushes.Yellow;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Target Line Color", Description = "Select the color for Target Line", Order = 11, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush TargetLineColor { get; set; } = Brushes.Green;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Stop Line Color", Description = "Select the color for Stop Line", Order = 12, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush StopLineColor { get; set; } = Brushes.Red;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Entry Line Color", Description = "Select the color for Entry Line", Order = 13, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush EntryLineColor { get; set; } = Brushes.Yellow;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "FVG Color", Description = "Select the color for FVG Lines", Order = 14, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush FVGColor { get; set; } = Brushes.MediumPurple;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Text Color", Description = "Select the color for Texts", Order = 15, GroupName = "Visual - Choose Transparent if it should not draw.")]
        public Brush TextColor { get; set; } = Brushes.CornflowerBlue;


        // Trading Times Settings
        [NinjaScriptProperty]
        [Display(Name = "Show Bar Counter on Chart", Description = "Show Bar Counter Indicator on Chart", Order = 1, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool ShowBarCounterOnChart { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "US", Description = "Check if you want to check/trade this Session", Order = 2, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool US { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "US Bar Start", Description = "Start Bar for US Session", Order = 3, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int USBarStart { get; set; } = 936;
        
        [NinjaScriptProperty]
        [Display(Name = "US Bar End", Description = "End Bar for US Session", Order = 4, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int USBarEnd { get; set; } = 1600;

        [NinjaScriptProperty]
        [Display(Name = "Asia", Description = "Check if you want to check/trade this Session", Order = 5, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool Asia { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Asia Bar Start", Description = "Start Bar for Asia Session", Order = 6, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int AsiaBarStart { get; set; } = 0;
        [NinjaScriptProperty]
        [Display(Name = "Asia Bar End", Description = "End Bar for Asia Session", Order = 7, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int AsiaBarEnd { get; set; } = 300;
        [NinjaScriptProperty]
        [Display(Name = "London", Description = "Check if you want to check/trade this Session", Order = 8, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool London { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "London Bar Start", Description = "Start Bar for London Session", Order = 9, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int LondonBarStart { get; set; } = 480;
        [NinjaScriptProperty]   
        [Display(Name = "London Bar End", Description = "End Bar for London Session", Order = 10, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int LondonBarEnd { get; set; } = 960;
        [NinjaScriptProperty]
        [Display(Name = "Frankfurt", Description = "Check if you want to check/trade this Session", Order = 11, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool Frankfurt { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Frankfurt Bar Start", Description = "Start Bar for Frankfurt Session", Order = 12, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int FrankfurtBarStart { get; set; } = 540;
        [NinjaScriptProperty]
        [Display(Name = "Frankfurt Bar End", Description = "End Bar for Frankfurt Session", Order = 13, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int FrankfurtBarEnd { get; set; } = 1020;
       

        [NinjaScriptProperty]
        [Display(Name = "Custom", Description = "Check if you want to check/trade this Session", Order = 14, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool Custom { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Custom Bar Start", Description = "Start Bar for Custom Session", Order = 15, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int CustomBarStart { get; set; } = 1;
        [NinjaScriptProperty]
        [Display(Name = "Custom Bar End", Description = "End Bar for Custom Session", Order = 16, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int CustomBarEnd { get; set; } = 5000;
        


        [NinjaScriptProperty]
        [Display(Name = "Monday", Description = "On these days Trades are allowed", Order = 20, GroupName = "Trading Days - refers to what your System says!")]
        public bool Monday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Tuesday", Description = "On these days Trades are allowed", Order = 21, GroupName = "Trading Days - refers to what your System says!")]
        public bool Tuesday { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Wednesday", Description = "On these days Trades are allowed", Order = 22, GroupName = "Trading Days - refers to what your System says!")]
        public bool Wednesday { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Thursday", Description = "On these days Trades are allowed", Order = 23, GroupName = "Trading Days - refers to what your System says!")]
        public bool Thursday { get; set; } = true;      

        [NinjaScriptProperty]
        [Display(Name = "Friday", Description = "On these days Trades are allowed", Order = 24, GroupName = "Trading Days - refers to what your System says!")]
        public bool Friday { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Saturday", Description = "On these days Trades are allowed", Order = 25, GroupName = "Trading Days - refers to what your System says!")]
        public bool Saturday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Sunday", Description = "On these days Trades are allowed", Order = 26, GroupName = "Trading Days - refers to what your System says!")]
        public bool Sunday { get; set; } = true;


        // Trading Parameters
        [NinjaScriptProperty]
        [Display(Name = "Use As Indicator Only", Description = "If false it will trigger REAL trades!", Order = 1, GroupName = "Attention Actual Trading here if Indicator Only is unchecked!")]

        public bool UseAsIndicatorOnly { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "No Trade if Risk is higher than Max Money to Risk", Description = "Will not enter a Trade if set to true and criterias are met.", Order = 3, GroupName = "Attention Actual Trading here if Indicator Only is unchecked!")]
        public bool NoTradeIfRiskTooHigh { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "R/R Multiplyer for Target", Description = "Will set the Target accordingly.", Order = 4, GroupName = "Attention Actual Trading here if Indicator Only is unchecked!")]
        public double RewardMultiplyer { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "Max Money to Risk", Description = "Set how much money you are willing to risk, it will calculate the Quantity for the trade", Order = 2, GroupName = "Attention Actual Trading here if Indicator Only is unchecked!")]
        public int RiskMaxMoney { get; set; } = 100;

        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Max Quantity - set prop Firm Limit", Description = "Set the maximum Quantity to trade - if you are limited by your prop firm", Order = 5, GroupName = "Attention Actual Trading here if Indicator Only is unchecked!")]
        public int MaxQuantity { get; set; } = 40;


        #endregion
    }
}

#region Wizard settings, neither change nor remove
/*@
<?xml version="1.0"?>
<ScriptProperties xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Calculate>OnPriceChange</Calculate>
  <ConditionalActions>
    <ConditionalAction>
      <Actions />
      <AnyOrAll>All</AnyOrAll>
      <Conditions>
        <WizardConditionGroup>
          <AnyOrAll>Any</AnyOrAll>
          <Conditions>
            <WizardCondition>
              <LeftItem xsi:type="WizardConditionItem">
                <IsExpanded>false</IsExpanded>
                <IsSelected>true</IsSelected>
                <Name>Ask</Name>
                <OffsetType>Arithmetic</OffsetType>
                <AssignedCommand>
                  <Command>GetCurrentAsk({0})</Command>
                  <Parameters>
                    <string>Series1</string>
                    <string>OffsetBuilder</string>
                  </Parameters>
                </AssignedCommand>
                <BarsAgo>0</BarsAgo>
                <CurrencyType>Currency</CurrencyType>
                <Date>2025-10-20T13:30:01.7704978</Date>
                <DayOfWeek>Sunday</DayOfWeek>
                <EndBar>0</EndBar>
                <ForceSeriesIndex>true</ForceSeriesIndex>
                <LookBackPeriod>0</LookBackPeriod>
                <MarketPosition>Long</MarketPosition>
                <Period>0</Period>
                <ReturnType>Number</ReturnType>
                <StartBar>0</StartBar>
                <State>Undefined</State>
                <Time>0001-01-01T00:00:00</Time>
              </LeftItem>
              <Lookback>1</Lookback>
              <Operator>LessEqual</Operator>
              <RightItem xsi:type="WizardConditionItem">
                <IsExpanded>false</IsExpanded>
                <IsSelected>true</IsSelected>
                <Name>Ichimoku Cloud</Name>
                <OffsetType>Arithmetic</OffsetType>
                <AssignedCommand>
                  <Command>IchimokuCloud</Command>
                  <Parameters>
                    <string>AssociatedIndicator</string>
                    <string>BarsAgo</string>
                    <string>OffsetBuilder</string>
                  </Parameters>
                </AssignedCommand>
                <AssociatedIndicator>
                  <AcceptableSeries>Indicator DataSeries CustomSeries DefaultSeries</AcceptableSeries>
                  <CustomProperties>
                    <item>
                      <key>
                        <string>ConversionPeriod</string>
                      </key>
                      <value>
                        <anyType xsi:type="NumberBuilder">
                          <LiveValue xsi:type="xsd:string">9</LiveValue>
                          <BindingValue xsi:type="xsd:string">9</BindingValue>
                          <DefaultValue>0</DefaultValue>
                          <IsInt>true</IsInt>
                          <IsLiteral>true</IsLiteral>
                        </anyType>
                      </value>
                    </item>
                    <item>
                      <key>
                        <string>BasePeriod</string>
                      </key>
                      <value>
                        <anyType xsi:type="NumberBuilder">
                          <LiveValue xsi:type="xsd:string">26</LiveValue>
                          <BindingValue xsi:type="xsd:string">26</BindingValue>
                          <DefaultValue>0</DefaultValue>
                          <IsInt>true</IsInt>
                          <IsLiteral>true</IsLiteral>
                        </anyType>
                      </value>
                    </item>
                    <item>
                      <key>
                        <string>LeadingSpanBPeriod</string>
                      </key>
                      <value>
                        <anyType xsi:type="NumberBuilder">
                          <LiveValue xsi:type="xsd:string">52</LiveValue>
                          <BindingValue xsi:type="xsd:string">52</BindingValue>
                          <DefaultValue>0</DefaultValue>
                          <IsInt>true</IsInt>
                          <IsLiteral>true</IsLiteral>
                        </anyType>
                      </value>
                    </item>
                    <item>
                      <key>
                        <string>SpanDisplacement</string>
                      </key>
                      <value>
                        <anyType xsi:type="NumberBuilder">
                          <LiveValue xsi:type="xsd:string">-26</LiveValue>
                          <BindingValue xsi:type="xsd:string">-26</BindingValue>
                          <DefaultValue>0</DefaultValue>
                          <IsInt>true</IsInt>
                          <IsLiteral>true</IsLiteral>
                        </anyType>
                      </value>
                    </item>
                    <item>
                      <key>
                        <string>LaggingDisplacement</string>
                      </key>
                      <value>
                        <anyType xsi:type="NumberBuilder">
                          <LiveValue xsi:type="xsd:string">26</LiveValue>
                          <BindingValue xsi:type="xsd:string">26</BindingValue>
                          <DefaultValue>0</DefaultValue>
                          <IsInt>true</IsInt>
                          <IsLiteral>true</IsLiteral>
                        </anyType>
                      </value>
                    </item>
                  </CustomProperties>
                  <IndicatorHolder>
                    <IndicatorName>IchimokuCloud</IndicatorName>
                    <Plots>
                      <Plot>
                        <IsOpacityVisible>false</IsOpacityVisible>
                        <BrushSerialize>&lt;SolidColorBrush xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"&gt;#FF00BFFF&lt;/SolidColorBrush&gt;</BrushSerialize>
                        <DashStyleHelper>Solid</DashStyleHelper>
                        <Opacity>100</Opacity>
                        <Width>1</Width>
                        <AutoWidth>false</AutoWidth>
                        <Max>1.7976931348623157E+308</Max>
                        <Min>-1.7976931348623157E+308</Min>
                        <Name>Conversion (Tenkan)</Name>
                        <PlotStyle>Line</PlotStyle>
                      </Plot>
                      <Plot>
                        <IsOpacityVisible>false</IsOpacityVisible>
                        <BrushSerialize>&lt;SolidColorBrush xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"&gt;#FFFFA500&lt;/SolidColorBrush&gt;</BrushSerialize>
                        <DashStyleHelper>Solid</DashStyleHelper>
                        <Opacity>100</Opacity>
                        <Width>1</Width>
                        <AutoWidth>false</AutoWidth>
                        <Max>1.7976931348623157E+308</Max>
                        <Min>-1.7976931348623157E+308</Min>
                        <Name>Base (Kijun)</Name>
                        <PlotStyle>Line</PlotStyle>
                      </Plot>
                      <Plot>
                        <IsOpacityVisible>false</IsOpacityVisible>
                        <BrushSerialize>&lt;SolidColorBrush xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"&gt;#FF008080&lt;/SolidColorBrush&gt;</BrushSerialize>
                        <DashStyleHelper>Solid</DashStyleHelper>
                        <Opacity>100</Opacity>
                        <Width>1</Width>
                        <AutoWidth>false</AutoWidth>
                        <Max>1.7976931348623157E+308</Max>
                        <Min>-1.7976931348623157E+308</Min>
                        <Name>Leading (Senkou) span A</Name>
                        <PlotStyle>Line</PlotStyle>
                      </Plot>
                      <Plot>
                        <IsOpacityVisible>false</IsOpacityVisible>
                        <BrushSerialize>&lt;SolidColorBrush xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"&gt;#FF9370DB&lt;/SolidColorBrush&gt;</BrushSerialize>
                        <DashStyleHelper>Solid</DashStyleHelper>
                        <Opacity>100</Opacity>
                        <Width>1</Width>
                        <AutoWidth>false</AutoWidth>
                        <Max>1.7976931348623157E+308</Max>
                        <Min>-1.7976931348623157E+308</Min>
                        <Name>Leading (Senkou) span B</Name>
                        <PlotStyle>Line</PlotStyle>
                      </Plot>
                      <Plot>
                        <IsOpacityVisible>false</IsOpacityVisible>
                        <BrushSerialize>&lt;SolidColorBrush xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"&gt;#FF008000&lt;/SolidColorBrush&gt;</BrushSerialize>
                        <DashStyleHelper>Solid</DashStyleHelper>
                        <Opacity>100</Opacity>
                        <Width>1</Width>
                        <AutoWidth>false</AutoWidth>
                        <Max>1.7976931348623157E+308</Max>
                        <Min>-1.7976931348623157E+308</Min>
                        <Name>Lagging (Chikou)</Name>
                        <PlotStyle>Line</PlotStyle>
                      </Plot>
                    </Plots>
                  </IndicatorHolder>
                  <IsExplicitlyNamed>false</IsExplicitlyNamed>
                  <IsPriceTypeLocked>false</IsPriceTypeLocked>
                  <PlotOnChart>true</PlotOnChart>
                  <PriceType>Close</PriceType>
                  <SeriesType>Indicator</SeriesType>
                </AssociatedIndicator>
                <BarsAgo>0</BarsAgo>
                <CurrencyType>Currency</CurrencyType>
                <Date>2025-10-20T13:30:01.8230081</Date>
                <DayOfWeek>Sunday</DayOfWeek>
                <EndBar>0</EndBar>
                <ForceSeriesIndex>false</ForceSeriesIndex>
                <LookBackPeriod>0</LookBackPeriod>
                <MarketPosition>Long</MarketPosition>
                <Period>0</Period>
                <ReturnType>Series</ReturnType>
                <StartBar>0</StartBar>
                <State>Undefined</State>
                <Time>0001-01-01T00:00:00</Time>
              </RightItem>
            </WizardCondition>
          </Conditions>
          <IsGroup>false</IsGroup>
          <DisplayName>GetCurrentAsk(Default input) &lt;= IchimokuCloud(9, 26, 52, -26, 26)[0]</DisplayName>
        </WizardConditionGroup>
      </Conditions>
      <SetName>Set 1</SetName>
      <SetNumber>1</SetNumber>
    </ConditionalAction>
  </ConditionalActions>
  <CustomSeries />
  <DataSeries />
  <Description>Utilizing the Ichimoku Cloud for this Strategy.</Description>
  <DisplayInDataBox>true</DisplayInDataBox>
  <DrawHorizontalGridLines>true</DrawHorizontalGridLines>
  <DrawOnPricePanel>true</DrawOnPricePanel>
  <DrawVerticalGridLines>true</DrawVerticalGridLines>
  <EntriesPerDirection>1</EntriesPerDirection>
  <EntryHandling>AllEntries</EntryHandling>
  <ExitOnSessionClose>true</ExitOnSessionClose>
  <ExitOnSessionCloseSeconds>30</ExitOnSessionCloseSeconds>
  <FillLimitOrdersOnTouch>false</FillLimitOrdersOnTouch>
  <InputParameters />
  <IsTradingHoursBreakLineVisible>true</IsTradingHoursBreakLineVisible>
  <IsInstantiatedOnEachOptimizationIteration>true</IsInstantiatedOnEachOptimizationIteration>
  <MaximumBarsLookBack>TwoHundredFiftySix</MaximumBarsLookBack>
  <MinimumBarsRequired>20</MinimumBarsRequired>
  <OrderFillResolution>Standard</OrderFillResolution>
  <OrderFillResolutionValue>1</OrderFillResolutionValue>
  <OrderFillResolutionType>Minute</OrderFillResolutionType>
  <OverlayOnPrice>false</OverlayOnPrice>
  <PaintPriceMarkers>true</PaintPriceMarkers>
  <PlotParameters />
  <RealTimeErrorHandling>StopCancelClose</RealTimeErrorHandling>
  <ScaleJustification>Right</ScaleJustification>
  <ScriptType>Strategy</ScriptType>
  <Slippage>0</Slippage>
  <StartBehavior>WaitUntilFlat</StartBehavior>
  <StopsAndTargets />
  <StopTargetHandling>PerEntryExecution</StopTargetHandling>
  <TimeInForce>Gtc</TimeInForce>
  <TraceOrders>false</TraceOrders>
  <UseOnAddTradeEvent>false</UseOnAddTradeEvent>
  <UseOnAuthorizeAccountEvent>false</UseOnAuthorizeAccountEvent>
  <UseAccountItemUpdate>false</UseAccountItemUpdate>
  <UseOnCalculatePerformanceValuesEvent>true</UseOnCalculatePerformanceValuesEvent>
  <UseOnConnectionEvent>false</UseOnConnectionEvent>
  <UseOnDataPointEvent>true</UseOnDataPointEvent>
  <UseOnFundamentalDataEvent>false</UseOnFundamentalDataEvent>
  <UseOnExecutionEvent>false</UseOnExecutionEvent>
  <UseOnMouseDown>true</UseOnMouseDown>
  <UseOnMouseMove>true</UseOnMouseMove>
  <UseOnMouseUp>true</UseOnMouseUp>
  <UseOnMarketDataEvent>false</UseOnMarketDataEvent>
  <UseOnMarketDepthEvent>false</UseOnMarketDepthEvent>
  <UseOnMergePerformanceMetricEvent>false</UseOnMergePerformanceMetricEvent>
  <UseOnNextDataPointEvent>true</UseOnNextDataPointEvent>
  <UseOnNextInstrumentEvent>true</UseOnNextInstrumentEvent>
  <UseOnOptimizeEvent>true</UseOnOptimizeEvent>
  <UseOnOrderUpdateEvent>false</UseOnOrderUpdateEvent>
  <UseOnPositionUpdateEvent>false</UseOnPositionUpdateEvent>
  <UseOnRenderEvent>true</UseOnRenderEvent>
  <UseOnRestoreValuesEvent>false</UseOnRestoreValuesEvent>
  <UseOnShareEvent>true</UseOnShareEvent>
  <UseOnWindowCreatedEvent>false</UseOnWindowCreatedEvent>
  <UseOnWindowDestroyedEvent>false</UseOnWindowDestroyedEvent>
  <Variables />
  <Name>Ichimoku</Name>
</ScriptProperties>
@*/
#endregion
