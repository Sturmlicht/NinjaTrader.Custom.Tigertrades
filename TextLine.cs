
#region Using declarations
using NinjaTrader.Core.FloatingPoint;
using NinjaTrader.Gui;
using NinjaTrader.Gui.Chart;
using NinjaTrader.Gui.Tools;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Xml.Serialization;
#endregion

namespace NinjaTrader.NinjaScript.DrawingTools
{

	public class TextLine : DrawingTool
	{
		// this line class takes care of all stock line types, so we use this to keep track
		// of what kind of line instances this is. Localization is not needed because it's not visible on ui
		protected enum ChartLineType
		{
		
			TextLine
			
		}

        // New: text on line support
        public enum TextAnchorPosition
        {
            First = 0,
            Second = 1,
        }


        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), GroupName = "NinjaScriptGeneral", Name = "LineText", Order = 100)]
        public string CustomText { get; set; } = "TextLine"; 

        [NinjaScriptProperty]
        [Display(ResourceType = typeof(Custom.Resource), GroupName = "NinjaScriptGeneral", Name = "LineTextAnchor", Order = 101)]
        public TextAnchorPosition TextAnchor { get; set; } = TextAnchorPosition.Second;


        public override IEnumerable<ChartAnchor> Anchors => new[] { StartAnchor, EndAnchor };

		[Display(Order = 2)]
		public ChartAnchor	EndAnchor		{ get; set; }
		[Display(Order = 1)]
		public ChartAnchor StartAnchor		{ get; set; }

		public override object Icon => Gui.Tools.Icons.DrawLineTool;

		[CLSCompliant(false)]
		protected		SharpDX.Direct2D1.PathGeometry		ArrowPathGeometry;
		private			ChartAnchor							cursorAnchor;
		private	const	double								cursorSensitivity		= 15;
		private			ChartAnchor							editingAnchor;

		[Browsable(false)]
		[XmlIgnore]
		protected ChartLineType LineType { get; set; }

		[Display(ResourceType = typeof(Custom.Resource), GroupName = "NinjaScriptGeneral", Name = "NinjaScriptDrawingToolLine", Order = 99)]
		public Stroke Stroke { get; set; }

		public override bool SupportsAlerts => true;

		/// <summary>
		/// If either Shift key is engaged, returns a new ChartAnchor of the endAnchor param adjusted to the nearest 45 degree multiple
		/// </summary>
		private ChartAnchor Anchor45(ChartAnchor startAnchor, ChartAnchor endAnchor, ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale)
		{
			if (IsLocked)
				return endAnchor;
			if (!Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
				return endAnchor;

			Point	startPoint	= startAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point	endPoint	= endAnchor.GetPoint(chartControl, chartPanel, chartScale);

			double	diffX		= endPoint.X - startPoint.X;
			double	diffY		= endPoint.Y - startPoint.Y;

			double	length		= Math.Sqrt(diffX * diffX + diffY * diffY);

			double	angle		= Math.Atan2(diffY, diffX);

			double step			= Math.PI / 8;
			double targetAngle	= 0;

			if (angle > Math.PI - step || angle < -Math.PI + step)	targetAngle = Math.PI;
			else if (angle > Math.PI - step * 3)					targetAngle = Math.PI - step * 2;
			else if (angle > Math.PI - step * 5)					targetAngle = Math.PI - step * 4;
			else if (angle > Math.PI - step * 7)					targetAngle = Math.PI - step * 6;
			else if (angle < -Math.PI + step * 3)					targetAngle = -Math.PI + step * 2;
			else if (angle < -Math.PI + step * 5)					targetAngle = -Math.PI + step * 4;
			else if (angle < -Math.PI + step * 7)					targetAngle = -Math.PI + step * 6;

			Point		targetPoint = new(startPoint.X + Math.Cos(targetAngle) * length, startPoint.Y + Math.Sin(targetAngle) * length);
			ChartAnchor	ret			= new();

			ret.UpdateFromPoint(targetPoint, chartControl, chartScale);

			if (Math.Abs(startPoint.X - targetPoint.X) < 0.00001)
			{
				ret.Time		= startAnchor.Time;
				ret.SlotIndex	=startAnchor.SlotIndex;
			}
			else if (Math.Abs(startPoint.Y - targetPoint.Y) < 0.00001)
				ret.Price = startAnchor.Price;

			return ret;
		}

		public override Cursor GetCursor(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, Point point)
		{
			switch (DrawingState)
			{
				case DrawingState.Building:	return Cursors.Pen;
				case DrawingState.Moving:	return IsLocked ? Cursors.No : Cursors.SizeAll;
				case DrawingState.Editing:
					if (IsLocked)
						return Cursors.No;
					
					return editingAnchor == StartAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE;
				default:
					// draw move cursor if cursor is near line path anywhere
					Point startPoint = StartAnchor.GetPoint(chartControl, chartPanel, chartScale);

					

					ChartAnchor closest = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);
					if (closest != null)
					{
						if (IsLocked)
							return Cursors.Arrow;
						return closest == StartAnchor ? Cursors.SizeNESW : Cursors.SizeNWSE;
					}

					Point	endPoint		= EndAnchor.GetPoint(chartControl, chartPanel, chartScale);
					Point	minPoint		= startPoint;
					Point	maxPoint		= endPoint;



					Vector	totalVector	= maxPoint - minPoint;
					return MathHelper.IsPointAlongVector(point, minPoint, totalVector, cursorSensitivity) ?
						IsLocked ? Cursors.Arrow : Cursors.SizeAll : null;
			}
		}

		public override IEnumerable<AlertConditionItem> GetAlertConditionItems()
		{
			yield return new AlertConditionItem
			{
				Name					= Custom.Resource.NinjaScriptDrawingToolLine,
				ShouldOnlyDisplayName	= true
			};
		}

		public sealed override Point[] GetSelectionPoints(ChartControl chartControl, ChartScale chartScale)
		{
			ChartPanel	chartPanel	= chartControl.ChartPanels[chartScale.PanelIndex];
			Point		startPoint	= StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point		endPoint	= EndAnchor.GetPoint(chartControl, chartPanel, chartScale);

			int			totalWidth	= chartPanel.W + chartPanel.X;
			int			totalHeight	= chartPanel.Y + chartPanel.H;


			//Vector strokeAdj = new Vector(Stroke.Width / 2, Stroke.Width / 2);
			Point midPoint = startPoint + (endPoint - startPoint) / 2;
			return new[]{ startPoint, midPoint, endPoint };
		}

		public override bool IsAlertConditionTrue(AlertConditionItem conditionItem, Condition condition, ChartAlertValue[] values, ChartControl chartControl, ChartScale chartScale)
		{
			if (values.Length < 1)
				return false;
			ChartPanel chartPanel = chartControl.ChartPanels[PanelIndex];


			// get start / end points of what is absolutely shown for our vector
			Point lineStartPoint	= StartAnchor.GetPoint(chartControl, chartPanel, chartScale);
			Point lineEndPoint		= EndAnchor.GetPoint(chartControl, chartPanel, chartScale);

			double minLineX = double.MaxValue;
			double maxLineX = double.MinValue;

			foreach (Point point in new[]{lineStartPoint, lineEndPoint})
			{
				minLineX = Math.Min(minLineX, point.X);
				maxLineX = Math.Max(maxLineX, point.X);
			}

			// first thing, if our smallest x is greater than most recent bar, we have nothing to do yet.
			// do not try to check Y because lines could cross through stuff
			double firstBarX = values[0].ValueType == ChartAlertValueType.StaticValue ? minLineX : chartControl.GetXByTime(values[0].Time);
			double firstBarY = chartScale.GetYByValue(values[0].Value);

			// dont have to take extension into account as its already handled in min/max line x

			// bars completely passed our line
			if (maxLineX < firstBarX)
				return false;

			// bars not yet to our line
			if (minLineX > firstBarX)
				return false;

			// NOTE: normalize line points so the leftmost is passed first. Otherwise, our vector
			// math could end up having the line normal vector being backwards if user drew it backwards.
			// but we dont care the order of anchors, we want 'up' to mean 'up'!
			Point leftPoint		= lineStartPoint.X < lineEndPoint.X ? lineStartPoint : lineEndPoint;
			Point rightPoint	= lineEndPoint.X > lineStartPoint.X ? lineEndPoint : lineStartPoint;

			Point barPoint = new(firstBarX, firstBarY);
			// NOTE: 'left / right' is relative to if line was vertical. it can end up backwards too
			MathHelper.PointLineLocation pointLocation = MathHelper.GetPointLineLocation(leftPoint, rightPoint, barPoint);
			// for vertical things, think of a vertical line rotated 90 degrees to lay flat, where it's normal vector is 'up'
			switch (condition)
			{
				case Condition.Greater:			return pointLocation == MathHelper.PointLineLocation.LeftOrAbove;
				case Condition.GreaterEqual:	return pointLocation is MathHelper.PointLineLocation.LeftOrAbove or MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.Less:			return pointLocation == MathHelper.PointLineLocation.RightOrBelow;
				case Condition.LessEqual:		return pointLocation is MathHelper.PointLineLocation.RightOrBelow or MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.Equals:			return pointLocation == MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.NotEqual:		return pointLocation != MathHelper.PointLineLocation.DirectlyOnLine;
				case Condition.CrossAbove:
				case Condition.CrossBelow:

					bool Predicate(ChartAlertValue v)
					{
						double barX = chartControl.GetXByTime(v.Time);
						double barY = chartScale.GetYByValue(v.Value);
						Point stepBarPoint = new(barX, barY);
						MathHelper.PointLineLocation ptLocation = MathHelper.GetPointLineLocation(leftPoint, rightPoint, stepBarPoint);
						if (condition == Condition.CrossAbove) return ptLocation == MathHelper.PointLineLocation.LeftOrAbove;
						return ptLocation == MathHelper.PointLineLocation.RightOrBelow;
					}

					return MathHelper.DidPredicateCross(values, Predicate);
			}

			return false;
		}

		public override bool IsVisibleOnChart(ChartControl chartControl, ChartScale chartScale, DateTime firstTimeOnChart, DateTime lastTimeOnChart)
		{
			if (DrawingState == DrawingState.Building)
				return true;

			DateTime	minTime = Core.Globals.MaxDate;
			DateTime	maxTime = Core.Globals.MinDate;


				// extended line, rays: here we'll get extended point and see if they're on scale
				ChartPanel	panel		= chartControl.ChartPanels[PanelIndex];
				Point		startPoint	= StartAnchor.GetPoint(chartControl, panel, chartScale);

				Point		minPoint	= startPoint;
				Point		maxPoint	= GetExtendedPoint(chartControl, panel, chartScale, StartAnchor, EndAnchor);


				foreach (Point pt in new[] { minPoint, maxPoint })
				{
					DateTime time = chartControl.GetTimeByX((int) pt.X);
					if (time > maxTime)
						maxTime = time;
					if (time < minTime)
						minTime = time;
				}
			


			// hline extends, but otherwise try to check if line horizontally crosses through visible chart times in some way
			if ( (minTime > lastTimeOnChart || maxTime < firstTimeOnChart))
				return false;

			return true;
		}

		public override void OnCalculateMinMax()
		{
			MinValue = double.MaxValue;
			MaxValue = double.MinValue;

			if (!IsVisible)
				return;



				// return min/max values only if something has been actually drawn
				if (Anchors.Any(a => !a.IsEditing))
					foreach (ChartAnchor anchor in Anchors)
					{
						MinValue = Math.Min(anchor.Price, MinValue);
						MaxValue = Math.Max(anchor.Price, MaxValue);
					}
			
		}

		public override void OnKeyDown(ChartControl chartControl, ChartPanel chartPanel, KeyEventArgs e)
		{
			if (DrawingState != DrawingState.Editing && DrawingState != DrawingState.Building || EndAnchor == null)
				return;

			if (e.Key is Key.LeftShift or Key.RightShift &&
				(EndAnchor.IsEditing || DrawingState == DrawingState.Editing && StartAnchor.IsEditing))
				ForceRefresh();
		}

		public override void OnKeyUp(ChartControl chartControl, ChartPanel chartPanel, KeyEventArgs e)
		{
			if (DrawingState != DrawingState.Editing && DrawingState != DrawingState.Building || EndAnchor == null)
				return;

			if (e.Key is Key.LeftShift or Key.RightShift &&
				(EndAnchor.IsEditing || DrawingState == DrawingState.Editing && StartAnchor.IsEditing))
				ForceRefresh();
		}

		public override void OnMouseDown(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			switch (DrawingState)
			{
				case DrawingState.Building:
					if (StartAnchor.IsEditing)
					{
						dataPoint.CopyDataValues(StartAnchor);
						StartAnchor.IsEditing = false;


						// give end anchor something to start with so we dont try to render it with bad values right away
						dataPoint.CopyDataValues(EndAnchor);
					}
					else if (EndAnchor.IsEditing)
					{
						// NT-3435 - Anchor45 checks for an engaged Left/Right Shift key to draw the line on the nearest 45 degree multiple
						Anchor45(StartAnchor, dataPoint, chartControl, chartPanel, chartScale).CopyDataValues(EndAnchor);
						EndAnchor.IsEditing = false;
					}

					// is initial building done (both anchors set)
					if (!StartAnchor.IsEditing && !EndAnchor.IsEditing)
					{
						DrawingState = DrawingState.Normal;
						IsSelected = false;
					}
					break;
				case DrawingState.Normal:
					Point point = dataPoint.GetPoint(chartControl, chartPanel, chartScale);
					// see if they clicked near a point to edit, if so start editing

							// we dont care here, since we're moving just one anchor
							editingAnchor = StartAnchor;
						
					
					editingAnchor = GetClosestAnchor(chartControl, chartPanel, chartScale, cursorSensitivity, point);

					if (editingAnchor != null)
					{
						editingAnchor.IsEditing = true;
						cursorAnchor = dataPoint;
						DrawingState = DrawingState.Editing;
					}
					else
					{
						if (GetCursor(chartControl, chartPanel, chartScale, point) != null)
							DrawingState = DrawingState.Moving;
						else
						// user whiffed.
							IsSelected = false;
					}
					break;
			}
		}

		public override void OnMouseMove(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			if (IsLocked && DrawingState != DrawingState.Building)
				return;

			cursorAnchor = dataPoint;

			if (DrawingState == DrawingState.Building)
			{
				// start anchor will not be editing here because we start building as soon as user clicks, which
				// plops down a start anchor right away
				if (EndAnchor.IsEditing)
					Anchor45(StartAnchor, dataPoint, chartControl, chartPanel, chartScale).CopyDataValues(EndAnchor);
			}
			else if (DrawingState == DrawingState.Editing && editingAnchor != null)
			{
				// if its a line with two anchors, update both x/y at once

					ChartAnchor startAnchor = editingAnchor == StartAnchor ? EndAnchor : StartAnchor;
					Anchor45(startAnchor, dataPoint, chartControl, chartPanel, chartScale).CopyDataValues(editingAnchor);
				

					// horizontal line only needs Y value updated
					editingAnchor.Price = dataPoint.Price;
					EndAnchor.Price		= dataPoint.Price;
				

			}
			else if (DrawingState == DrawingState.Moving)
				foreach (ChartAnchor anchor in Anchors)
					// only move anchor values as needed depending on line type

						anchor.MoveAnchor(InitialMouseDownAnchor, dataPoint, chartControl, chartPanel, chartScale, this);
			//lastMouseMovePoint.Value, point, chartControl, chartScale);
		}

		public override void OnMouseUp(ChartControl chartControl, ChartPanel chartPanel, ChartScale chartScale, ChartAnchor dataPoint)
		{
			// simply end whatever moving
			if (DrawingState is DrawingState.Moving or DrawingState.Editing)
				DrawingState = DrawingState.Normal;
			if (editingAnchor != null)
				editingAnchor.IsEditing = false;
			editingAnchor = null;
		}

		public override void OnRender(ChartControl chartControl, ChartScale chartScale)
		{
			if (Stroke == null)
				return;
						
			Stroke.RenderTarget									= RenderTarget;

			SharpDX.Direct2D1.AntialiasMode	oldAntiAliasMode	= RenderTarget.AntialiasMode;
			RenderTarget.AntialiasMode							= SharpDX.Direct2D1.AntialiasMode.PerPrimitive;
			ChartPanel						panel				= chartControl.ChartPanels[chartScale.PanelIndex];

			if (!IsLocked  && cursorAnchor != null)
			{
				if (EndAnchor.IsEditing)
					Anchor45(StartAnchor, cursorAnchor, chartControl, panel, chartScale).CopyDataValues(EndAnchor);
				else if (DrawingState == DrawingState.Editing && StartAnchor.IsEditing)
				{
					ChartAnchor startAnchor = editingAnchor == StartAnchor ? EndAnchor : StartAnchor;
					Anchor45(startAnchor, cursorAnchor, chartControl, panel, chartScale).CopyDataValues(editingAnchor);
				}
			}

			Point							startPoint			= StartAnchor.GetPoint(chartControl, panel, chartScale);

			// align to full pixel to avoid unneeded aliasing
			double							strokePixAdj		= ((double)(Stroke.Width % 2)).ApproxCompare(0) == 0 ? 0.5d : 0d;
			Vector							pixelAdjustVec		= new(strokePixAdj, strokePixAdj);



			Point					endPoint			= EndAnchor.GetPoint(chartControl, panel, chartScale);

			// convert our start / end pixel points to directx 2d vectors
			Point					endPointAdjusted	= endPoint + pixelAdjustVec;
			SharpDX.Vector2			endVec				= endPointAdjusted.ToVector2();
			Point					startPointAdjusted	= startPoint + pixelAdjustVec;
			SharpDX.Vector2			startVec			= startPointAdjusted.ToVector2();
			SharpDX.Direct2D1.Brush	tmpBrush			= IsInHitTest ? chartControl.SelectionBrush : Stroke.BrushDX;

			RenderTarget.DrawLine(startVec, endVec, tmpBrush, Stroke.Width, Stroke.StrokeStyle);

            // draw text if present
            if (!string.IsNullOrWhiteSpace(CustomText))
            {
                // choose anchor to attach text to
                Point textRefPoint = TextAnchor == TextAnchorPosition.First ? startPoint : endPoint;

                SimpleFont wpfFont = chartControl.Properties.LabelFont ?? new SimpleFont();
                SharpDX.DirectWrite.TextFormat textFormat = wpfFont.ToDirectWriteTextFormat();
                textFormat.TextAlignment = SharpDX.DirectWrite.TextAlignment.Leading;
                textFormat.WordWrapping = SharpDX.DirectWrite.WordWrapping.NoWrap;

                using var textLayout = new SharpDX.DirectWrite.TextLayout(Core.Globals.DirectWriteFactory, CustomText, textFormat, panel.W, textFormat.FontSize);

                // small offset so text does not overlap the anchor exactly
                float offsetX = 4f;
                float offsetY = -(textLayout.Metrics.Height / 2f);

                RenderTarget.DrawTextLayout(
                    new SharpDX.Vector2((float)textRefPoint.X + offsetX, (float)textRefPoint.Y + offsetY),
                    textLayout,
                    Stroke.BrushDX,
                    SharpDX.Direct2D1.DrawTextOptions.NoSnap);
            }




            return;
			


				Point minPoint = startPointAdjusted;
				Point maxPoint = GetExtendedPoint(chartControl, panel, chartScale, StartAnchor, EndAnchor);//GetExtendedPoint(startPoint, endPoint); //

				RenderTarget.DrawLine(minPoint.ToVector2(), maxPoint.ToVector2(), tmpBrush, Stroke.Width, Stroke.StrokeStyle);
			
		
			RenderTarget.AntialiasMode	= oldAntiAliasMode;
		}

		protected override void OnStateChange()
		{
			if (State == State.SetDefaults)
			{
				LineType					= ChartLineType.TextLine;
				Name						= "TextLine";
				DrawingState				= DrawingState.Building;

				EndAnchor					= new ChartAnchor
				{
					IsEditing		= true,
					DrawingTool		= this,
					DisplayName		= Custom.Resource.NinjaScriptDrawingToolAnchorEnd,
					IsBrowsable		= true
				};

				StartAnchor			= new ChartAnchor
				{
					IsEditing		= true,
					DrawingTool		= this,
					DisplayName		= Custom.Resource.NinjaScriptDrawingToolAnchorStart,
					IsBrowsable		= true
				};

				// a normal line with both end points has two anchors
				Stroke						= new Stroke(Brushes.CornflowerBlue, 2f);
			}
			else if (State == State.Terminated)
			{
				// release any device resources
				Dispose();
			}
		}
	}




	public static partial class LetsDraw
	{
		private static T DrawLineTypeCore<T>(NinjaScriptBase owner, bool isAutoScale, string tag,
										int startBarsAgo, DateTime startTime, double startY, int endBarsAgo, DateTime endTime, double endY,
										Brush brush, DashStyleHelper dashStyle, int width, bool isGlobal, string templateName, string CustomText) where T : TextLine
		{
			if (owner == null)
				throw new ArgumentException("owner");

			if (string.IsNullOrWhiteSpace(tag))
				throw new ArgumentException(@"tag cant be null or empty", nameof(tag));

			if (isGlobal && tag[0] != GlobalDrawingToolManager.GlobalDrawingToolTagPrefix)
				tag = $"{GlobalDrawingToolManager.GlobalDrawingToolTagPrefix}{tag}";

			if (DrawingTool.GetByTagOrNew(owner, typeof(T), tag, templateName) is not T lineT)
				return null;


			else if (startTime == Core.Globals.MinDate && endTime == Core.Globals.MinDate && startBarsAgo == int.MinValue && endBarsAgo == int.MinValue)
				throw new ArgumentException("bad start/end date/time");
			else
			{
				if (startTime < Core.Globals.MinDate)
					throw new ArgumentException(lineT + " startTime must be greater than the minimum Date of " + Core.Globals.MinDate + " but was " + startTime);
				if (endTime < Core.Globals.MinDate)
					throw new ArgumentException(lineT + " endTime must be greater than the minimum Date of " + Core.Globals.MinDate + " but was " + endTime);
			}

			DrawingTool.SetDrawingToolCommonValues(lineT, tag, isAutoScale, owner, isGlobal);

            // set custom text
            if (!string.IsNullOrEmpty(CustomText))
                lineT.CustomText = CustomText;

            // dont nuke existing anchor refs on the instance
            ChartAnchor startAnchor;

				startAnchor				= DrawingTool.CreateChartAnchor(owner, startBarsAgo, startTime, startY);
				ChartAnchor endAnchor	= DrawingTool.CreateChartAnchor(owner, endBarsAgo, endTime, endY);
				startAnchor.CopyDataValues(lineT.StartAnchor);
				endAnchor.CopyDataValues(lineT.EndAnchor);
			

			if (brush != null)
				lineT.Stroke = new Stroke(brush, dashStyle, width) { RenderTarget = lineT.Stroke.RenderTarget };

			lineT.SetState(State.Active);
			return lineT;
		}


		// line overloads
		private static TextLine TextLine(NinjaScriptBase owner, bool isAutoScale, string tag,
								int startBarsAgo, DateTime startTime, double startY, int endBarsAgo, DateTime endTime, double endY,
								Brush brush, DashStyleHelper dashStyle, int width, string CustomText) =>
			DrawLineTypeCore<TextLine>(owner, isAutoScale, tag, startBarsAgo, startTime, startY, endBarsAgo, endTime, endY, brush, dashStyle, width, false, null, CustomText);

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="startBarsAgo">The starting bar (x axis coordinate) where the draw object will be drawn. For example, a value of 10 would paint the draw object 10 bars back.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endBarsAgo">The end bar (x axis coordinate) where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, int startBarsAgo, double startY, int endBarsAgo, double endY, Brush brush, string CustomText) => 
			TextLine(owner, false, tag, startBarsAgo, Core.Globals.MinDate, startY, endBarsAgo, Core.Globals.MinDate, endY, brush, DashStyleHelper.Solid, 1, CustomText);

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startBarsAgo">The starting bar (x axis coordinate) where the draw object will be drawn. For example, a value of 10 would paint the draw object 10 bars back.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endBarsAgo">The end bar (x axis coordinate) where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="dashStyle">The dash style used for the lines of the object.</param>
		/// <param name="width">The width of the draw object</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, int startBarsAgo, double startY, int endBarsAgo,
			double endY, Brush brush, DashStyleHelper dashStyle, int width, string CustomText) =>
			TextLine(owner, isAutoScale, tag, startBarsAgo, Core.Globals.MinDate, startY, endBarsAgo, Core.Globals.MinDate, endY, brush, dashStyle, width, CustomText);

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startTime">The starting time where the draw object will be drawn.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endTime">The end time where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="dashStyle">The dash style used for the lines of the object.</param>
		/// <param name="width">The width of the draw object</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime startTime, double startY, DateTime endTime,
			double endY, Brush brush, DashStyleHelper dashStyle, int width, string CustomText) =>
			TextLine(owner, isAutoScale, tag, int.MinValue, startTime, startY, int.MinValue, endTime, endY, brush, dashStyle, width, CustomText);

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startBarsAgo">The starting bar (x axis coordinate) where the draw object will be drawn. For example, a value of 10 would paint the draw object 10 bars back.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endBarsAgo">The end bar (x axis coordinate) where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="dashStyle">The dash style used for the lines of the object.</param>
		/// <param name="width">The width of the draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, int startBarsAgo, double startY, int endBarsAgo,
			double endY, Brush brush, DashStyleHelper dashStyle, int width, bool drawOnPricePanel, string CustomText) =>
			DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () =>
				TextLine(owner, isAutoScale, tag, startBarsAgo, Core.Globals.MinDate, startY, endBarsAgo, Core.Globals.MinDate, endY, brush, dashStyle, width, CustomText));

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startTime">The starting time where the draw object will be drawn.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endTime">The end time where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="brush">The brush used to color draw object</param>
		/// <param name="dashStyle">The dash style used for the lines of the object.</param>
		/// <param name="width">The width of the draw object</param>
		/// <param name="drawOnPricePanel">Determines if the draw-object should be on the price panel or a separate panel</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime startTime, double startY, DateTime endTime,
			double endY, Brush brush, DashStyleHelper dashStyle, int width, bool drawOnPricePanel, string CustomText) =>
			DrawingTool.DrawToggledPricePanel(owner, drawOnPricePanel, () =>
				TextLine(owner, isAutoScale, tag, int.MinValue, startTime, startY, int.MinValue, endTime, endY, brush, dashStyle, width, CustomText));

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startTime">The starting time where the draw object will be drawn.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endTime">The end time where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime startTime, double startY, DateTime endTime,
			double endY, string templateName, string CustomText) =>
			DrawLineTypeCore<TextLine>(owner, isAutoScale, tag, int.MinValue, startTime, startY, int.MinValue, endTime, endY,
				null, DashStyleHelper.Dash, 0, false, templateName, CustomText);

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startBarsAgo">The starting bar (x axis coordinate) where the draw object will be drawn. For example, a value of 10 would paint the draw object 10 bars back.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endBarsAgo">The end bar (x axis coordinate) where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, int startBarsAgo, double startY, int endBarsAgo,
			double endY, string templateName, string CustomText) =>
			DrawLineTypeCore<TextLine>(owner, isAutoScale, tag, startBarsAgo, Core.Globals.MinDate, startY, endBarsAgo, Core.Globals.MinDate, endY,
				null, DashStyleHelper.Dash, 0, false, templateName, CustomText);

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startBarsAgo">The starting bar (x axis coordinate) where the draw object will be drawn. For example, a value of 10 would paint the draw object 10 bars back.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endBarsAgo">The end bar (x axis coordinate) where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, int startBarsAgo, double startY, int endBarsAgo,
			double endY, bool isGlobal, string templateName, string CustomText) =>
			DrawLineTypeCore<TextLine>(owner, isAutoScale, tag, startBarsAgo, Core.Globals.MinDate, startY, endBarsAgo, Core.Globals.MinDate, endY,
				null, DashStyleHelper.Solid, 0, isGlobal, templateName, CustomText);

		/// <summary>
		/// Draws a line between two points.
		/// </summary>
		/// <param name="owner">The hosting NinjaScript object which is calling the draw method</param>
		/// <param name="tag">A user defined unique id used to reference the draw object</param>
		/// <param name="isAutoScale">Determines if the draw object will be included in the y-axis scale</param>
		/// <param name="startTime">The starting time where the draw object will be drawn.</param>
		/// <param name="startY">The starting y value coordinate where the draw object will be drawn</param>
		/// <param name="endTime">The end time where the draw object will terminate</param>
		/// <param name="endY">The end y value coordinate where the draw object will terminate</param>
		/// <param name="isGlobal">Determines if the draw object will be global across all charts which match the instrument</param>
		/// <param name="templateName">The name of the drawing tool template the object will use to determine various visual properties</param>
		/// <returns></returns>
		public static TextLine TextLine(NinjaScriptBase owner, string tag, bool isAutoScale, DateTime startTime, double startY, DateTime endTime,
			double endY, bool isGlobal, string templateName, string CustomText) =>
			DrawLineTypeCore<TextLine>(owner, isAutoScale, tag, int.MinValue, startTime, startY, int.MinValue, endTime, endY,
				null, DashStyleHelper.Solid, 0, isGlobal, templateName, CustomText);


	}
}
