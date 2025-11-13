//
// Copyright (C) 2025, NinjaTrader LLC <www.ninjatrader.com>.
// NinjaTrader reserves the right to modify or overwrite this NinjaScript component with each release.
//
#region Using declarations
using System;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
#endregion

// This namespace holds indicators in this folder and is required. Do not change it.
namespace NinjaTrader.NinjaScript.Indicators
{
	/// <summary>
	/// The Average True Range (ATR) is a measure of volatility. It was introduced by Welles Wilder
	/// in his book 'New Concepts in Technical Trading Systems' and has since been used as a component
	/// of many indicators and trading systems.
	/// </summary>
	public class ATR_Ticks : Indicator
	{
		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				Description					= Custom.Resource.NinjaScriptIndicatorDescriptionATR;
				Name						= "ATR-Ticks";
				IsSuspendedWhileInactive	= true;
				Period						= 14;

				AddPlot(Brushes.DarkCyan, Custom.Resource.NinjaScriptIndicatorNameATR);
			}
		}

		protected override void OnBarUpdate()
		{
			double high0	= High[0];
			double low0		= Low[0];

			if (CurrentBar == 0)
				Value[0] = (high0 - low0) / TickSize;
			else
            {
                double close1 = Close[1];
                double tr1 = Math.Abs(low0 - close1) / TickSize;
                double tr2 = (high0 - low0) / TickSize;
                double tr3 = Math.Abs(high0 - close1) / TickSize;
                double trueRange = Math.Max(tr1, Math.Max(tr2, tr3));
                Value[0] = ((Math.Min(CurrentBar + 1, Period) - 1) * Value[1] + trueRange) / Math.Min(CurrentBar + 1, Period);
            }
        }

		#region Properties
		[Range(1, int.MaxValue), NinjaScriptProperty]
		[Display(ResourceType = typeof(Custom.Resource), Name = "Period", GroupName = "NinjaScriptParameters", Order = 0)]
		public int Period { get; set; }
		#endregion
	}
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private ATR_Ticks[] cacheATR_Ticks;
		public ATR_Ticks ATR_Ticks(int period)
		{
			return ATR_Ticks(Input, period);
		}

		public ATR_Ticks ATR_Ticks(ISeries<double> input, int period)
		{
			if (cacheATR_Ticks != null)
				for (int idx = 0; idx < cacheATR_Ticks.Length; idx++)
					if (cacheATR_Ticks[idx] != null && cacheATR_Ticks[idx].Period == period && cacheATR_Ticks[idx].EqualsInput(input))
						return cacheATR_Ticks[idx];
			return CacheIndicator<ATR_Ticks>(new ATR_Ticks(){ Period = period }, input, ref cacheATR_Ticks);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.ATR_Ticks ATR_Ticks(int period)
		{
			return indicator.ATR_Ticks(Input, period);
		}

		public Indicators.ATR_Ticks ATR_Ticks(ISeries<double> input , int period)
		{
			return indicator.ATR_Ticks(input, period);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.ATR_Ticks ATR_Ticks(int period)
		{
			return indicator.ATR_Ticks(Input, period);
		}

		public Indicators.ATR_Ticks ATR_Ticks(ISeries<double> input , int period)
		{
			return indicator.ATR_Ticks(input, period);
		}
	}
}

#endregion
