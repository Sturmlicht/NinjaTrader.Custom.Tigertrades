using NinjaTrader.Cbi;
using NinjaTrader.Data;
using NinjaTrader.Gui.Tools;
using NinjaTrader.NinjaScript;
using NinjaTrader.NinjaScript.Indicators;
using NinjaTrader.NinjaScript.DrawingTools;
using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Windows.Media;
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

namespace NinjaTrader.NinjaScript.Indicators
{
    public class DayOfWeekSeparator : Indicator
    {
        [NinjaScriptProperty]
        [Display(Name = "Show day labels", GroupName = "General", Order = 1)]
        public bool ShowLabels { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Show separator lines", GroupName = "General", Order = 2)]
        public bool ShowLines { get; set; } = true;

        [NinjaScriptProperty]
        [Range(10, 2000)]
        [Display(Name = "Lookback bars for height calc", GroupName = "General", Order = 3)]
        public int HeightLookback { get; set; } = 200;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Label Brush", GroupName = "Visual", Order = 1)]
        public Brush LabelBrush { get; set; } = Brushes.CornflowerBlue;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Line Brush", GroupName = "Visual", Order = 2)]
        public Brush LineBrush { get; set; } = Brushes.Gray;

        [NinjaScriptProperty]
        [Display(Name = "Line Width", GroupName = "Visual", Order = 3)]
        public int LineWidth { get; set; } = 2;

        [NinjaScriptProperty]
        [Display(Name = "Label offset (ticks)", GroupName = "Visual", Order = 4)]
        public int LabelOffsetTicks { get; set; } = 2;

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description           = "Draws a day label (day + date) at each session start and a vertical separator line.";
                Name                  = "DayOfWeekSeparator";
                Calculate             = Calculate.OnBarClose;
                IsOverlay             = true;
                DisplayInDataBox      = false;
                PaintPriceMarkers     = false;
                IsSuspendedWhileInactive = true;
            }
        }

        protected override void OnBarUpdate()
        {
            // need at least one previous bar to detect date change
            if (CurrentBar < 0)
                return;

            bool isNewDay = CurrentBar == 0 || Times[0][0].Date != Times[0][1].Date;
            if (!isNewDay)
                return;

            // compute approximate chart height using recent bars (to make vertical line span chart)
            int lookback = Math.Min(HeightLookback, CurrentBar + 1);
            double highest = double.MinValue;
            double lowest = double.MaxValue;
            for (int i = 0; i < lookback; i++)
            {
                highest = Math.Max(highest, High[i]);
                lowest  = Math.Min(lowest, Low[i]);
            }

            // safety if something odd
            if (highest == double.MinValue || lowest == double.MaxValue)
                return;

            // padding and coordinates
            double pad = Math.Max(5 * TickSize, (highest - lowest) * 0.03);
            double yTop = highest + pad;
            double yBottom = lowest - pad;

            // build strings and unique tags
            string dayText = Times[0][0].ToString("ddd dd-MMM-yyyy"); // e.g. "Thu 06-Nov-2025"
            string txtTag = $"DayLabel_{CurrentBar}";
            string lineTag = $"DayLine_{CurrentBar}";

            // draw label a bit above the top
            if (ShowLabels)
            {
                double yLabel = yTop + LabelOffsetTicks * TickSize;
                Draw.Text(this, txtTag, dayText, 0, yLabel, LabelBrush);
            }

            // draw vertical separator at this bar from yTop down to yBottom
            if (ShowLines)
                Draw.Line(this, lineTag, false, 0, yTop, 0, yBottom, LineBrush, DashStyleHelper.Solid, LineWidth);
        }
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private DayOfWeekSeparator[] cacheDayOfWeekSeparator;
		public DayOfWeekSeparator DayOfWeekSeparator(bool showLabels, bool showLines, int heightLookback, Brush labelBrush, Brush lineBrush, int lineWidth, int labelOffsetTicks)
		{
			return DayOfWeekSeparator(Input, showLabels, showLines, heightLookback, labelBrush, lineBrush, lineWidth, labelOffsetTicks);
		}

		public DayOfWeekSeparator DayOfWeekSeparator(ISeries<double> input, bool showLabels, bool showLines, int heightLookback, Brush labelBrush, Brush lineBrush, int lineWidth, int labelOffsetTicks)
		{
			if (cacheDayOfWeekSeparator != null)
				for (int idx = 0; idx < cacheDayOfWeekSeparator.Length; idx++)
					if (cacheDayOfWeekSeparator[idx] != null && cacheDayOfWeekSeparator[idx].ShowLabels == showLabels && cacheDayOfWeekSeparator[idx].ShowLines == showLines && cacheDayOfWeekSeparator[idx].HeightLookback == heightLookback && cacheDayOfWeekSeparator[idx].LabelBrush == labelBrush && cacheDayOfWeekSeparator[idx].LineBrush == lineBrush && cacheDayOfWeekSeparator[idx].LineWidth == lineWidth && cacheDayOfWeekSeparator[idx].LabelOffsetTicks == labelOffsetTicks && cacheDayOfWeekSeparator[idx].EqualsInput(input))
						return cacheDayOfWeekSeparator[idx];
			return CacheIndicator<DayOfWeekSeparator>(new DayOfWeekSeparator(){ ShowLabels = showLabels, ShowLines = showLines, HeightLookback = heightLookback, LabelBrush = labelBrush, LineBrush = lineBrush, LineWidth = lineWidth, LabelOffsetTicks = labelOffsetTicks }, input, ref cacheDayOfWeekSeparator);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.DayOfWeekSeparator DayOfWeekSeparator(bool showLabels, bool showLines, int heightLookback, Brush labelBrush, Brush lineBrush, int lineWidth, int labelOffsetTicks)
		{
			return indicator.DayOfWeekSeparator(Input, showLabels, showLines, heightLookback, labelBrush, lineBrush, lineWidth, labelOffsetTicks);
		}

		public Indicators.DayOfWeekSeparator DayOfWeekSeparator(ISeries<double> input , bool showLabels, bool showLines, int heightLookback, Brush labelBrush, Brush lineBrush, int lineWidth, int labelOffsetTicks)
		{
			return indicator.DayOfWeekSeparator(input, showLabels, showLines, heightLookback, labelBrush, lineBrush, lineWidth, labelOffsetTicks);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.DayOfWeekSeparator DayOfWeekSeparator(bool showLabels, bool showLines, int heightLookback, Brush labelBrush, Brush lineBrush, int lineWidth, int labelOffsetTicks)
		{
			return indicator.DayOfWeekSeparator(Input, showLabels, showLines, heightLookback, labelBrush, lineBrush, lineWidth, labelOffsetTicks);
		}

		public Indicators.DayOfWeekSeparator DayOfWeekSeparator(ISeries<double> input , bool showLabels, bool showLines, int heightLookback, Brush labelBrush, Brush lineBrush, int lineWidth, int labelOffsetTicks)
		{
			return indicator.DayOfWeekSeparator(input, showLabels, showLines, heightLookback, labelBrush, lineBrush, lineWidth, labelOffsetTicks);
		}
	}
}

#endregion
