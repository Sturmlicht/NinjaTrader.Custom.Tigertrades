#region Using declarations
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
using NinjaTrader.Cbi;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.SuperDom;
using NinjaTrader.Gui.Tools;
using NinjaTrader.Data;
using NinjaTrader.NinjaScript;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.NinjaScript.DrawingTools;
#endregion

//This namespace holds Indicators in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.Indicators
{
    public class BarCounter : Indicator
    {
        private int _barCntr = 1;
        private bool _showOddNumbers = true;
        private Brush _textColorDefinedbyUser = Brushes.Gray;
        private int _pixelsAboveBelowBar = -50;
        private bool _textIsBelowBars = true;
        private int _fontSize = 14;
        

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description = @"Labels Bars by number from session start, Ninjascript ready for use in Strategy Builder ";
                Name = "BarCounter";
                Calculate = Calculate.OnBarClose;
                IsOverlay = true;
                DisplayInDataBox = false;
                DrawOnPricePanel = true;
                //DrawHorizontalGridLines						= true;
                //DrawVerticalGridLines						= false;
                PaintPriceMarkers = false;
                ScaleJustification = NinjaTrader.Gui.Chart.ScaleJustification.Right;
                //Disable this property if your indicator requires custom values that cumulate with each new market data event. 
                //See Help Guide for additional information.
                IsSuspendedWhileInactive = true;
                TextColor = Brushes.Gray;
				AddPlot(Brushes.Transparent, "barCount");
                



            }
            else if (State == State.Configure)
            {
                if (_pixelsAboveBelowBar > 0 && _textIsBelowBars == true)
                    _pixelsAboveBelowBar = _pixelsAboveBelowBar * -1;
            }
        }



        protected override void OnBarUpdate()
        {
            //Print("Bar Counter called - OnBarUpdate at Bar: " + CurrentBar);
            double _textYStartingPoint = High[0]; //position text above or below bars
            if (_textIsBelowBars)
                _textYStartingPoint = Low[0];

            //Add your custom indicator logic here.
            NinjaTrader.Gui.Tools.SimpleFont myFont = new NinjaTrader.Gui.Tools.SimpleFont("Courier New", 14) { Size = _fontSize, Bold = false };

          			
			
			if (Bars.IsFirstBarOfSession) //start counting from first bar of session
            {
               Value[0]  =  _barCntr = 1;
            }
            else
               Value[0] =   _barCntr++;

            if (_showOddNumbers)
            {
                if (_barCntr % 2 != 0) //is odd number
                    Draw.Text(this, CurrentBar.ToString(), true, _barCntr.ToString(), 0, _textYStartingPoint, _pixelsAboveBelowBar, _textColorDefinedbyUser, myFont, TextAlignment.Center, null, null, 1);
            }
            else
            {
                if (_barCntr % 2 == 0) //is even number
                    Draw.Text(this, CurrentBar.ToString(), true, _barCntr.ToString(), 0, _textYStartingPoint, _pixelsAboveBelowBar, _textColorDefinedbyUser, myFont, TextAlignment.Center, null, null, 1);
            }
        }

        #region Properties
        [NinjaScriptProperty]
        [Display(Name = "ShowOddNumbers", Description = "Show Odd or Even Numbers?", Order = 1, GroupName = "Visual")]
        public bool ShowOddNumbers
        {
            get { return _showOddNumbers; }
            set { _showOddNumbers = value; }
        }

        [NinjaScriptProperty]
        [XmlIgnore]
        [Display(Name = "TextColor", Description = "Text Color ", Order = 2, GroupName = "Visual")]
        public Brush TextColor
        {
            get { return _textColorDefinedbyUser; }
            set { _textColorDefinedbyUser = value; }
        }

        [Range(4, 40)]
        [NinjaScriptProperty]
        [Display(Name = "TextFontSize", Description = "Text Font Size", Order = 3, GroupName = "Visual")]
        public int TextFontSize
        {
            get { return _fontSize; }
            set { _fontSize = value; }
        }

        [Browsable(false)]
        public string TextColorSerializable
        {
            get { return Serialize.BrushToString(TextColor); }
            set { TextColor = Serialize.StringToBrush(value); }
        }

        [Range(0, 2000)]
        [NinjaScriptProperty]
        [Display(Name = "PixelsAboveBelow", Description = "Text Offset in Pixels from Bar", Order = 4, GroupName = "Visual")]
        public int PixelsAboveBelow
        {
            get { return Math.Abs(_pixelsAboveBelowBar); }
            set { _pixelsAboveBelowBar = value; }
        }

        [NinjaScriptProperty]
        [Display(Name = "TextIsBelowBars", Description = "Display Text Above/Below Bars", Order = 5, GroupName = "Visual")]
        public bool TextIsBelowBars
        {
            get { return _textIsBelowBars; }
            set { _textIsBelowBars = value; }
        }

       [Browsable(false)]
		[XmlIgnore]
		public Series<double> barCount
		{
			get { return Values[0]; }
		}  
		

        #endregion
    }
}

#region NinjaScript generated code. Neither change nor remove.

namespace NinjaTrader.NinjaScript.Indicators
{
	public partial class Indicator : NinjaTrader.Gui.NinjaScript.IndicatorRenderBase
	{
		private BarCounter[] cacheBarCounter;
		public BarCounter BarCounter(bool showOddNumbers, Brush textColor, int textFontSize, int pixelsAboveBelow, bool textIsBelowBars)
		{
			return BarCounter(Input, showOddNumbers, textColor, textFontSize, pixelsAboveBelow, textIsBelowBars);
		}

		public BarCounter BarCounter(ISeries<double> input, bool showOddNumbers, Brush textColor, int textFontSize, int pixelsAboveBelow, bool textIsBelowBars)
		{
			if (cacheBarCounter != null)
				for (int idx = 0; idx < cacheBarCounter.Length; idx++)
					if (cacheBarCounter[idx] != null && cacheBarCounter[idx].ShowOddNumbers == showOddNumbers && cacheBarCounter[idx].TextColor == textColor && cacheBarCounter[idx].TextFontSize == textFontSize && cacheBarCounter[idx].PixelsAboveBelow == pixelsAboveBelow && cacheBarCounter[idx].TextIsBelowBars == textIsBelowBars && cacheBarCounter[idx].EqualsInput(input))
						return cacheBarCounter[idx];
			return CacheIndicator<BarCounter>(new BarCounter(){ ShowOddNumbers = showOddNumbers, TextColor = textColor, TextFontSize = textFontSize, PixelsAboveBelow = pixelsAboveBelow, TextIsBelowBars = textIsBelowBars }, input, ref cacheBarCounter);
		}
	}
}

namespace NinjaTrader.NinjaScript.MarketAnalyzerColumns
{
	public partial class MarketAnalyzerColumn : MarketAnalyzerColumnBase
	{
		public Indicators.BarCounter BarCounter(bool showOddNumbers, Brush textColor, int textFontSize, int pixelsAboveBelow, bool textIsBelowBars)
		{
			return indicator.BarCounter(Input, showOddNumbers, textColor, textFontSize, pixelsAboveBelow, textIsBelowBars);
		}

		public Indicators.BarCounter BarCounter(ISeries<double> input , bool showOddNumbers, Brush textColor, int textFontSize, int pixelsAboveBelow, bool textIsBelowBars)
		{
			return indicator.BarCounter(input, showOddNumbers, textColor, textFontSize, pixelsAboveBelow, textIsBelowBars);
		}
	}
}

namespace NinjaTrader.NinjaScript.Strategies
{
	public partial class Strategy : NinjaTrader.Gui.NinjaScript.StrategyRenderBase
	{
		public Indicators.BarCounter BarCounter(bool showOddNumbers, Brush textColor, int textFontSize, int pixelsAboveBelow, bool textIsBelowBars)
		{
			return indicator.BarCounter(Input, showOddNumbers, textColor, textFontSize, pixelsAboveBelow, textIsBelowBars);
		}

		public Indicators.BarCounter BarCounter(ISeries<double> input , bool showOddNumbers, Brush textColor, int textFontSize, int pixelsAboveBelow, bool textIsBelowBars)
		{
			return indicator.BarCounter(input, showOddNumbers, textColor, textFontSize, pixelsAboveBelow, textIsBelowBars);
		}
	}
}

#endregion
