
#region Using declarations
using NinjaTrader.Data;
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion


namespace NinjaTrader.NinjaScript.Indicators
{
	public class RangeCounterMod : Indicator
	{
		private bool	isAdvancedType;
		private string	rangeString;
		private bool	supportsRange;

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= "will calculate and show the remaining ticks for the candle and if wanted will also mark out the potential close of the candle";
				Name						="Range Counter Mod";
				Calculate					= Calculate.OnPriceChange;
				CountDown					= true;
				DisplayInDataBox			= false;
				DrawOnPricePanel			= false;
				IsOverlay					= true;
				IsChartOnly					= true;
				IsOverlay					= true;
				IsSuspendedWhileInactive	= true;
				TextPositionFine			= TextPositionFine.BottomRight;
			}
			else if (State == State.Historical)
			{
				isAdvancedType		= BarsPeriod.BarsPeriodType == BarsPeriodType.HeikenAshi || BarsPeriod.BarsPeriodType == BarsPeriodType.PriceOnVolume || BarsPeriod.BarsPeriodType == BarsPeriodType.Volumetric;
				bool isOtherType	= BarsPeriod.ToString().IndexOf("Range", StringComparison.Ordinal) >= 0 || BarsPeriod.ToString().IndexOf(Custom.Resource.BarsPeriodTypeNameRange, StringComparison.Ordinal) >= 0;

				if (BarsPeriod.BarsPeriodType == BarsPeriodType.Range || BarsPeriod.BaseBarsPeriodType == BarsPeriodType.Range && isAdvancedType ||
					BarsArray[0].BarsType.BuiltFrom == BarsPeriodType.Tick && isOtherType)
					supportsRange = true;
			}
		}

		protected override void OnBarUpdate()
		{
			if (BarsArray == null || BarsArray.Length == 0 || CurrentBar < 1)
				return;

			if (supportsRange)
			{
				double	high		= High.GetValueAt(Bars.Count - 1 - (Calculate == Calculate.OnBarClose ? 1 : 0));
				double	low			= Low.GetValueAt(Bars.Count - 1 - (Calculate == Calculate.OnBarClose ? 1 : 0));
				double	close		= Close.GetValueAt(Bars.Count - 1 - (Calculate == Calculate.OnBarClose ? 1 : 0));
				int		actualRange	= (int)Math.Round(Math.Max(close - low, high - close) / Bars.Instrument.MasterInstrument.TickSize);
				double	rangeCount	= CountDown ? (isAdvancedType ? BarsPeriod.BaseBarsPeriodValue : BarsPeriod.Value) - actualRange : actualRange;

				rangeString	= CountDown ? string.Format(Custom.Resource.RangeCounterRemaing, rangeCount) : string.Format(Custom.Resource.RangerCounterCount, rangeCount);

				double InstrumentRange = (double)BarsPeriodType.Range;
                double UpLine = low + BarsPeriod.Value * TickSize;
				double DownLine = high - BarsPeriod.Value* TickSize;
				Draw.Line(this, "UP", 1, UpLine, -3, UpLine, Color);
				Draw.Line(this, "Down", 1, DownLine, -3, DownLine, Color);


            }
			else
				rangeString = Custom.Resource.RangeCounterBarError;

			Draw.TextFixedFine(this, "NinjaScriptInfo", rangeString, TextPositionFine);


        }



        #region Properties
        [NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "CountDown", Order = 1, GroupName = "NinjaScriptParameters")]
		public bool CountDown { get; set; }

		[Display(ResourceType = typeof(Custom.Resource), Name = "GuiPropertyNameTextPosition", GroupName = "PropertyCategoryVisual", Order = 70)]
		public TextPositionFine TextPositionFine { get; set; }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "Color", Order = 5, GroupName = "PropertyCategoryVisual")]
        public Brush Color { get; set; } = Brushes.SpringGreen;
        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private RangeCounterMod[] cacheRangeCounterMod;
		public RangeCounterMod RangeCounterMod(bool countDown, Brush color)
		{
			return RangeCounterMod(Input, countDown, color);
		}

		public RangeCounterMod RangeCounterMod(ISeries<double> input, bool countDown, Brush color)
		{
			if (cacheRangeCounterMod != null)
				for (int idx = 0; idx < cacheRangeCounterMod.Length; idx++)
					if (cacheRangeCounterMod[idx] != null && cacheRangeCounterMod[idx].CountDown == countDown && cacheRangeCounterMod[idx].Color == color && cacheRangeCounterMod[idx].EqualsInput(input))
						return cacheRangeCounterMod[idx];
			return CacheIndicator<RangeCounterMod>(new RangeCounterMod(){ CountDown = countDown, Color = color }, input, ref cacheRangeCounterMod);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.RangeCounterMod RangeCounterMod(bool countDown, Brush color)
		{
			return indicator.RangeCounterMod(Input, countDown, color);
		}

		public Indicators.RangeCounterMod RangeCounterMod(ISeries<double> input , bool countDown, Brush color)
		{
			return indicator.RangeCounterMod(input, countDown, color);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.RangeCounterMod RangeCounterMod(bool countDown, Brush color)
		{
			return indicator.RangeCounterMod(Input, countDown, color);
		}

		public Indicators.RangeCounterMod RangeCounterMod(ISeries<double> input , bool countDown, Brush color)
		{
			return indicator.RangeCounterMod(input, countDown, color);
		}
	}
}

#endregion
