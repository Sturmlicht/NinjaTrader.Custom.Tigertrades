//
// Copyright (C) 2025, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
#region Using declarations
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

// This namespace holds indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	
	public class ImbalancesFinder : Indicator
	{
      

        protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description                 = "Will find Imbalances on the Chart";
				Name                        = "ImbalancesFinder";
				IsOverlay                   = true;
				IsSuspendedWhileInactive    = true;
	


            }
			else if (State == State.Configure)
			{
				
			}
		}

		protected override void OnBarUpdate() 
		{

            if (BarsInProgress != 0  
				|| CurrentBars[0] < 1 
               )
                return;

         
            //bullish Imbalance
            if (Close[1] >= Open[1]
				&& Close[0] >= Open[0]
				&& Close[1] < Open[0]
				)
			{

				if (High[1] < Low[0]) //We got a Vaccum Block / Price Gap
				{
                    Draw.Rectangle(this, $"Region {CurrentBar}", true,  1, Close[1], -2, Open[0], Brushes.Transparent, BullishVaccumColor, RegionOpacity);
					//Print($"Found Bullish Vaccum Block at {Times[0][0].Date}, {CurrentBar}");
                }

                if (High[1] > Low[0]) //We have a Volume Imbalance
				{
                    Draw.Rectangle(this, $"Region {CurrentBar}", true, 1, Close[1], -2, Open[0], Brushes.Transparent, BullishImbalanceColor, RegionOpacity);
                    //Print($"Found Bullish Volume Imbalance Block at {Times[0][0].Date}, {CurrentBar}");
                }

				
				
			}


            //bearish Imbalance
            if (Close[1] <= Open[1]
                && Close[0] <= Open[0]
                && Close[1] > Open[0]
                )
            {

                if (Low[1] > High[0]) //We got a Vaccum Block / Price Gap
                {
                    Draw.Rectangle(this, $"Region {CurrentBar}", true, 1, Close[1], -2, Open[0], Brushes.Transparent, BearishVaccumColor, RegionOpacity);
                    Print($"Found Bearish Vaccum Block at {Times[0][0].Date}, {CurrentBar}");
                }

                if (Low[1] < High[0]) //We have a Volume Imbalance
                {
                    Draw.Rectangle(this, $"Region {CurrentBar}", true, 1, Close[1], -2, Open[0], Brushes.Transparent, BearishImbalanceColor, RegionOpacity);
                    Print($"Found Bearish Volume Imbalance Block at {Times[0][0].Date}, {CurrentBar}");
                }



            }

        }

		#region Properties




        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Bullish Vaccum Color", Description = "Select the color Region Area", Order = 13, GroupName = "NinjaScriptParameters")]
        public Brush BullishVaccumColor { get; set; } = Brushes.CornflowerBlue;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Bearish Vaccum Color", Description = "Select the color Region Area", Order = 13, GroupName = "NinjaScriptParameters")]
        public Brush BearishVaccumColor { get; set; } = Brushes.OrangeRed;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Bullish Imbalance Color", Description = "Select the color Region Area", Order = 13, GroupName = "NinjaScriptParameters")]
        public Brush BullishImbalanceColor { get; set; } = Brushes.CadetBlue;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Bearish Imbalance Color", Description = "Select the color Region Area", Order = 13, GroupName = "NinjaScriptParameters")]
        public Brush BearishImbalanceColor { get; set; } = Brushes.Orange;

        [Range(0, 100), NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Region Opacity", GroupName = "NinjaScriptParameters", Order = 14)]
        public int RegionOpacity { get; set; } = 50;
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ImbalancesFinder[] cacheImbalancesFinder;
		public ImbalancesFinder ImbalancesFinder(Brush bullishVaccumColor, Brush bearishVaccumColor, Brush bullishImbalanceColor, Brush bearishImbalanceColor, int regionOpacity)
		{
			return ImbalancesFinder(Input, bullishVaccumColor, bearishVaccumColor, bullishImbalanceColor, bearishImbalanceColor, regionOpacity);
		}

		public ImbalancesFinder ImbalancesFinder(ISeries<double> input, Brush bullishVaccumColor, Brush bearishVaccumColor, Brush bullishImbalanceColor, Brush bearishImbalanceColor, int regionOpacity)
		{
			if (cacheImbalancesFinder != null)
				for (int idx = 0; idx < cacheImbalancesFinder.Length; idx++)
					if (cacheImbalancesFinder[idx] != null && cacheImbalancesFinder[idx].BullishVaccumColor == bullishVaccumColor && cacheImbalancesFinder[idx].BearishVaccumColor == bearishVaccumColor && cacheImbalancesFinder[idx].BullishImbalanceColor == bullishImbalanceColor && cacheImbalancesFinder[idx].BearishImbalanceColor == bearishImbalanceColor && cacheImbalancesFinder[idx].RegionOpacity == regionOpacity && cacheImbalancesFinder[idx].EqualsInput(input))
						return cacheImbalancesFinder[idx];
			return CacheIndicator<ImbalancesFinder>(new ImbalancesFinder(){ BullishVaccumColor = bullishVaccumColor, BearishVaccumColor = bearishVaccumColor, BullishImbalanceColor = bullishImbalanceColor, BearishImbalanceColor = bearishImbalanceColor, RegionOpacity = regionOpacity }, input, ref cacheImbalancesFinder);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ImbalancesFinder ImbalancesFinder(Brush bullishVaccumColor, Brush bearishVaccumColor, Brush bullishImbalanceColor, Brush bearishImbalanceColor, int regionOpacity)
		{
			return indicator.ImbalancesFinder(Input, bullishVaccumColor, bearishVaccumColor, bullishImbalanceColor, bearishImbalanceColor, regionOpacity);
		}

		public Indicators.ImbalancesFinder ImbalancesFinder(ISeries<double> input , Brush bullishVaccumColor, Brush bearishVaccumColor, Brush bullishImbalanceColor, Brush bearishImbalanceColor, int regionOpacity)
		{
			return indicator.ImbalancesFinder(input, bullishVaccumColor, bearishVaccumColor, bullishImbalanceColor, bearishImbalanceColor, regionOpacity);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ImbalancesFinder ImbalancesFinder(Brush bullishVaccumColor, Brush bearishVaccumColor, Brush bullishImbalanceColor, Brush bearishImbalanceColor, int regionOpacity)
		{
			return indicator.ImbalancesFinder(Input, bullishVaccumColor, bearishVaccumColor, bullishImbalanceColor, bearishImbalanceColor, regionOpacity);
		}

		public Indicators.ImbalancesFinder ImbalancesFinder(ISeries<double> input , Brush bullishVaccumColor, Brush bearishVaccumColor, Brush bullishImbalanceColor, Brush bearishImbalanceColor, int regionOpacity)
		{
			return indicator.ImbalancesFinder(input, bullishVaccumColor, bearishVaccumColor, bullishImbalanceColor, bearishImbalanceColor, regionOpacity);
		}
	}
}

#endregion
