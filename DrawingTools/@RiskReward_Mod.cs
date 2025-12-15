// 
// Updated 15.Dec.2025 - now target and stop line can be moved freely, Entry Line will calculate the Risk/Reward Ratio and will show the entry price, Target and Stop Line will show the currency, price and ticks. Enjoy!
//
#region Using declarations
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using NinjaTrader.Cbi;
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
#endregion

//This namespace holds Drawing tools in this folder and is required. Do not change it. 
namespace NinjaTrader.NinjaScript.DrawingTools
{
    /// <summary>
    /// Represents an interface that exposes information regarding a Risk Reward IDrawingTool.
    /// </summary>
    public class RiskReward_Mod : DrawingTool
    {
        private const int cursorSensitivity = 15;
        private ChartAnchor editingAnchor;
        private double entryPrice;
        private bool needsRatioUpdate = true;
        private double ratio = 2;
        private double risk;
        private double reward;
        private double stopPrice;
        private double targetPrice;
        private double textleftPoint;
        private double textRightPoint;

        // New: track which anchor was last edited to avoid automatic recalculation when Stop/Target were moved
        private enum EditOrigin { None, Entry, Stop, Target }
        private EditOrigin lastEdit = EditOrigin.None;

        // New: once the user interacts with the tool (edits any anchor after initial creation),
        // we consider the tool "user edited" and will not auto-recalculate Stop/Target from Entry+Ratio.
        private bool hasUserEdited = false;

        [Browsable(false)]
        private bool DrawTarget => RiskAnchor is { IsEditing: false } || RewardAnchor is { IsEditing: false };

        [Display(Order = 1)]
        public ChartAnchor EntryAnchor { get; set; }
        [Display(Order = 2)]
        public ChartAnchor RiskAnchor { get; set; }
        [Browsable(false)]
        public ChartAnchor RewardAnchor { get; set; }

        public override object Icon => Icons.DrawRiskReward;

        [Range(0, double.MaxValue)]
        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardRatio_Mod", GroupName = "NinjaScriptGeneral", Order = 1)]
        public double Ratio
        {
            get => ratio;
            set
            {
                if (ratio.ApproxCompare(value) == 0)
                    return;
                ratio               = value;
                needsRatioUpdate    = true;
            }
        }

        // New property: if true, moving Stop or Target will NOT auto-adjust the other line
        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), Name = "Unlink Stop/Target From Ratio", GroupName = "NinjaScriptGeneral", Order = 99)]
        public bool UnlinkStopTargetFromRatio { get; set; } = true;

        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolAnchor", GroupName = "NinjaScriptLines", Order = 3)]
        public Stroke AnchorLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeEntry", GroupName = "NinjaScriptLines", Order = 6)]
        public Stroke EntryLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeRisk", GroupName = "NinjaScriptLines", Order = 4)]
        public Stroke StopLineStroke { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRiskRewardLineStrokeReward", GroupName = "NinjaScriptLines", Order = 5)]
        public Stroke TargetLineStroke { get; set; }

        public override IEnumerable<ChartAnchor> Anchors { get { return new[] { EntryAnchor, RiskAnchor, RewardAnchor }; } }

        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesRight", GroupName = "NinjaScriptLines", Order = 2)]
        public bool IsExtendedLinesRight { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolFibonacciRetracementsExtendLinesLeft", GroupName = "NinjaScriptLines", Order = 1)]
        public bool IsExtendedLinesLeft { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolTextAlignment", GroupName = "NinjaScriptGeneral", Order = 2)]
        public TextLocation TextAlignment { get; set; }
        [Display(ResourceType = typeof(Custom.Resource), Name = "NinjaScriptDrawingToolRulerYValueDisplayUnit", GroupName = "NinjaScriptGeneral", Order = 3)]
        public ValueUnit DisplayUnit { get; set; }

        public override bool SupportsAlerts => true;

        private void DrawPriceText(ChartAnchor anchor, Point point, double price, ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale)
        {
            if (TextAlignment == TextLocation.Off)
                return;

            ChartBars chartBars = GetAttachedToChartBars();

            // bars can be null while chart is initializing
            if (chartBars == null)
                return;

            // NS can change ChartAnchor price via Draw method or directly meaning we needed to resync price before drawing
            if (!IsUserDrawn)
                price = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(anchor.Price);

            // Build text according to anchor:
            // - Entry: "Price / RR 1:x"
            // - Stop/Target: "Currency / Price / Ticks: x"
            string priceString;
            try
            {
                var mi = AttachedTo.Instrument.MasterInstrument;
                double tickSize = mi.TickSize;
                double pointValue = mi.PointValue;
                double e = mi.RoundToTickSize(EntryAnchor.Price);
                double a = mi.RoundToTickSize(price); // this anchor's price

                // formatted price string
                string formattedPrice = chartBars.Bars.Instrument.MasterInstrument.FormatPrice(a);

                if (anchor == EntryAnchor)
                {
                    // compute RR from current anchors
                    double s = mi.RoundToTickSize(RiskAnchor.Price);
                    double t = mi.RoundToTickSize(RewardAnchor.Price);

                    double stopTicks = tickSize.ApproxCompare(0) != 0 ? Math.Abs(e - s) / tickSize : 0;
                    double targetTicks = tickSize.ApproxCompare(0) != 0 ? Math.Abs(t - e) / tickSize : 0;

                    double ratioCalc = stopTicks > 0 ? Math.Round(targetTicks / stopTicks, 2) : Ratio;
                    priceString = $"{formattedPrice} / RR 1:{ratioCalc}";
                }
                else // RiskAnchor or RewardAnchor
                {
                    double diff = (a - e);
                    double absDiffTicks = tickSize.ApproxCompare(0) != 0 ? Math.Abs(mi.RoundToTickSize(diff)) / tickSize : 0;
                    string ticksText = Math.Round(absDiffTicks, 0).ToString("F0");

                    string currencyText;
                    if (mi.InstrumentType == InstrumentType.Forex)
                    {
                        // respect forex lot size
                        double lotSize = Account.All.Count > 0 ? Account.All[0].ForexLotSize : 1.0;
                        double currencyValue = absDiffTicks * (tickSize * pointValue * lotSize);
                        currencyText = Core.Globals.FormatCurrency(currencyValue);
                    }
                    else
                    {
                        double currencyValue = absDiffTicks * (tickSize * pointValue);
                        currencyText = Core.Globals.FormatCurrency(currencyValue);
                    }

                    priceString = $"{currencyText} / {formattedPrice} / Ticks: {ticksText}";
                }
            }
            catch
            {
                // fallback to original formatting if anything goes wrong
                priceString = GetPriceString(price, chartBars);
            }

            Stroke color;
            textleftPoint   = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale).X;
            textRightPoint  = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale).X;

            if (anchor == RewardAnchor) color = TargetLineStroke;
            else if (anchor == RiskAnchor) color = StopLineStroke;
            else if (anchor == EntryAnchor) color = EntryLineStroke;
            else color = AnchorLineStroke;

            SimpleFont wpfFont = chartControl.Properties.LabelFont ?? new SimpleFont();
            SharpDX.DirectWrite.TextFormat textFormat = wpfFont.ToDirectWriteTextFormat();
            textFormat.TextAlignment                    = SharpDX.DirectWrite.TextAlignment.Leading;
            textFormat.WordWrapping                     = SharpDX.DirectWrite.WordWrapping.NoWrap;
            SharpDX.DirectWrite.TextLayout textLayout = new(Core.Globals.DirectWriteFactory, priceString, textFormat, chartPanel.H, textFormat.FontSize);

            if (RiskAnchor.Time <= EntryAnchor.Time)
            {
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textleftPoint,
                        TextLocation.InsideRight => textRightPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => textleftPoint,
                        TextLocation.ExtremeRight => textRightPoint - textLayout.Metrics.Width,
                        _ => point.X
                    };
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textleftPoint,
                        TextLocation.InsideRight => textRightPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => chartPanel.X,
                        TextLocation.ExtremeRight => textRightPoint - textLayout.Metrics.Width,
                        _ => point.X
                    };
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textleftPoint,
                        TextLocation.InsideRight => textRightPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => textleftPoint,
                        TextLocation.ExtremeRight => chartPanel.W - textLayout.Metrics.Width,
                        _ => point.X
                    };
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textleftPoint,
                        TextLocation.InsideRight => textRightPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeRight => chartPanel.W - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => chartPanel.X,
                        _ => point.X
                    };
            }
            else if (RiskAnchor.Time >= EntryAnchor.Time)
                if (!IsExtendedLinesLeft && !IsExtendedLinesRight)
                {
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textRightPoint,
                        TextLocation.InsideRight => textleftPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => textRightPoint,
                        TextLocation.ExtremeRight => textleftPoint - textLayout.Metrics.Width,
                        _ => point.X
                    };
                }
                else if (IsExtendedLinesLeft && !IsExtendedLinesRight)
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textRightPoint,
                        TextLocation.InsideRight => textleftPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => chartPanel.X,
                        TextLocation.ExtremeRight => textleftPoint - textLayout.Metrics.Width,
                        _ => point.X
                    };
                else if (!IsExtendedLinesLeft && IsExtendedLinesRight)
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textRightPoint,
                        TextLocation.InsideRight => textleftPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => textRightPoint,
                        TextLocation.ExtremeRight => chartPanel.W - textLayout.Metrics.Width,
                        _ => point.X
                    };
                else if (IsExtendedLinesLeft && IsExtendedLinesRight)
                    point.X = TextAlignment switch
                    {
                        TextLocation.InsideLeft => textRightPoint,
                        TextLocation.InsideRight => textleftPoint - textLayout.Metrics.Width,
                        TextLocation.ExtremeRight => chartPanel.W - textLayout.Metrics.Width,
                        TextLocation.ExtremeLeft => chartPanel.X,
                        _ => point.X
                    };

            RenderTarget.DrawTextLayout(new SharpDX.Vector2((float)point.X, (float)point.Y), textLayout, color.BrushDX, SharpDX.Direct2D1.DrawTextOptions.NoSnap);
        }

        public override IEnumerable<AlertConditionItem> GetAlertConditionItems() =>
            Anchors.Select(anchor => new AlertConditionItem { Name = anchor.DisplayName, ShouldOnlyDisplayName = true, Tag = anchor });

        public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, Point point)
        {
            switch (DrawingState)
            {
                case DrawingState.Building: return Cursors.Pen;
                case DrawingState.Moving: return IsLocked ? Cursors.No : Cursors.SizeAll;
                case DrawingState.Editing: return IsLocked ? Cursors.No : editingAnchor == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE;
                default:
                    // draw move cursor if cursor is near line path anywhere
                    Point entryAnchorPixelPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);

                    // see if we are near an anchor right away. this is is cheap so no big deal to do often
                    ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);

                    if (closest != null)
                        return IsLocked ? Cursors.Arrow : closest == EntryAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE;

                    Point stopAnchorPixelPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
                    Vector anchorsVector = stopAnchorPixelPoint - entryAnchorPixelPoint;

                    // see if the mouse is along one of our lines for moving
                    if (MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, anchorsVector, cursorSensitivity))
                        return IsLocked ? Cursors.Arrow : Cursors.SizeAll;

                    if (!DrawTarget)
                        return null;

                    Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
                    Vector targetToEntryVector = targetPoint - entryAnchorPixelPoint;
                    return MathHelper.IsPointAlongVector(point, entryAnchorPixelPoint, targetToEntryVector, cursorSensitivity) ? IsLocked ? Cursors.Arrow : Cursors.SizeAll : null;
            }
        }

        private string GetPriceString(double price, ChartBars chartBars)
        {
            string priceString;
            double yValueEntry = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            double tickSize = AttachedTo.Instrument.MasterInstrument.TickSize;
            double pointValue = AttachedTo.Instrument.MasterInstrument.PointValue;
            switch (DisplayUnit)
            {

                case ValueUnit.Currency:
                    if (AttachedTo.Instrument.MasterInstrument.InstrumentType == InstrumentType.Forex)
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize)) + " / Ticks: " + (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize).ToString("F0") :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue * Account.All[0].ForexLotSize)) + " / Ticks: " + (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize).ToString("F0");
                    }
                    else
                    {
                        priceString = price > yValueEntry ?
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize * (tickSize * pointValue)) + " / Ticks: " + (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize).ToString("F0") :
                            Core.Globals.FormatCurrency(AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize * (tickSize * pointValue)) + " / Ticks: " + (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize).ToString("F0");
                    }
                    break;
                case ValueUnit.Percent:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture) :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / yValueEntry).ToString("P", Core.Globals.GeneralOptions.CurrentCulture);
                    break;
                case ValueUnit.Ticks:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize).ToString("F0") :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize).ToString("F0");
                    break;
                case ValueUnit.Pips:
                    priceString = price > yValueEntry ?
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(price - yValueEntry) / tickSize / 10).ToString("F0") :
                        (AttachedTo.Instrument.MasterInstrument.RoundToTickSize(yValueEntry - price) / tickSize / 10).ToString("F0");
                    break;
                default:
                    priceString = chartBars.Bars.Instrument.MasterInstrument.FormatPrice(price);
                    break;
            }
            return priceString;
        }

        public override Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
        {
            ChartPanel chartPanel = chartControl.ChartPanels[chartScale.PanelIndex];
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);

            if (!DrawTarget)
                return new[] { entryPoint, stopPoint };

            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
            return new[] { entryPoint, stopPoint, targetPoint };
        }

        public override bool IsAlertConditionTrue(AlertConditionItem conditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
        {
            // dig up which anchor we are running on to determine line
            if (conditionItem.Tag is not ChartAnchor chartAnchor)
                return false;

            ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
            double alertY = chartScale.GetYByValue(chartAnchor.Price);
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);
            double anchorMinX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min() : new[] { entryPoint.X, stopPoint.X }.Min();
            double anchorMaxX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max() : new[] { entryPoint.X, stopPoint.X }.Max();
            double lineStartX = IsExtendedLinesLeft ? chartPanel.X : anchorMinX;
            double lineEndX = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

            // first thing, if our smallest x is greater than most recent bar, we have nothing to do yet.
            // do not try to check Y because lines could cross through stuff
            double firstBarX = chartControl.GetXByTime(values[0].Time);
            double firstBarY = chartScale.GetYByValue(values[0].Value);

            if (lineEndX < firstBarX) // bars passed our drawing tool
                return false;

            Point lineStartPoint = new(lineStartX, alertY);
            Point lineEndPoint = new(lineEndX, alertY);

            Point barPoint = new(firstBarX, firstBarY);
            // NOTE: 'left / right' is relative to if line was vertical. it can end up backwards too
            MathHelper.PointLineLocation pointLocation = MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, barPoint);
            // for vertical things, think of a vertical line rotated 90 degrees to lay flat, where it's normal vector is 'up'
            switch (condition)
            {
                case Condition.Greater: return pointLocation == MathHelper.PointLineLocation.LeftOrAbove;
                case Condition.GreaterEqual: return pointLocation == MathHelper.PointLineLocation.LeftOrAbove || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.Less: return pointLocation == MathHelper.PointLineLocation.RightOrBelow;
                case Condition.LessEqual: return pointLocation == MathHelper.PointLineLocation.RightOrBelow || pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.Equals: return pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.NotEqual: return pointLocation != MathHelper.PointLineLocation.DirectlyOnLine;
                case Condition.CrossAbove:
                case Condition.CrossBelow:

                    bool Predicate(ChartAlertValue v)
                    {
                        double barX = chartControl.GetXByTime(v.Time);
                        double barY = chartScale.GetYByValue(v.Value);
                        Point stepBarPoint = new(barX, barY);
                        // NOTE: 'left / right' is relative to if line was vertical. it can end up backwards too
                        MathHelper.PointLineLocation ptLocation = MathHelper.GetPointLineLocation(lineStartPoint, lineEndPoint, stepBarPoint);
                        if (condition == Condition.CrossAbove) return ptLocation == MathHelper.PointLineLocation.LeftOrAbove;
                        return ptLocation == MathHelper.PointLineLocation.RightOrBelow;
                    }

                    return MathHelper.DidPredicateCross(values, Predicate);
            }
            return false;
        }

        public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
            => DrawingState == DrawingState.Building || Anchors.Any(a => a.Time >= firstTimeOnChart && a.Time <= lastTimeOnChart);

        public override void OnCalculateMinMax()
        {
            // It is important to set MinValue and MaxValue to the min/max Y values your drawing tool uses if you want it to support auto scale
            MinValue = double.MaxValue;
            MaxValue = double.MinValue;

            if (!IsVisible)
                return;

            // return min/max values only if something has been actually drawn
            if (Anchors.Any(a => !a.IsEditing))
                foreach (ChartAnchor anchor in Anchors)
                {
                    if (anchor.DisplayName == RewardAnchor.DisplayName && !DrawTarget)
                        continue;

                    MinValue = Math.Min(anchor.Price, MinValue);
                    MaxValue = Math.Max(anchor.Price, MaxValue);
                }
        }

        public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            switch (DrawingState)
            {
                case DrawingState.Building:
                    if (EntryAnchor.IsEditing)
                    {
                        dataPoint.CopyDataValues(EntryAnchor);
                        dataPoint.CopyDataValues(RiskAnchor);
                        EntryAnchor.IsEditing   = false;
                        entryPrice              = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);

                        // record last edit origin
                        lastEdit = EditOrigin.Entry;
                    }
                    else if (RiskAnchor.IsEditing)
                    {
                        dataPoint.CopyDataValues(RiskAnchor);
                        RiskAnchor.IsEditing    = false;
                        stopPrice               = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
                        // Previously we automatically calculated the target from stop + ratio here.
                        // That behavior is removed so Stop and Target remain independent during building.
                        // we set the anchor for the target after stop mouse down event via copying time/slot so it appears in view
                        RewardAnchor.Time       = EntryAnchor.Time;
                        RewardAnchor.SlotIndex  = EntryAnchor.SlotIndex;
                        RewardAnchor.IsEditing  = false;

                        // record last edit origin
                        lastEdit = EditOrigin.Stop;
                    }
                    else if (RewardAnchor.IsEditing)
                    {
                        dataPoint.CopyDataValues(RewardAnchor);
                        RewardAnchor.IsEditing = false;

                        // record last edit origin
                        lastEdit = EditOrigin.Target;
                    }
                    // if the anchors are no longer being edited, set the drawing state to normal and unselect the object
                    if (!EntryAnchor.IsEditing && !RiskAnchor.IsEditing && !RewardAnchor.IsEditing)
                    {
                        DrawingState = DrawingState.Normal;
                        IsSelected = false;
                    }
                    break;
                case DrawingState.Normal:
                    Point point = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
                    //find which anchor has been clicked relative to the mouse point and make whichever anchor now editable
                    editingAnchor = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);
                    if (editingAnchor != null)
                    {
                        editingAnchor.IsEditing = true;
                        DrawingState = DrawingState.Editing;

                        // set lastEdit while editing so SetReward/SetRisk can check it
                        if (editingAnchor == EntryAnchor) lastEdit = EditOrigin.Entry;
                        else if (editingAnchor == RiskAnchor) lastEdit = EditOrigin.Stop;
                        else if (editingAnchor == RewardAnchor) lastEdit = EditOrigin.Target;

                        // mark this tool as user-edited so subsequent entry moves don't auto-update Stop/Target
                        hasUserEdited = true;
                    }
                    else if (GetCursor(chartControl, chartPanel, chartScale, point) == null)
                        IsSelected = false; // missed
                    else
                        // didnt click an anchor but on a line so start moving
                        DrawingState = DrawingState.Moving;
                    break;
            }
        }

        public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            if (IsLocked && DrawingState != DrawingState.Building || !IsVisible)
                return;

            if (DrawingState == DrawingState.Building)
            {
                if (EntryAnchor.IsEditing)
                    dataPoint.CopyDataValues(EntryAnchor);
                else if (RiskAnchor.IsEditing)
                    dataPoint.CopyDataValues(RiskAnchor);
                else if (RewardAnchor.IsEditing)
                    dataPoint.CopyDataValues(RewardAnchor);
            }
            else if (DrawingState == DrawingState.Editing && editingAnchor != null)
            {
                // copy the edited anchor values into the anchor
                dataPoint.CopyDataValues(editingAnchor);

                // Important change:
                // Do NOT auto-adjust the opposite anchor when editing stop or target.
                // This unlinks Stop and Target from automatic updates based on Ratio when one of them is moved.
                // The entry anchor behavior (when present) still triggers recalculation on finalize below.

                // keep lastEdit set to editing anchor
                if (editingAnchor == EntryAnchor) lastEdit = EditOrigin.Entry;
                else if (editingAnchor == RiskAnchor) lastEdit = EditOrigin.Stop;
                else if (editingAnchor == RewardAnchor) lastEdit = EditOrigin.Target;

                // ensure that any interactive edit is considered a user edit
                hasUserEdited = true;
            }
            else if (DrawingState == DrawingState.Moving)
                foreach (ChartAnchor anchor in Anchors)
                    anchor.MoveAnchor(InitialMouseDownAnchor, dataPoint, chartControl, chartPanel, chartScale, this);

            entryPrice  = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            stopPrice   = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
            targetPrice = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);
        }

        public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
        {
            //don't set anchors until we're done drawing
            if (DrawingState == DrawingState.Building)
                return;

            //set the drawing state back to normal when mouse is relased
            if (DrawingState == DrawingState.Editing || DrawingState == DrawingState.Moving)
                DrawingState = DrawingState.Normal;
            if (editingAnchor != null)
            {
                if (editingAnchor == EntryAnchor)
                {
                    // Only auto-recalculate if the tool has NOT yet been user-edited.
                    // This preserves the initial automatic calculation but prevents subsequent entry moves
                    // from changing Stop/Target once the user has made a manual change.
                    if (!UnlinkStopTargetFromRatio || !hasUserEdited)
                    {
                        SetReward();
                        if (Ratio.ApproxCompare(0) != 0)
                            SetRisk();
                    }
                }
                // when editing stop/target we no longer auto-adjust the other anchor here
                editingAnchor.IsEditing = false;
            }
            editingAnchor = null;

            // reset lastEdit to None so subsequent ratio changes behave normally
            lastEdit = EditOrigin.None;
        }

        public override void OnRender(ChartControl chartControl, ChartScale chartScale)
        {
            if (!IsVisible)
                return;
            if (Anchors.All(a => a.IsEditing))
                return;

            // this will be true right away to fix a restoral issue, so check if we really want to set reward
            // Only do the automatic SetReward on render if unlinking is disabled OR this is an initialization/ratio-driven update.
            if (needsRatioUpdate && DrawTarget)
            {
                // force the update during initialization / ratio changes even if Unlink is true,
                // but normal interactive edits of stop/target won't trigger SetReward because hasUserEdited will be true after user interaction.
                SetReward(true);
            }

            ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];
            Point entryPoint = EntryAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point stopPoint = RiskAnchor.GetPoint(chartControl, chartPanel, chartScale);
            Point targetPoint = RewardAnchor.GetPoint(chartControl, chartPanel, chartScale);

            AnchorLineStroke.RenderTarget   = RenderTarget;
            EntryLineStroke.RenderTarget    = RenderTarget;
            StopLineStroke.RenderTarget     = RenderTarget;

            // first of all, turn on anti-aliasing to smooth out our line
            RenderTarget.AntialiasMode  = SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
            RenderTarget.DrawLine(entryPoint.ToVector2(), stopPoint.ToVector2(), AnchorLineStroke.BrushDX, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

            double anchorMinX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Min() : new[] { entryPoint.X, stopPoint.X }.Min();
            double anchorMaxX = DrawTarget ? new[] { entryPoint.X, stopPoint.X, targetPoint.X }.Max() : new[] { entryPoint.X, stopPoint.X }.Max();
            double lineStartX = IsExtendedLinesLeft ? chartPanel.X : anchorMinX;
            double lineEndX = IsExtendedLinesRight ? chartPanel.X + chartPanel.W : anchorMaxX;

            SharpDX.Vector2 entryStartVector = new((float)lineStartX, (float)entryPoint.Y);
            SharpDX.Vector2 entryEndVector = new((float)lineEndX, (float)entryPoint.Y);
            SharpDX.Vector2 stopStartVector = new((float)lineStartX, (float)stopPoint.Y);
            SharpDX.Vector2 stopEndVector = new((float)lineEndX, (float)stopPoint.Y);

            // don't try and draw the target stuff until we have calculated the target
            SharpDX.Direct2D1.Brush tmpBrush = IsInHitTest ? chartControl.SelectionBrush : AnchorLineStroke.BrushDX;
            if (DrawTarget)
            {
                AnchorLineStroke.RenderTarget   = RenderTarget;
                RenderTarget.DrawLine(entryPoint.ToVector2(), targetPoint.ToVector2(), tmpBrush, AnchorLineStroke.Width, AnchorLineStroke.StrokeStyle);

                TargetLineStroke.RenderTarget       = RenderTarget;
                SharpDX.Vector2 targetStartVector = new((float)lineStartX, (float)targetPoint.Y);
                SharpDX.Vector2 targetEndVector = new((float)lineEndX, (float)targetPoint.Y);

                tmpBrush = IsInHitTest ? chartControl.SelectionBrush : TargetLineStroke.BrushDX;
                RenderTarget.DrawLine(targetStartVector, targetEndVector, tmpBrush, TargetLineStroke.Width, TargetLineStroke.StrokeStyle);
                DrawPriceText(RewardAnchor, targetPoint, targetPrice, chartControl, chartPanel, chartScale);
            }

            tmpBrush = IsInHitTest ? chartControl.SelectionBrush : EntryLineStroke.BrushDX;
            RenderTarget.DrawLine(entryStartVector, entryEndVector, tmpBrush, EntryLineStroke.Width, EntryLineStroke.StrokeStyle);
            DrawPriceText(EntryAnchor, entryPoint, entryPrice, chartControl, chartPanel, chartScale);

            tmpBrush = IsInHitTest ? chartControl.SelectionBrush : StopLineStroke.BrushDX;
            RenderTarget.DrawLine(stopStartVector, stopEndVector, tmpBrush, StopLineStroke.Width, StopLineStroke.StrokeStyle);
            DrawPriceText(RiskAnchor, stopPoint, stopPrice, chartControl, chartPanel, chartScale);
        }

        protected override void OnStateChange()
        {
            if (State == State.SetDefaults)
            {
                Description                 = Custom.Resource.NinjaScriptDrawingToolRiskRewardDescription;
                Name                        = "RiskReward_Mod";
                Ratio                       = 1;
                AnchorLineStroke            = new Stroke(Brushes.DarkGray, DashStyleHelper.Solid, 1f, 50);
                EntryLineStroke             = new Stroke(Brushes.Goldenrod, DashStyleHelper.Solid, 2f);
                StopLineStroke              = new Stroke(Brushes.Crimson, DashStyleHelper.Solid, 2f);
                TargetLineStroke            = new Stroke(Brushes.SeaGreen, DashStyleHelper.Solid, 2f);
                EntryAnchor                 = new ChartAnchor { IsEditing = true, DrawingTool = this };
                RiskAnchor                  = new ChartAnchor { IsEditing = true, DrawingTool = this };
                RewardAnchor                = new ChartAnchor { IsEditing = true, DrawingTool = this };
                EntryAnchor.DisplayName     = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorEntry;
                RiskAnchor.DisplayName      = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorRisk;
                RewardAnchor.DisplayName    = Custom.Resource.NinjaScriptDrawingToolRiskRewardAnchorReward;
            }
            else if (State == State.Terminated)
                Dispose();
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetReward(bool force = false)
        {
            // If unlinking is enabled and the user has already edited the tool interactively,
            // skip auto-adjust unless a caller forces the update.
            if (UnlinkStopTargetFromRatio && !force && hasUserEdited)
                return;

            if (Anchors == null || AttachedTo == null)
                return;

            entryPrice              = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            stopPrice               = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RiskAnchor.Price);
            risk                    = entryPrice - stopPrice;
            reward                  = risk * Ratio;
            targetPrice             = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice + reward);

            RewardAnchor.Price      = targetPrice;
            RewardAnchor.IsEditing  = false;

            needsRatioUpdate        = false;
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void SetRisk(bool force = false)
        {
            // If unlinking is enabled and the user has already edited the tool interactively,
            // skip auto-adjust unless a caller forces the update.
            if (UnlinkStopTargetFromRatio && !force && hasUserEdited)
                return;

            if (Anchors == null || AttachedTo == null)
                return;

            entryPrice              = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(EntryAnchor.Price);
            targetPrice             = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(RewardAnchor.Price);

            reward                  = targetPrice - entryPrice;
            risk                    = reward / Ratio;
            stopPrice               = AttachedTo.Instrument.MasterInstrument.RoundToTickSize(entryPrice - risk);

            RiskAnchor.Price        = stopPrice;
            RiskAnchor.IsEditing    = false;

            needsRatioUpdate        = false;
        }
    }

    public static partial class Draw
    {
        private static RiskReward_Mod RiskRewardCore_Mod(NinjaScriptBase owner, string tag,
            bool isAutoScale,
            int entryBarsAgo, DateTime entryTime, double entryY,
            int stopBarsAgo, DateTime stopTime, double stopY,
            int targetBarsAgo, DateTime targetTime, double targetY,
            double ratio, bool isStop, bool isGlobal, string templateName)
        {
            if (owner == null)
                throw new ArgumentException("owner");

            if (entryBarsAgo == int.MinValue && entryTime == Core.Globals.MinDate)
                throw new ArgumentException("entry value required");

            if (stopBarsAgo == int.MinValue && stopTime == Core.Globals.MinDate &&
                targetBarsAgo == int.MinValue && targetTime == Core.Globals.MinDate)
                throw new ArgumentException("a stop or target value is required");

            if (isGlobal && tag[0] != GlobalDrawingToolManager.GlobalDrawingToolTagPrefix)
                tag = $"{GlobalDrawingToolManager.GlobalDrawingToolTagPrefix}{tag}";

            if (DrawingTool.GetByTagOrNew(owner, typeof(RiskReward_Mod), tag, templateName) is not RiskReward_Mod riskReward)
                return null;

            DrawingTool.SetDrawingToolCommonValues(riskReward, tag, isAutoScale, owner, isGlobal);

            // this is a little tricky, we use entry + (stop or target) to calculate the (target or stop) from ratio
            ChartAnchor entryAnchor = DrawingTool.CreateChartAnchor(owner, entryBarsAgo, entryTime, entryY);

            riskReward.Ratio = ratio;

            if (isStop)
            {
                ChartAnchor stopAnchor = DrawingTool.CreateChartAnchor(owner, stopBarsAgo, stopTime, stopY);
                entryAnchor.CopyDataValues(riskReward.EntryAnchor);
                entryAnchor.CopyDataValues(riskReward.RewardAnchor);
                stopAnchor.CopyDataValues(riskReward.RiskAnchor);
                // force the initialization calculation
                riskReward.SetReward(true);
            }
            else
            {
                ChartAnchor targetAnchor = DrawingTool.CreateChartAnchor(owner, targetBarsAgo, targetTime, targetY);
                entryAnchor.CopyDataValues(riskReward.EntryAnchor);
                entryAnchor.CopyDataValues(riskReward.RiskAnchor);
                targetAnchor.CopyDataValues(riskReward.RewardAnchor);
                // force the initialization calculation
                riskReward.SetRisk(true);
            }

            riskReward.SetState(State.Active);
            return riskReward;
        }

        /// <summary>
        /// Draws a risk/reward on a chart.
        /// </summary>
        /// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
        /// <param name="tag">A user defined unique id used to reference the draw object</param>
        /// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
        /// <param name="entryTime">The time where the draw object's entry will be drawn</param>
        /// <param name="entryY">The y value coordinate where the draw object's entry price will be drawn</param>
        /// <param name="endTime">The end time where the draw object will terminate</param>
        /// <param name="endY">The end y value coordinate where the draw object's end price will be drawn</param>
        /// <param name="ratio">An int value determining the calculated ratio between the risk or reward based on the entry point</param>
        /// <param name="isStop">A bool value, when true will use the endTime/endBarsAgo and endY to set the stop, and will automatically calculate the target based off the ratio value.</param>
        /// <returns></returns>
        public static RiskReward_Mod RiskReward_Mod(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime entryTime, double entryY, DateTime endTime, double endY, double ratio, bool isStop) =>
            isStop
                ? RiskRewardCore_Mod(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, int.MinValue, endTime, endY, 0, Core.Globals.MinDate, 0, ratio, true, false, null)
                : RiskRewardCore_Mod(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, 0, Core.Globals.MinDate, 0, int.MinValue, endTime, endY, ratio, false, false, null);

        /// <summary>
        /// Draws a risk/reward on a chart.
        /// </summary>
        /// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
        /// <param name="tag">A user defined unique id used to reference the draw object</param>
        /// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
        /// <param name="entryBarsAgo">The starting bar (x axis coordinate) where the draw object's entry will be drawn. For example, a value of 10 would paint the draw object 10 bars back.</param>
        /// <param name="entryY">The y value coordinate where the draw object's entry price will be drawn</param>
        /// <param name="endBarsAgo">The end bar (x axis coordinate) where the draw object will terminate</param>
        /// <param name="endY">The end y value coordinate where the draw object's end price will be drawn</param>
        /// <param name="ratio">An int value determining the calculated ratio between the risk or reward based on the entry point</param>
        /// <param name="isStop">A bool value, when true will use the endTime/endBarsAgo and endY to set the stop, and will automatically calculate the target based off the ratio value.</param>
        /// <returns></returns>
        public static RiskReward_Mod RiskReward_Mod(NinjaScriptBase owner, string tag, bool isAutoScale, int entryBarsAgo, double entryY, int endBarsAgo, double endY, double ratio, bool isStop) =>
            isStop
                ? RiskRewardCore_Mod(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, endBarsAgo, Core.Globals.MinDate, endY, 0, Core.Globals.MinDate, 0, ratio, true, false, null)
                : RiskRewardCore_Mod(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, 0, Core.Globals.MinDate, 0, endBarsAgo, Core.Globals.MinDate, endY, ratio, false, false, null);

        /// <summary>
        /// Draws a risk/reward on a chart.
        /// </summary>
        /// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
        /// <param name="tag">A user defined unique id used to reference the draw object</param>
        /// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
        /// <param name="entryTime">The time where the draw object's entry will be drawn</param>
        /// <param name="entryY">The y value coordinate where the draw object's entry price will be drawn</param>
        /// <param name="endTime">The end time where the draw object will terminate</param>
        /// <param name="endY">The end y value coordinate where the draw object's end price will be drawn</param>
        /// <param name="ratio">An int value determining the calculated ratio between the risk or reward based on the entry point</param>
        /// <param name="isStop">A bool value, when true will use the endTime/endBarsAgo and endY to set the stop, and will automatically calculate the target based off the ratio value.</param>
        /// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
        /// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
        /// <returns></returns>
        public static RiskReward_Mod RiskReward_Mod(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime entryTime, double entryY, DateTime endTime, double endY, double ratio, bool isStop, bool isGlobal, string templateName) =>
            isStop
                ? RiskRewardCore_Mod(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, int.MinValue, endTime, endY, 0, Core.Globals.MinDate, 0, ratio, true, isGlobal, templateName)
                : RiskRewardCore_Mod(owner, tag, isAutoScale, int.MinValue, entryTime, entryY, 0, Core.Globals.MinDate, 0, int.MinValue, endTime, endY, ratio, false, isGlobal, templateName);

        /// <summary>
        /// Draws a risk/reward on a chart.
        /// </summary>
        /// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
        /// <param name="tag">A user defined unique id used to reference the draw object</param>
        /// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
        /// <param name="entryBarsAgo">The starting bar (x axis coordinate) where the draw object's entry will be drawn. For example, a value of 10 would paint the draw object 10 bars back.</param>
        /// <param name="entryY">The y value coordinate where the draw object's entry price will be drawn</param>
        /// <param name="endBarsAgo">The end bar (x axis coordinate) where the draw object will terminate</param>
        /// <param name="endY">The end y value coordinate where the draw object's end price will be drawn</param>
        /// <param name="ratio">An int value determining the calculated ratio between the risk or reward based on the entry point</param>
        /// <param name="isStop">A bool value, when true will use the endTime/endBarsAgo and endY to set the stop, and will automatically calculate the target based off the ratio value.</param>
        /// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
        /// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
        /// <returns></returns>
        public static RiskReward_Mod RiskReward_Mod(NinjaScriptBase owner, string tag, bool isAutoScale, int entryBarsAgo, double entryY, int endBarsAgo, double endY, double ratio, bool isStop, bool isGlobal, string templateName) =>
            isStop
                ? RiskRewardCore_Mod(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, endBarsAgo, Core.Globals.MinDate, endY, 0, Core.Globals.MinDate, 0, ratio, true, isGlobal, templateName)
                : RiskRewardCore_Mod(owner, tag, isAutoScale, entryBarsAgo, Core.Globals.MinDate, entryY, 0, Core.Globals.MinDate, 0, endBarsAgo, Core.Globals.MinDate, endY, ratio, false, isGlobal, templateName);
    }
}
