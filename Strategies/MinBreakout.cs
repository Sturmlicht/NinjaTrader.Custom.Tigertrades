#region Using declarations

using System;
using System.IO;
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
    public class MinBreakoutStrategy : Strategy
    {
        private EMA EMA1;
        private Indicators.BarCounter barCounter;
        private NinjaTrader.NinjaScript.Indicators.RSI rsi;
        private VOL vol;
        private ATR_Ticks atrAll;
        
        public double EntryPrice;
        public double StopPrice;
        public double TargetPrice;
        
        public double BreakEvenPrice;
        public double BreakEvenTriggerPrice;
        public double Risk;

        private bool TradingDay;
        private bool TradingTime;


        // Breakeven moved flag (prevents repeated updates)
        private bool BreakevenMoved = false;

        // Interval breakout fields
        private int _intervalStartBar = -1;
        private int _rangeEndBar = -1;
        private int _intervalEndBar = -1;
        private double _intervalRangeHigh = double.MinValue;
        private double _intervalRangeLow = double.MaxValue;
        private bool _rangeFinalized = false;
        private int _barsNeededForRange = 1;
        private int _barsInInterval = 1;

        // Add fields
        private const string SettingsSummaryTag = "SettingsSummaryFixed";
        private bool _settingsSummaryShown = false;

        private bool _TradedThisInterval = false;
        private bool _TradeExecuted = false;

        private NinjaTrader.Cbi.Order breakoutOrder = null;
        private int requestedQuantity = 0;
        private double OrderFilledPrice = 0.0;
        private bool OrderChanged = false;
        


        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                                 = @"Interval breakout strategy with optional filters.";
                Name                                        = "MinBreakout";
                Calculate                                   = Calculate.OnEachTick;
                EntriesPerDirection                         = 1;
                EntryHandling                               = EntryHandling.AllEntries;
                IsExitOnSessionCloseStrategy                = true;
                ExitOnSessionCloseSeconds                   = 30;
                IsFillLimitOnTouch                          = false;
                MaximumBarsLookBack                         = MaximumBarsLookBack.TwoHundredFiftySix;
                OrderFillResolution                         = OrderFillResolution.Standard;
                Slippage                                    = 0;
                StartBehavior                               = StartBehavior.WaitUntilFlat;
                TimeInForce                                 = TimeInForce.Gtc;
                TraceOrders                                 = false;
                RealtimeErrorHandling                       = RealtimeErrorHandling.StopCancelClose;
                StopTargetHandling                          = StopTargetHandling.PerEntryExecution;
                BarsRequiredToTrade                         = 20;
                IsInstantiatedOnEachOptimizationIteration   = true;
                
            }
            else if (State == State.Configure)
            {
                AddDataSeries(Data.BarsPeriodType.Minute, 1); // used for BarCounter and interval timing
                AddDataSeries(Data.BarsPeriodType.Tick, 1); //fallback for backtesting and live trading failes to move SL or get out of a trade
            }
            else if (State == State.DataLoaded)
            {
                //this is needed to make sure only a certain timeframe is used for the strategy. The strategy will not run if the chart is in a different timeframe.
                barCounter = BarCounter(Closes[1], true, Brushes.Gray, 14, 50, true);

                // Setion for the Filters

                EMA1 = EMA(Close, EMAPeriod);
                if (UseEMAFilter && AddFiltersToChart)
                    AddChartIndicator(EMA1);


                rsi = RSI(Close, RSIPeriod, 1);
                if (UseRSIFilter && AddFiltersToChart)
                    AddChartIndicator(rsi);

                vol = VOL();
                if (UseVolumeFilter && AddFiltersToChart)
                    AddChartIndicator(vol);

                atrAll = ATR_Ticks(ATRPeriod);

                if (UseATRFilter && AddFiltersToChart)
                {

                    AddChartIndicator(atrAll);

                }

            }
        }
        /// OnBarUpdate is called on each bar update event (incoming tick) - searching for the strategy requirements and entering the inital trade
        protected override void OnBarUpdate()
        {
           

            if (BarsInProgress == 2) // tick series for backtesting and live trading, used to move SL and exit trades
                                     // -> it does increase calculation time in the analyzis, be aware to choose smaller chunks to test on!
            {
                if (CurrentBars[2] < 5) 
                    return;
                
                if (OrderChanged && !UseLimitOrder) //in case we use a market order we need to check for slippage to update breakeven accordingly
                {
                    OrderChanged = false;
                    
                    
                        if (Position.MarketPosition == MarketPosition.Long && UseBreakeven)
                        {
                            OrderFilledPrice = Position.AveragePrice;
                            //Lets not adjust the Trigger, only the BreakEven Price!
                            //BreakEvenTriggerPrice = (int)Math.Floor(OrderFilledPrice + ((OrderFilledPrice - StopPrice) * BreakevenTriggerMultiplier));
                            BreakEvenPrice = (int)Math.Floor(OrderFilledPrice + ((OrderFilledPrice - StopPrice) * BreakevenMultiplier));
                            LetsDraw.TextLine(this, $"IntervalBreakout_Breakeven_{CurrentBar}_adjusted", false, 0, BreakEvenPrice, -LineExtension, BreakEvenPrice, BreakEVENColor, DashStyleHelper.Dash, 2, false, $"Break Even {BreakEvenPrice} - adjusted from Fill Price");

                        }
                        else if (Position.MarketPosition == MarketPosition.Short && UseBreakeven)
                        {
                            OrderFilledPrice = Position.AveragePrice;
                            //Lets not adjust the Trigger, only the BreakEven Price!
                            //BreakEvenTriggerPrice = (int)Math.Floor(OrderFilledPrice + (( StopPrice - OrderFilledPrice) * BreakevenTriggerMultiplier)); 
                            BreakEvenPrice = (int)Math.Floor(OrderFilledPrice + ((StopPrice - OrderFilledPrice) * BreakevenMultiplier));

                            LetsDraw.TextLine(this, $"IntervalBreakout_Breakeven_{CurrentBar}_adjusted", false, 0, BreakEvenPrice, -LineExtension, BreakEvenPrice, BreakEVENColor, DashStyleHelper.Dash, 2, false, $"Break Even {BreakEvenPrice} - adjusted from Fill Price");
                        }

                }

                //handeling going breakeven on Ticks-Chart

              
                    if (Position.MarketPosition != MarketPosition.Flat)
                    {
                        // Aktuellen Preis des Ticks holen
                        double currentPrice = Close[0];

                        // Management für den laufenden Long-Trade
                        if (Position.MarketPosition == MarketPosition.Long)
                        {
                            // Sobald der TICK-Preis den Trigger erreicht und noch nicht verschoben wurde
                            if (currentPrice >= BreakEvenTriggerPrice && !BreakevenMoved && UseBreakeven)
                            {

                                // StopLoss sofort im Live-Markt modifizieren
                                SetStopLoss("IntervalBreakout_Long", CalculationMode.Price, BreakEvenPrice, false);

                                BreakevenMoved = true;
                                Log($"TICK-EXECUTION: Stop auf Breakeven ({BreakEvenPrice}) nachgezogen bei Preis: {currentPrice}", LogLevel.Information);
                            }
                        }

                        // Management für den laufenden Long-Trade
                        if (Position.MarketPosition == MarketPosition.Short)
                        {
                            // Sobald der TICK-Preis den Trigger erreicht und noch nicht verschoben wurde
                            if (currentPrice <= BreakEvenTriggerPrice && !BreakevenMoved && UseBreakeven)
                            {

                                // StopLoss sofort im Live-Markt modifizieren
                                SetStopLoss("IntervalBreakout_Long", CalculationMode.Price, BreakEvenPrice, false);

                                BreakevenMoved = true;
                                Log($"TICK-EXECUTION: Stop auf Breakeven ({BreakEvenPrice}) nachgezogen bei Preis: {currentPrice}", LogLevel.Information);
                            }
                        }

                    }

            }

            if (BarsInProgress == 1) // 1-minute series for interval timing
            {
                if (CurrentBars[1] < 5) 
                    return;

               

                // Check for the Tradging Day
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

                //check every minute if it is still within the trading time requirement
                // we won't check for the setup outside the trading time, but will handle open trades!
                TradingTime =
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

                //we want te exit trades outside of the trading time
                if (CloseTradesOutsiedTradingTime && Position.MarketPosition != MarketPosition.Flat && !TradingTime && CloseTradesOutsideTradingTime)
                {
                    ExitLong("ExitLong_OutsideTradingTime", "IntervalBreakout_Long");
                    ExitShort("ExitShort_OutsideTradingTime", "IntervalBreakout_Short");

                    //we check all pending orders and remove them
                    for (int i = Orders.Count - 1; i >= 0; i--)
                    {
                        NinjaTrader.Cbi.Order order = Orders[i];

                        // 1. BASIS-CHECK: Existiert die Order und wartet sie aktiv im Orderbuch (schwebend)?
                        if (order != null && (order.OrderState == OrderState.Working || order.OrderState == OrderState.Accepted || order.OrderState == OrderState.Initialized || order.OrderState == OrderState.Unknown))
                        {
                            // should only be for this instrument
                            bool isCorrectInstrument = (order.Instrument == Instrument);

                            // should only be for our breakout entries
                            // - be aware, having the same strategy running on the same instrument just higher timeframe is not intended and will lead to conflicts!                           
                            bool isOurBreakoutEntry = (order.Name == "IntervalBreakout_Long" || order.Name == "IntervalBreakout_Short");

                            //the actual cancelling if they meet the requirments
                            if (isCorrectInstrument && isOurBreakoutEntry)
                            {
                                Log(string.Format($"STRATEGIE-CLEANUP: Lösche schwebende Einstiegs-Order '{0}' für {1}.",
                                    order.Name, order.Instrument.FullName), LogLevel.Information);
                                CancelOrder(order);
                            }
                        }
                    }
                }

                //getting the bars for the range
                if (TradingTime)
                {
                    // use the 1-minute series minutes and treat the first 1-min bar of the day as interval start
                    int minutesOfDay = Times[1][0].Hour * 60 + Times[1][0].Minute;
                    bool isFirstBarOfDay1 = CurrentBars[1] == 0 || Times[1][0].Date != Times[1][1].Date;
                    bool isIntervalStart = isFirstBarOfDay1 || (minutesOfDay % IntervalMinutes) == 0;



                    // New interval detected
                    if (isIntervalStart && _intervalStartBar != CurrentBars[1])
                    {
                        _intervalStartBar = CurrentBars[1];
                        _rangeEndBar = _intervalStartBar + (FirstMinutesToDefineRange - 1);
                        _intervalEndBar = _intervalStartBar + (IntervalMinutes - 1);
                        _intervalRangeHigh = Highs[1][0];
                        _intervalRangeLow = Lows[1][0];
                        _rangeFinalized = false;
                        _TradedThisInterval = false;
                    }

                    // Collect range bars (first N bars of interval)
                    if (_intervalStartBar != -1 && CurrentBars[1] >= _intervalStartBar && CurrentBars[1] <= _rangeEndBar)
                    {
                        _intervalRangeHigh = Math.Max(_intervalRangeHigh, Highs[1][0]);
                        _intervalRangeLow = Math.Min(_intervalRangeLow, Lows[1][0]);
                    }

                    // Finalize the initial range once we've processed the last defining bar
                    if (_intervalStartBar != -1 && !_rangeFinalized && CurrentBars[1] >= _rangeEndBar
                        )
                    {

                        // calculate and show range size (ticks and money) above the candle in grey
                        double rangePrice = _intervalRangeHigh - _intervalRangeLow;
                        int rangeTicks = (int)Math.Round(rangePrice / TickSize);
                        double rangeMoney = Math.Round(rangePrice * Instrument.MasterInstrument.PointValue, 2);
                        double textPrice = _intervalRangeHigh + (2 * TickSize);

                        string ATRText;
                        //let's check the ATR Filter 
                        if (atrAll[0] < rangeTicks*4)
                            ATRText = "Range is bigger than average ATR";
                        else if (atrAll[0] > rangeTicks*4)
                            ATRText = "Range is smaller than average ATR";
                        else
                            ATRText = "Range is equal to average ATR";

                        if ((UseATRFilter && (atrAll[0] >= rangeTicks)) || !UseATRFilter)
                            _rangeFinalized = true;

                        PrintRangeLines();

                        //int StartLinetoBars = 1;
                        //int ExtendLinetoBars = 60;
                        //if (BarsPeriod.BarsPeriodType == BarsPeriodType.Minute && BarsPeriod.Value > 0)
                        //{
                        //    StartLinetoBars = Math.Max(1, (int)Math.Ceiling((double)FirstMinutesToDefineRange / BarsPeriod.Value));
                        //    ExtendLinetoBars = Math.Max(1, (int)Math.Ceiling((double)(IntervalMinutes - FirstMinutesToDefineRange) / BarsPeriod.Value));
                        //}
                        //else
                        //{
                        //    StartLinetoBars = Math.Max(1, (int)Math.Ceiling((double)FirstMinutesToDefineRange));
                        //    ExtendLinetoBars = Math.Max(1, (int)Math.Ceiling((double)IntervalMinutes- FirstMinutesToDefineRange));
                        //}
                        //LetsDraw.TextLine(this, $"IntervalRange_{CurrentBar}", false, StartLinetoBars, _intervalRangeHigh, -ExtendLinetoBars, _intervalRangeHigh, Brushes.Gray, DashStyleHelper.Dash, 1, false, "Interval Range High" + $" \n Range: {rangeTicks} ticks / {rangeMoney} $; \n {ATRText}");
                        //LetsDraw.TextLine(this, $"IntervalRangeLow_{CurrentBar}", false, StartLinetoBars, _intervalRangeLow, -ExtendLinetoBars, _intervalRangeLow, Brushes.Gray, DashStyleHelper.Dash, 1, false, "Interval Range Low");

                        
                        
                       
                    }
                   
                }

                // Clean up at the end of interval
                if (_intervalStartBar != -1 && CurrentBar > _intervalEndBar)
                {
                    _intervalStartBar = -1;
                    _rangeEndBar = -1;
                    _intervalEndBar = -1;
                    _rangeFinalized = false;
                    _intervalRangeHigh = double.MinValue;
                    _intervalRangeLow = double.MaxValue;
                }

            }

            if (BarsInProgress == 0) // the timeframe you want to use the strategy on, usually the chart timeframe
            {
                if (
                  EMA1[0] == 0
               || CurrentBars[0] < Math.Max(EMAPeriod, 5)
               || CurrentBars[1] < Math.Max(IntervalMinutes, 5)
               || CurrentBars[0] < Math.Max(ATRPeriod, 5)
               )
                    return;

                #region Timecheck
                // Check for the Tradging Day
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

                //check every minute if it is still within the trading time requirement
                // we won't check for the setup outside the trading time, but will handle open trades!
                TradingTime =
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
                #endregion

                

              

                // Strategy needs to be executed on something higher than a Seconds Chart, otherwise it will not work properly. So we will check for that and terminate the strategy if it is a Seconds Chart.
                // Strategy can work on Ranged, Renko, Volume, Tick and timebased charts equal or above a 1 minute.
                if (BarsPeriod.BarsPeriodType == BarsPeriodType.Second && !_settingsSummaryShown)
                {
                    _settingsSummaryShown=true;
                    Draw.TextFixed(this, "ChartError", "MinBreakout does not support Seconds charts. Strategy will terminate.", TextPosition.Center, TextColor, new SimpleFont("Segoe UI", 12), Brushes.Red, Brushes.Transparent, 0);
                }

                else
                    ShowSettingsSummary();

                // --- Interval breakout logic ---
                if (TradingTime)
                {
                    //in case we entered a trade but not all contracts were filled, we want to cancel the rest of the order to avoid being stuck in a trade with a smaller position than intended - we check after the bar closed
                    if (Position.MarketPosition != MarketPosition.Flat  && breakoutOrder != null && (breakoutOrder.OrderState == OrderState.Working || breakoutOrder.OrderState == OrderState.Accepted))
                    {
                        Log(string.Format("ZEITABLAUF: Limit-Order wurde nicht vollständig gefüllt! Gefüllt: {0}/{1}. Storniere Rest...",
                            breakoutOrder.Filled, breakoutOrder.Quantity), LogLevel.Warning);

                        // Löscht den unbefüllten Teil der Order restlos an der Börse
                        CancelOrder(breakoutOrder);
                    }

                    

                    // Check for breakouts after the initial range within the same interval
                    if (_rangeFinalized )
                    {
                        //Print("--- Interval Breakout Logic ---");
                        // Long breakout
                        if (doLongs && Position.MarketPosition == MarketPosition.Flat && Close[0] > _intervalRangeHigh && (!OneTradePerInterval || !_TradedThisInterval))
                        {
                            bool emaOk = !UseEMAFilter || Close[0] > EMA1[0];
                            bool volOk = !UseVolumeFilter || Volume[0] > Volume[1];
                            bool rsiOkLong = !UseRSIFilter || rsi[0] < RSILongThreshold;
                            bool lowInsideRange = Low[0] < _intervalRangeHigh; //Low[0] > _intervalRangeLow && - taking it out


                            if (emaOk && volOk && rsiOkLong && lowInsideRange)
                            {
                                if (UseLimitOrder)
                                    EntryPrice = Close[0] - TicksEntryPriceAdjustment*TickSize;
                                else
                                    EntryPrice = Close[0];

                                StopPrice = (int)Math.Floor(_intervalRangeHigh - ((_intervalRangeHigh-_intervalRangeLow) * StopMultiplier));
                                TargetPrice = (int)Math.Floor(EntryPrice + ((EntryPrice - StopPrice) * LongMultiplier));
                                int EntryCandle = CurrentBar;
                                string ContractRiskInfo = "";
                                double riskPerContract = Math.Abs(EntryPrice - StopPrice) * Instrument.MasterInstrument.PointValue;


                                if (UseMoneyRiskLimit)
                                    orderQuantity = Math.Max(1, (int)Math.Floor(MoneyRiskPerTrade / riskPerContract));

                                ContractRiskInfo = $"{orderQuantity} Contracts for {riskPerContract} $ Risk per Contract";

                                // Bracket Orders initializing before entering the trade to avoid slippage and ensure proper risk management
                                SetStopLoss("IntervalBreakout_Long", CalculationMode.Price, StopPrice, false);
                                SetProfitTarget("IntervalBreakout_Long", CalculationMode.Price, TargetPrice);


                                // Enter the long position
                                EnterLong(orderQuantity, $"IntervalBreakout_Long");

                                LetsDraw.TextLine(this, $"IntervalBreakout_Long_{CurrentBar}", false, 0, EntryPrice, -LineExtension, EntryPrice, EntryLineColor, DashStyleHelper.Dash, 2, false, $"Entry {EntryPrice} - {ContractRiskInfo}");
                                LetsDraw.TextLine(this, $"IntervalBreakout_Target_{CurrentBar}", false, 0, TargetPrice, -LineExtension, TargetPrice, TargetLineColor, DashStyleHelper.Dash, 2, false, $"Target {TargetPrice}");
                                LetsDraw.TextLine(this, $"IntervalBreakout_Stop_{CurrentBar}", false, 0, StopPrice, -LineExtension, StopPrice, StopLineColor, DashStyleHelper.Dash, 2, false, $"Stop {StopPrice}");

                                if (UseBreakeven)
                                {
                                    //Breakeven calculations and drawing lines for breakeven trigger and breakeven price
                                    BreakEvenTriggerPrice =(int)Math.Floor(EntryPrice + ((EntryPrice - StopPrice) * BreakevenTriggerMultiplier));
                                    BreakEvenPrice = (int)Math.Floor(EntryPrice + ((EntryPrice - StopPrice) * BreakevenMultiplier));
                                    // reset moved flag for the new trade
                                    BreakevenMoved = false;
                                    LetsDraw.TextLine(this, $"IntervalBreakout_BreakevenTrigger_{CurrentBar}", false, 0, BreakEvenTriggerPrice, -LineExtension, BreakEvenTriggerPrice, BreakEVENColor, DashStyleHelper.Dash, 2, false, $"Break Even Trigger {BreakEvenTriggerPrice}");

                                    //SetStopLoss("Ichimoku_MIT_Long", CalculationMode.Price, StopPrice, false);
                                    LetsDraw.TextLine(this, $"IntervalBreakout_Breakeven_{CurrentBar}", false, 0, BreakEvenPrice, -LineExtension, BreakEvenPrice, BreakEVENColor, DashStyleHelper.Dash, 2, false, $"Break Even {BreakEvenPrice}");
                                    BreakevenMoved = false;
                                }

                                _TradedThisInterval = true;
                                _TradeExecuted = true;

                            }
                        }


                        // Short breakout
                        if (doShorts && Position.MarketPosition == MarketPosition.Flat && Close[0] < _intervalRangeLow && (!OneTradePerInterval || !_TradedThisInterval))
                        {
                            bool emaOk = !UseEMAFilter || Close[0] < EMA1[0];
                            bool volOk = !UseVolumeFilter || Volume[0] > Volume[1];
                            bool rsiOkShort = !UseRSIFilter || rsi[0] > RSIShortThreshold;
                            bool highInsideRange = High[0] > _intervalRangeLow; //  High[0] < _intervalRangeHigh && - taking it out


                            if (emaOk && volOk && rsiOkShort && highInsideRange)
                            {

                                if (UseLimitOrder)
                                    EntryPrice = Close[0] + TicksEntryPriceAdjustment*TickSize;
                                else
                                    EntryPrice = Close[0];

                                string ContractRiskInfo = "";


                                StopPrice = _intervalRangeHigh;
                                TargetPrice = (int)Math.Floor(EntryPrice - ((StopPrice - EntryPrice)  * ShortMultiplier));
                                double riskPerContract = Math.Abs(StopPrice - EntryPrice) * Instrument.MasterInstrument.PointValue;

                                if (UseMoneyRiskLimit)
                                    orderQuantity = Math.Max(1, (int)Math.Floor(MoneyRiskPerTrade / riskPerContract));

                                ContractRiskInfo = $"{orderQuantity} Contracts for {riskPerContract}$ Risk per Contract";

                                SetStopLoss($"IntervalBreakout_Short", CalculationMode.Price, StopPrice, false);
                                SetProfitTarget($"IntervalBreakout_Short", CalculationMode.Price, TargetPrice);

                                if (UseLimitOrder)
                                    EnterShortLimit(orderQuantity, EntryPrice, $"IntervalBreakout_Short");
                                else
                                    EnterShort(orderQuantity, $"IntervalBreakout_Short");


                                LetsDraw.TextLine(this, $"IntervalBreakout_Short_{CurrentBar}", false, 0, EntryPrice, -LineExtension, EntryPrice, EntryLineColor, DashStyleHelper.Dash, 2, false, $"Entry {EntryPrice} - {ContractRiskInfo}");
                                LetsDraw.TextLine(this, $"IntervalBreakout_Target_{CurrentBar}", false, 0, TargetPrice, -LineExtension, TargetPrice, TargetLineColor, DashStyleHelper.Dash, 2, false, $"Target {TargetPrice}");
                                LetsDraw.TextLine(this, $"IntervalBreakout_Stop_{CurrentBar}", false, 0, StopPrice, -LineExtension, StopPrice, StopLineColor, DashStyleHelper.Dash, 2, false, $"Stop {StopPrice}");


                                if (UseBreakeven)
                                {
                                    BreakEvenTriggerPrice = (int)Math.Floor(EntryPrice - ((StopPrice - EntryPrice) * BreakevenTriggerMultiplier));
                                    BreakEvenPrice = (int)Math.Floor(EntryPrice - ((StopPrice - EntryPrice) * BreakevenMultiplier));
                                    LetsDraw.TextLine(this, $"IntervalBreakout_BreakevenTrigger_{CurrentBar}", false, 0, BreakEvenTriggerPrice, -LineExtension, BreakEvenTriggerPrice, BreakEVENColor, DashStyleHelper.Dash, 2, false, $"Break Even Trigger {BreakEvenTriggerPrice}");
                                    LetsDraw.TextLine(this, $"IntervalBreakout_Breakeven_{CurrentBar}", false, 0, BreakEvenPrice, -LineExtension, BreakEvenPrice, BreakEVENColor, DashStyleHelper.Dash, 2, false, $"Break Even {BreakEvenPrice}");
                                    BreakevenMoved = false;
                                }
                                _TradedThisInterval = true;
                            }
                        }

                    }



                    if (Position.MarketPosition == MarketPosition.Flat)
                    {
                        //just in case resetting
                        BreakevenMoved = false;

                    }

                    Draw.TextFixed(this, "Status", $"Breakeven Moved: {BreakevenMoved} \n Trading Time: {TradingTime}", TextPosition.TopRight, TextColor, new SimpleFont("Segoe UI", 12), TextColor, Brushes.Transparent, 0);



                }

            }


           





        }

        protected override void OnOrderUpdate(NinjaTrader.Cbi.Order order, double limitPrice, double stopPrice, int quantity, int filled, double averageFillPrice, OrderState orderState, DateTime time, ErrorCode error, string comment)
        {
            OrderFilledPrice = averageFillPrice;
            OrderChanged = true;

            // Wir prüfen, ob es sich um unsere Breakout-Order handelt
            if (breakoutOrder != null && order == breakoutOrder)
            {
                // Loggen Sie den aktuellen Status für Transparenz im Kontrollzentrum
                if (orderState == OrderState.Filled)
                {
                    Print($"VOLLSTÄNDIG GEFÜLLT: Alle {0} Kontrakte wurden zu {1} ausgeführt.");
                    breakoutOrder = null; // Reset, da die Order abgeschlossen ist
                }
                else if (orderState == OrderState.PartFilled)
                {
                    Print($"TEILAUSFÜHRUNG: Bisher {0} von {1} Kontrakten gefüllt.");
                }
                else if (orderState == OrderState.Cancelled)
                {
                    Print($" STORNO ERFOLGREICH: Die verbleibenden schwebenden Kontrakte wurden gelöscht.");
                    breakoutOrder = null; // Reset nach Stornierung
                }
            }
        }

        // On MarketData to move the SL to Breakeven once a trade was entered
        //protected override void OnMarketData(MarketDataEventArgs marketDataUpdate)
        //{
        //    // This method is called for each market data update (bid, ask, last price, etc.)
        //    if (marketDataUpdate.MarketDataType == MarketDataType.Last && Position.MarketPosition != MarketPosition.Flat)
        //    {
        //        // Aktuellen Preis des Ticks holen
        //        double currentPrice = marketDataUpdate.Price;

        //        // Management für den laufenden Long-Trade
        //        if (Position.MarketPosition == MarketPosition.Long)
        //        {
        //            // Sobald der TICK-Preis den Trigger erreicht und noch nicht verschoben wurde
        //            if (currentPrice >= BreakEvenTriggerPrice && !BreakevenMoved && UseBreakeven)
        //            {
                      
        //                // StopLoss sofort im Live-Markt modifizieren
        //                SetStopLoss("IntervalBreakout_Long", CalculationMode.Price, BreakEvenPrice, false);

        //                BreakevenMoved = true;
        //                Log($"TICK-EXECUTION: Stop auf Breakeven ({BreakEvenPrice}) nachgezogen bei Preis: {currentPrice}", LogLevel.Information);
        //            }
        //        }

        //        // Management für den laufenden Long-Trade
        //        if (Position.MarketPosition == MarketPosition.Short)
        //        {
        //            // Sobald der TICK-Preis den Trigger erreicht und noch nicht verschoben wurde
        //            if (currentPrice <= BreakEvenTriggerPrice && !BreakevenMoved && UseBreakeven)
        //            {

        //                // StopLoss sofort im Live-Markt modifizieren
        //                SetStopLoss("IntervalBreakout_Long", CalculationMode.Price, BreakEvenPrice, false);

        //                BreakevenMoved = true;
        //                Log($"TICK-EXECUTION: Stop auf Breakeven ({BreakEvenPrice}) nachgezogen bei Preis: {currentPrice}", LogLevel.Information);
        //            }
        //        }

        //    }

        //}


        #region Custom Helpers

        //Printing the Range Lines
        private void PrintRangeLines()
        {
            if (_intervalStartBar != -1 && _rangeFinalized)
            {
                BarsPeriodType mainChartPeriodType = BarsArray[0].BarsPeriod.BarsPeriodType;
                int mainChartValue = BarsArray[0].BarsPeriod.Value;

                double rangePrice = _intervalRangeHigh - _intervalRangeLow;
                int rangeTicks = (int)Math.Round(rangePrice / TickSize);
                double rangeMoney = Math.Round(rangePrice * Instrument.MasterInstrument.PointValue, 2);
                double textPrice = _intervalRangeHigh + (2 * TickSize);
                int StartLinetoBars = 1;
                int ExtendLinetoBars = 60;
                if (mainChartPeriodType == BarsPeriodType.Minute && mainChartValue > 0)
                {
                    StartLinetoBars = Math.Max(1, (int)Math.Ceiling((double)FirstMinutesToDefineRange / mainChartValue));
                    ExtendLinetoBars = Math.Max(1, (int)Math.Ceiling((double)(IntervalMinutes - FirstMinutesToDefineRange) / mainChartValue));
                }
                else
                {
                    StartLinetoBars = Math.Max(1, (int)Math.Ceiling((double)FirstMinutesToDefineRange));
                    ExtendLinetoBars = Math.Max(1, (int)Math.Ceiling((double)IntervalMinutes - FirstMinutesToDefineRange));
                }
                LetsDraw.TextLine(this, $"IntervalRange_{CurrentBar}", false, StartLinetoBars, _intervalRangeHigh, -ExtendLinetoBars, _intervalRangeHigh, Brushes.Gray, DashStyleHelper.Dash, 1, false, "Interval Range High" + $" \n Range: {rangeTicks} ticks / {rangeMoney} $");
                LetsDraw.TextLine(this, $"IntervalRangeLow_{CurrentBar}", false, StartLinetoBars, _intervalRangeLow, -ExtendLinetoBars, _intervalRangeLow, Brushes.Gray, DashStyleHelper.Dash, 1, false, "Interval Range Low");
            }
        }


        // A Helper to have nice text formating for the time we trade
        private string FormatHHMM(int hhmm, bool useAmPm = true)
        {
            int h = Math.Max(0, Math.Min(23, hhmm / 100));
            int m = Math.Max(0, Math.Min(59, hhmm % 100));
            var dt = DateTime.Today.AddHours(h).AddMinutes(m);
            return useAmPm ? dt.ToString("hh:mm tt") : dt.ToString("HH:mm");
        }


        // The Helper to build the text for the summary
        private string BuildSettingsSummary()
        {
            // trading days (only show enabled ones)
            var days = new List<string>();
            if (Monday) days.Add("Mon");
            if (Tuesday) days.Add("Tue");
            if (Wednesday) days.Add("Wed");
            if (Thursday) days.Add("Thu");
            if (Friday) days.Add("Fri");
            if (Saturday) days.Add("Sat");
            if (Sunday) days.Add("Sun");
            string daysStr = days.Count > 0 ? string.Join(", ", days) : "None";

            // active time windows (local HH:mm from HHMM ints)
            var sessions = new List<string>();
            if (US) sessions.Add($"US {FormatHHMM(USBarStart)}-{FormatHHMM(USBarEnd)}");
            if (Asia) sessions.Add($"Asia {FormatHHMM(AsiaBarStart)}-{FormatHHMM(AsiaBarEnd)}");
            if (London) sessions.Add($"London {FormatHHMM(LondonBarStart)}-{FormatHHMM(LondonBarEnd)}");
            if (Frankfurt) sessions.Add($"Frankfurt {FormatHHMM(FrankfurtBarStart)}-{FormatHHMM(FrankfurtBarEnd)}");
            if (Custom) sessions.Add($"Custom {FormatHHMM(CustomBarStart)}-{FormatHHMM(CustomBarEnd)}");
            string timesStr = sessions.Count > 0 ? string.Join("; ", sessions) : "None";


            return
                $"Interval: {IntervalMinutes}m (Range {FirstMinutesToDefineRange}m)" +
                $" \n Filters: EMA={UseEMAFilter}({EMAPeriod}), Vol={UseVolumeFilter}, RSI={UseRSIFilter}({RSIPeriod}), ATR={UseATRFilter}({ATRPeriod})" +
                $" \n Risk: Qty={orderQuantity}, UseMoneyLimit={UseMoneyRiskLimit}, MoneyRisk={MoneyRiskPerTrade:C}" +
                $" \n Multipliers: Longs={LongMultiplier}, Shorts={ShortMultiplier}, BETrigger={BreakevenTriggerMultiplier}, BE={BreakevenMultiplier}" +
                $" \n Sides: Longs={doLongs}, Shorts={doShorts}" +
                $" \n Days: {daysStr}{Environment.NewLine}" +
                $" \n ActiveTimes: {timesStr}{Environment.NewLine}";
        }

        // the function to actually print the summary to the screen
        private void ShowSettingsSummary()
        {
            string s = BuildSettingsSummary();
            Draw.TextFixed(this, SettingsSummaryTag, s, TextPosition.BottomLeft, TextColor, new SimpleFont("Segoe UI", 12), Brushes.DimGray, Brushes.Transparent, 0);
        }

        #endregion

        #region Properties   

        // --- Interval / Breakout settings ---
        [NinjaScriptProperty]
        [Range(1, 240)]
        [Display(Name = "Interval Minutes", Description = "Check every X minutes (e.g. 30, 60, 90).", Order = 2, GroupName = "Interval Breakout")]
        public int IntervalMinutes { get; set; } = 60;

        [NinjaScriptProperty]
        [Range(1, 60)]
        [Display(Name = "First Minutes To Define Range", Description = "Number of minutes from interval start used to define the initial range.", Order = 3, GroupName = "Interval Breakout")]
        public int FirstMinutesToDefineRange { get; set; } = 5;

        [NinjaScriptProperty]
        [Display(Name = "Limits Trades to One per Interval Minutes", Description = "If the setup appears a second time and a trade was already executed and closed, another trade will not exectue if this option is true.", Order = 2, GroupName = "Interval Breakout")]
        public bool OneTradePerInterval { get; set; } = true;


        // --- Filters / Trigger settings ---


        [NinjaScriptProperty]
        [Display(Name = "Use EMA Filter", Description = "Require price relation to EMA for breakout (price > EMA for long, price < EMA for short).", Order = 3, GroupName = "Filters")]
        public bool UseEMAFilter { get; set; } = true;

        [Range(1, int.MaxValue), NinjaScriptProperty]
        [Display(Name = "EMA Period", Description = "Set the Period for the EMA", Order = 4, GroupName = "Filters")]
        public int EMAPeriod { get; set; } = 9;

        [NinjaScriptProperty]
        [Display(Name = "Use Volume Filter", Description = "Require breakout candle volume > previous candle volume.", Order = 5, GroupName = "Filters")]
        public bool UseVolumeFilter { get; set; } = false;

        [NinjaScriptProperty]
        [Display(Name = "Use RSI Filter", Description = "Enable RSI filter for breakouts", Order = 6, GroupName = "Filters")]
        public bool UseRSIFilter { get; set; } = false;

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "RSI Period", Description = "Period for RSI filter", Order = 7, GroupName = "Filters")]
        public int RSIPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "RSI Long Threshold", Description = "Require RSI < this value for longs", Order = 8, GroupName = "Filters")]
        public double RSILongThreshold { get; set; } = 85;

        [NinjaScriptProperty]
        [Range(0.0, 100.0)]
        [Display(Name = "RSI Short Threshold", Description = "Require RSI > this value for shorts", Order = 9, GroupName = "Filters")]
        public double RSIShortThreshold { get; set; } = 15;

        [NinjaScriptProperty]
        [Display(Name = "Use ATR Filter", Order = 10, GroupName = "Filters")]
        public bool UseATRFilter { get; set; } = false;

        [NinjaScriptProperty]
        [Range(1, 200)]
        [Display(Name = "ATR Period", Order = 11, GroupName = "Filters")]
        public int ATRPeriod { get; set; } = 14;

        [NinjaScriptProperty]
        [Display(Name = "Add Filters To Chart", Order = 12, GroupName = "Filters")]
        public bool AddFiltersToChart { get; set; } = false;

        // --- Risk / Position sizing ---
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Default Quantity", Description = "Default number of contracts", Order = 1, GroupName = "Risk")]
        public int orderQuantity { get; set; } = 1;

        [NinjaScriptProperty]
        [Display(Name = "Use Money Risk Limit", Order = 2, GroupName = "Risk")]
        public bool UseMoneyRiskLimit { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0.01, double.MaxValue)]
        [Display(Name = "Money Risk Per Trade", Description = "Max money to risk per trade (currency)", Order = 3, GroupName = "Risk")]
        public double MoneyRiskPerTrade { get; set; } = 300.0;

        [NinjaScriptProperty]
        [Display(Name = "Use Breakeven", Order = 4, GroupName = "Risk")]
        public bool UseBreakeven { get; set; } = true;

        [NinjaScriptProperty]
        [Display(Name = "Close trades outside Trading time", Description = "If true, closes open trades when we are outside of the trading time", Order = 4, GroupName = "Risk")]
        public bool CloseTradesOutsiedTradingTime { get; set; } = false;


        // --- Entry / signal control ---
        [NinjaScriptProperty]
        [Display(Name = "Trade Longs?", Order = 2, GroupName = "Entry Control")]
        public bool doLongs { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Trade Shorts?", Order = 2, GroupName = "Entry Control")]
        public bool doShorts { get; set; } = true;

       

        [NinjaScriptProperty]
        [Display(Name = "Use Limit instead of Market Orders", Description = "This will avoid slippage from market orders but you might not be entered with all contracts or have an order not execute at all",Order = 2, GroupName = "Entry Control")]
        public bool UseLimitOrder { get; set; } = true;

        [NinjaScriptProperty]
        [Range(0, int.MaxValue)]
        [Display(Name = "Ticks to Adjust the Entry Price", Description = "For a Limit Order we can adjust the entry price and avoid slippage - 1 Tick is 0.25$ on MNQ - 4 Ticks are 1 Point", Order = 2, GroupName = "Multipliers")]
        public int TicksEntryPriceAdjustment { get; set; } = 4;

        [NinjaScriptProperty]
        [Display(Name = "Close Trades Outside Trading Time?", Description = "Will close trades that are 1 min outside the defined trading time ", Order = 2, GroupName = "Entry Control")]
        public bool CloseTradesOutsideTradingTime{ get; set; } = true;


        // --- Multipliers / Targets / Stops ---
        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "R/R Multiplyer for Target Longs", Description = "Will set the Target accordingly.", Order = 1, GroupName = "Multipliers")]
        public double LongMultiplier { get; set; } = 1.3;

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "R/R Multiplyer for Target Shorts", Description = "Will set the Target accordingly.", Order = 2, GroupName = "Multipliers")]
        public double ShortMultiplier { get; set; } = 1.3;

        [NinjaScriptProperty]
        [Range(0.1, double.MaxValue)]
        [Display(Name = "Multiplier to adjust Risk", Description = "Will enlarge or reduce Risk based on the Range.", Order = 3, GroupName = "Multipliers")]
        public double StopMultiplier { get; set; } = 1;

        [NinjaScriptProperty]
        [Range(0.2, int.MaxValue)]
        [Display(Name = "Breakeven Trigger Multiplier", Description = "Add Reward multiplier to move the Stop Loss", Order = 2, GroupName = "Multipliers")]
        public double BreakevenTriggerMultiplier { get; set; } = 0.66;

        [NinjaScriptProperty]
        [Range(0.1, int.MaxValue)]
        [Display(Name = "Breakeven Multiplier", Description = "Add Reward Multiplier to move the Stop Loss to this value", Order = 3, GroupName = "Multipliers")]
        public double BreakevenMultiplier { get; set; } = 0.16;

        // --- Trading times / sessions (already grouped) ---
        [NinjaScriptProperty]
        [Display(Name = "US", Description = "Check if you want to check/trade this Session", Order = 1, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool US { get; set; }

        [NinjaScriptProperty]
        [Display(Name = "US Bar Start", Description = "Start Bar for US Session", Order = 2, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int USBarStart { get; set; } = 936;
        [NinjaScriptProperty]
        [Display(Name = "US Bar End", Description = "End Bar for US Session", Order = 3, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int USBarEnd { get; set; } = 1600;

        [NinjaScriptProperty]
        [Display(Name = "Asia", Description = "Check if you want to check/trade this Session", Order = 4, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool Asia { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Asia Bar Start", Description = "Start Bar for Asia Session", Order = 5, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int AsiaBarStart { get; set; } = 0;
        [NinjaScriptProperty]
        [Display(Name = "Asia Bar End", Description = "End Bar for Asia Session", Order = 6, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int AsiaBarEnd { get; set; } = 300;

        [NinjaScriptProperty]
        [Display(Name = "London", Description = "Check if you want to check/trade this Session", Order = 7, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool London { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "London Bar Start", Description = "Start Bar for London Session", Order = 8, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int LondonBarStart { get; set; } = 480;
        [NinjaScriptProperty]
        [Display(Name = "London Bar End", Description = "End Bar for London Session", Order = 9, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int LondonBarEnd { get; set; } = 960;

        [NinjaScriptProperty]
        [Display(Name = "Frankfurt", Description = "Check if you want to check/trade this Session", Order = 10, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool Frankfurt { get; set; }
        [NinjaScriptProperty]
        [Display(Name = "Frankfurt Bar Start", Description = "Start Bar for Frankfurt Session", Order = 11, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int FrankfurtBarStart { get; set; } = 540;
        [NinjaScriptProperty]
        [Display(Name = "Frankfurt Bar End", Description = "End Bar for Frankfurt Session", Order = 12, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int FrankfurtBarEnd { get; set; } = 1020;

        [NinjaScriptProperty]
        [Display(Name = "Custom", Description = "Check if you want to check/trade this Session", Order = 13, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public bool Custom { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Custom Bar Start", Description = "Start Bar for Custom Session", Order = 14, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int CustomBarStart { get; set; } = 1;
        [NinjaScriptProperty]
        [Display(Name = "Custom Bar End", Description = "End Bar for Custom Session", Order = 15, GroupName = "Trading Times - Uses 1 Minute Chart Bars! Use Bar Counter Indicator")]
        public int CustomBarEnd { get; set; } = 1380;

        [NinjaScriptProperty]
        [Display(Name = "Monday", Description = "On these days Trades are allowed", Order = 20, GroupName = "Trading Days")]
        public bool Monday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Tuesday", Description = "On these days Trades are allowed", Order = 21, GroupName = "Trading Days")]
        public bool Tuesday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Wednesday", Description = "On these days Trades are allowed", Order = 22, GroupName = "Trading Days")]
        public bool Wednesday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Thursday", Description = "On these days Trades are allowed", Order = 23, GroupName = "Trading Days")]
        public bool Thursday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Friday", Description = "On these days Trades are allowed", Order = 24, GroupName = "Trading Days")]
        public bool Friday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Saturday", Description = "On these days Trades are allowed", Order = 25, GroupName = "Trading Days")]
        public bool Saturday { get; set; } = true;
        [NinjaScriptProperty]
        [Display(Name = "Sunday", Description = "On these days Trades are allowed", Order = 26, GroupName = "Trading Days")]
        public bool Sunday { get; set; } = true;

        // --- Visuals ---
        [NinjaScriptProperty]
        [Range(1, int.MaxValue)]
        [Display(Name = "Line Extension", Description = "How Many Bars should the Order Managing Lines extend?", Order = 1, GroupName = "Visuals")]
        public int LineExtension { get; set; } = 5;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Entry Line Color", Description = "Select the color for Entry Line", Order = 2, GroupName = "Visuals")]
        public Brush EntryLineColor { get; set; } = Brushes.Yellow;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Target Line Color", Description = "Select the color for Target Line", Order = 3, GroupName = "Visuals")]
        public Brush TargetLineColor { get; set; } = Brushes.Green;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Stop Line Color", Description = "Select the color for Stop Line", Order = 4, GroupName = "Visuals")]
        public Brush StopLineColor { get; set; } = Brushes.Red;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Range Markout Color", Description = "Select the color for marking the Range", Order = 5, GroupName = "Visuals")]
        public Brush RangeColor { get; set; } = Brushes.Gray;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Text Color", Description = "Select the color for Texts", Order = 6, GroupName = "Visuals")]
        public Brush TextColor { get; set; } = Brushes.CornflowerBlue;

        [NinjaScriptProperty]
        [TypeConverter(typeof(BrushConverter))]
        [Display(Name = "Break Even Color", Description = "Select the color For Showing Break Even Trigger and Price Line", Order = 7, GroupName = "Visuals")]
        public Brush BreakEVENColor { get; set; } = Brushes.White;
        #endregion

    }

 
}

