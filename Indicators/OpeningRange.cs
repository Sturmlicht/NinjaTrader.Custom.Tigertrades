#region Using declarations
using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.FIX;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.DrawingTools;
using NinjaTrader.NinjaScript.Indicators;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography.X509Certificates;
using System.Windows.Media;
using System.Xml.Serialization;



#endregion

namespace NinjaTrader.NinjaScript.Indicators
{
    public class OpeningRange : Indicator
    {
        private int anchorBar = -1;
        private double anchorHigh = 0;
        private double anchorLow = 0;
        private Indicators.BarCounter BarNo;


        int CheckedBar;
        System.DateTime CheckedDate;
        int CheckedMonth;
        int CheckedDay;

        // Set BarNumber based on session selection  - UTC and EST do not have daylight saving! 
        // we will put the Number always one ahead, as the BarCounter triggers after Close and will lead to delays in this indicator
        // US always opens at 9:30 EST -> 14:30 UTC no DST
        private int BarNumberUS = 929;
        // Asia does not have DST and opens at 9AM JST =>  12 AM (Midnight) UTC
        private int BarNumberAsia = 119;
        // London has DST, Winter opens at 7am UTC (BarNo. 480), Summer opens at UTC+1 so 8 am UTC (BarNo. 540)
        //the timeswitch takes place on the last Sunday in March to Summer Time,
        //and on the last Sunday in October to Winter time
        //this script has an automated check to switch the number!
        private int BarNumberLondon = 479;
        //Frankfurt has DST, It is always opening 1 hour before London!
        //it opens at 8am local time -> Winter 6am UTC (BarNo. 480), Summer 7am  UTC (BarNo. 480)
        //the switch happens also on the last Sunday in March and October
        private int BarNumberFrankfurt = 419;
        private int BarNumberMarket = 1; //it's allways the first candle
        


        DayOfWeek Monday = DayOfWeek.Monday;
        DayOfWeek Tuesday = DayOfWeek.Tuesday;
        DayOfWeek Wednesday = DayOfWeek.Wednesday;
        DayOfWeek Thursday = DayOfWeek.Thursday;
        DayOfWeek Friday = DayOfWeek.Friday;

        System.DayOfWeek currentDay;
        System.DateTime barDate;

        bool DayAfterDST;  //in case someone does not want to trade on those days

        double GapHigh;
        double GapLow;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = "Plots the Ópening Range of the Sessions of your choosing. Sessions are based on Bar Count, so universal. It also checks for Daylight Saving Time for London and Frankfurt Session. And it can also plot the Gab from the Market Open. Use and change to your liking. ~ Tigertrades";
                Name = "Opening Range";

                IsOverlay = true;
                AddPlot(Brushes.Yellow, "Opening High");
                AddPlot(Brushes.Yellow, "Opening Low");
                AddPlot(GapColor, "Gap High");
                AddPlot(GapColor, "Gap Low");
            }



            else if (State == State.Configure)
            {
                AddDataSeries(Data.BarsPeriodType.Minute, 1);
            }
            else if (State == State.DataLoaded)
            {
                // Adjust parameters as needed for your BarCounter - it will always take the 1 Minute Timeframe
                BarNo = BarCounter(Closes[1], false, Brushes.Gray, 14, -50, true);
                            

            }
        }

        protected override void OnBarUpdate()
        {
            if (CurrentBar < OpeningRangeMinutes+1)
                return;


            #region DST
            //Taking care of daylight saving time 
            currentDay = Times[0][0].DayOfWeek;
            barDate = Times[0][0].Date; // session / calendar date for the current bar

            //Print(currentDay);
            //Print(barDate.DayOfYear);
            //Print(barDate.Month);
            //Print(barDate.Day);
            //Print(BarNumberLondon);

            if (CheckedDay != barDate.Day)
            {
                if (DayAfterDST && CheckedDay != barDate.Day)
                { DayAfterDST = false; }

                CheckedBar=CurrentBar;
                CheckedMonth=barDate.Month;
                CheckedDay=barDate.Day;
                //Print(CheckedMonth);
                //Print(barDate);
                //Print(BarNumberLondon);

                //Winter
                if (
                    ((London && BarNumberLondon != 479) || (Frankfurt && BarNumberFrankfurt !=419))
                    &&
                    (
                    (currentDay == Monday && barDate.Month == 10 && barDate.Day >= 26) //the switch happened on the previous Sunday so we should switch
                    || (currentDay == Tuesday && barDate.Month == 10 && barDate.Day >= 27)
                    || (currentDay == Wednesday && barDate.Month == 10 && barDate.Day >= 28)
                    || (currentDay == Thursday && barDate.Month == 10 && barDate.Day >= 29)
                    || (currentDay == Friday && barDate.Month == 10 && barDate.Day >= 30)
                    || (barDate.Month >= 11 && barDate.Month <= 2) //November to February are Definitly Wintertime
                    || (currentDay == Friday && barDate.Month == 3 && barDate.Day <= 28) //Switch should happen at the following weekend to summer
                    || (currentDay == Thursday && barDate.Month == 3 && barDate.Day <= 27)
                    || (currentDay == Wednesday && barDate.Month == 3 && barDate.Day <= 26)
                    || (currentDay == Tuesday && barDate.Month == 3 && barDate.Day <= 25)
                    || (currentDay == Monday && barDate.Month == 3 && barDate.Day <= 25)
                    ))
                {
                        BarNumberLondon = 479; //switching to Wintertime
                        BarNumberFrankfurt =419;
                        Print($"Switched to Wintertime for London Frankfurt {barDate}, {currentDay}");
                        DayAfterDST = true;

                }
                
                    //Summer
                if (
                    ((London && BarNumberLondon != 539) || (Frankfurt && BarNumberFrankfurt !=479))
                    && 
                    (
                    (currentDay == Monday && barDate.Month == 3 && barDate.Day >= 26) //the switch happened on the previous Sunday so we should switch
                    || (currentDay == Tuesday && barDate.Month == 3 && barDate.Day >= 27)
                    || (currentDay == Wednesday && barDate.Month == 3 && barDate.Day >= 28)
                    || (currentDay == Thursday && barDate.Month == 3 && barDate.Day >= 29)
                    || (currentDay == Friday && barDate.Month == 3 && barDate.Day >= 30)
                    || (barDate.Month >= 4 && barDate.Month <= 9) //November to February are Definitly Wintertime
                    || (currentDay == Friday && barDate.Month == 10 && barDate.Day <= 28) //Switch should happen at the following weekend to summer
                    || (currentDay == Thursday && barDate.Month == 10 && barDate.Day <= 27)
                    || (currentDay == Wednesday && barDate.Month == 10 && barDate.Day <= 26)
                    || (currentDay == Tuesday && barDate.Month == 10 && barDate.Day <= 25)
                    || (currentDay == Monday && barDate.Month == 10 && barDate.Day <= 25)
                    ))
                {
                    BarNumberLondon = 539; //switching to Summertime
                    BarNumberFrankfurt = 479;
                    Print($"Switched to Summertime for London and Franfurt {barDate}, {currentDay}");

                    DayAfterDST = true;

                }


                
            }
            #endregion

            #region Highlight
            if (US && BarNo[0] == BarNumberUS)
            {
                if (ShowHighlight) Highlight();
                Draw.Text(this, $"US Open {barDate}", $"US Open\n{barDate.Month}/{barDate.Day}/{barDate.Year}", 2, High[0] + 20 * TickSize, TextColor);
            }
            if (Asia && BarNo[0] == BarNumberAsia)
            {
                if (ShowHighlight) Highlight();
                Draw.Text(this, $"Asia Open {barDate}", $"Asia Open\n{barDate.Month}/{barDate.Day}/{barDate.Year}", 2, High[0] + 20 * TickSize, TextColor);
            }
            if (London && BarNo[0] == BarNumberLondon)
            {
                if (ShowHighlight) Highlight();
                Draw.Text(this, $"London Open {barDate}", $"London Open\n{barDate.Month}/{barDate.Day}/{barDate.Year}", 2, High[0] + 20 * TickSize, TextColor);
            }
            if (Frankfurt&& BarNo[0] == BarNumberFrankfurt)
            {
                if (ShowHighlight) Highlight();
                Draw.Text(this, $"Frankfurt Open {barDate}", $"Frankfurt Open\n{barDate.Month}/{barDate.Day}/{barDate.Year}", 2, High[0] + 20 * TickSize, TextColor);
            }
            if (Market && Bars.IsFirstBarOfSession)
            {
                if (ShowHighlight) Highlight();
                Draw.Text(this, $"Market Open {barDate}", $"Market Open\n{barDate.Month}/{barDate.Day}/{barDate.Year}", 2, High[0] + 20 * TickSize, TextColor);
            }

            #endregion

            // Reset at session start
            if (Bars.IsFirstBarOfSession)
            {
                anchorBar = -1;
                anchorHigh = 0;
                anchorLow = 0;

                if (PlotTheGap)
                {

                    //GapHigh = Math.Min(Math.Max(Highs[1][1], Lows[1][0]) , Math.Max(Highs[1][0], Lows[1][1]));
                    //GapLow = Math.Max(Math.Min(Lows[1][0], Highs[1][1]) , Math.Min(Lows[1][1], Highs[1][0]));

                    GapHigh = Close[1];
                    GapLow = Open[0];

                    Draw.Rectangle(this, $"Gap {barDate}", 0, GapLow, -1320, GapHigh, GapColor);
                    GapUpper[0] = GapHigh;
                    GapLower[0] = GapLow;


                }
                
            }
            if (GapHigh == 0.0)
            {
                GapHigh= Open[0];
                GapLow= Close[1];
            }


            if (US && BarNo[0] == BarNumberUS + OpeningRangeMinutes)
            {
                CalculateOpeningHighLow();
            }
            if (Asia && BarNo[0] == BarNumberAsia + OpeningRangeMinutes)
            {
                CalculateOpeningHighLow();
            }
            if (London && BarNo[0] == BarNumberLondon + OpeningRangeMinutes) 
            {
                CalculateOpeningHighLow();
            }
            if (Frankfurt && BarNo[0] == BarNumberFrankfurt + OpeningRangeMinutes)
            {
                CalculateOpeningHighLow();
            }
            if (Market && BarNo[0] == BarNumberMarket + OpeningRangeMinutes)
            {
                CalculateOpeningHighLow();
            }



            // Plot the anchored values until session end
            if (CurrentBars[1] >= anchorBar-1 && CurrentBars[1]<= anchorBar + HowLongToPlot )
            {
                Upper[0] = anchorHigh;
                Lower[0] = anchorLow;

            }

            if (PlotTheGap)
            {
                GapUpper[0] = GapHigh;
                GapLower[0] = GapLow;
            }


        }
        private void CalculateOpeningHighLow()
        {
            anchorBar = CurrentBars[1];


            // Calculate highest high and lowest low of previous x bars
            double high = Highs[1][OpeningRangeMinutes];
            double low = Lows[1][OpeningRangeMinutes];
            for (int i = OpeningRangeMinutes-1; i >= 1; i--)
            {
                
                high = Math.Max(high, Highs[1][i]);
                low = Math.Min(low, Lows[1][i]);
            }

            anchorHigh = high;
            anchorLow = low;

        }

        private void Highlight()
        {//i am currently unsure why I need to offset the Highlight by 1 as it should trigger on bar close
            Draw.RegionHighlightX(this, $"Opening Range {CurrentBar}", 0, -(OpeningRangeMinutes-1), HighlightColor, HighlightAreaColor, HighlightOpacity);

        }

        #region Properties
        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Lower => Values[1];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> Upper => Values[0];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> GapUpper => Values[2];

        [Browsable(false)]
        [XmlIgnore]
        public Series<double> GapLower => Values[3];

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
        [Display(Name = "Region Highlight Area Color", Order = 6, GroupName = "3 - Visuals")]
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
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private OpeningRange[] cacheOpeningRange;
		public OpeningRange OpeningRange(int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return OpeningRange(Input, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}

		public OpeningRange OpeningRange(ISeries<double> input, int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			if (cacheOpeningRange != null)
				for (int idx = 0; idx < cacheOpeningRange.Length; idx++)
					if (cacheOpeningRange[idx] != null && cacheOpeningRange[idx].OpeningRangeMinutes == openingRangeMinutes && cacheOpeningRange[idx].Asia == asia && cacheOpeningRange[idx].London == london && cacheOpeningRange[idx].Frankfurt == frankfurt && cacheOpeningRange[idx].US == uS && cacheOpeningRange[idx].Market == market && cacheOpeningRange[idx].HighlightColor == highlightColor && cacheOpeningRange[idx].HighlightAreaColor == highlightAreaColor && cacheOpeningRange[idx].HighlightOpacity == highlightOpacity && cacheOpeningRange[idx].GapColor == gapColor && cacheOpeningRange[idx].ShowHighlight == showHighlight && cacheOpeningRange[idx].TextColor == textColor && cacheOpeningRange[idx].HowLongToPlot == howLongToPlot && cacheOpeningRange[idx].PlotTheGap == plotTheGap && cacheOpeningRange[idx].EqualsInput(input))
						return cacheOpeningRange[idx];
			return CacheIndicator<OpeningRange>(new OpeningRange(){ OpeningRangeMinutes = openingRangeMinutes, Asia = asia, London = london, Frankfurt = frankfurt, US = uS, Market = market, HighlightColor = highlightColor, HighlightAreaColor = highlightAreaColor, HighlightOpacity = highlightOpacity, GapColor = gapColor, ShowHighlight = showHighlight, TextColor = textColor, HowLongToPlot = howLongToPlot, PlotTheGap = plotTheGap }, input, ref cacheOpeningRange);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.OpeningRange OpeningRange(int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OpeningRange(Input, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}

		public Indicators.OpeningRange OpeningRange(ISeries<double> input , int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OpeningRange(input, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.OpeningRange OpeningRange(int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OpeningRange(Input, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}

		public Indicators.OpeningRange OpeningRange(ISeries<double> input , int openingRangeMinutes, bool asia, bool london, bool frankfurt, bool uS, bool market, Brush highlightColor, Brush highlightAreaColor, int highlightOpacity, Brush gapColor, bool showHighlight, Brush textColor, int howLongToPlot, bool plotTheGap)
		{
			return indicator.OpeningRange(input, openingRangeMinutes, asia, london, frankfurt, uS, market, highlightColor, highlightAreaColor, highlightOpacity, gapColor, showHighlight, textColor, howLongToPlot, plotTheGap);
		}
	}
}

#endregion
