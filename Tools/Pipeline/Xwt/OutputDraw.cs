using System;
using Xwt;
using Xwt.Drawing;

namespace MonoGame.Tools.Pipeline
{
	public class OutputDraw : Canvas, IOutput
	{
		System.Text.StringBuilder _builder;
		ScrollAdjustment _xscroll;

		double _textHeight;

		public OutputDraw ()
		{
			BackgroundColor = Colors.Transparent;
			ExpandHorizontal = true;
			ExpandVertical = true;

			_builder = new System.Text.StringBuilder ();
			_textHeight = 0.0;
		}

		protected override bool SupportsCustomScrolling {
			get {
				return true;
			}
		}

		protected override void SetScrollAdjustments (ScrollAdjustment horizontal, ScrollAdjustment vertical)
		{
			_xscroll = vertical;
			//_xscroll.UpperValue = 1000;
			_xscroll.ValueChanged += (object sender, EventArgs e) => QueueDraw();
		}

		protected override void OnBoundsChanged ()
		{
			if (_xscroll == null)
				return;
			_xscroll.PageSize = _xscroll.PageIncrement = Bounds.Height;
		}

		public void Append (string text) {
			_builder.Append (text);
		}

		public new void Clear() {
			base.Clear ();
			_builder.Clear ();
		}

		protected override void OnDraw (Context ctx, Rectangle dirtyRect)
		{
			ctx.Save ();
			base.OnDraw (ctx, dirtyRect);
			ctx.Restore ();

			ctx.Translate (0, -_xscroll.Value);

			var lay = new TextLayout (this);

			lay.Text = _builder.ToString ();
			lay.Width = Bounds.Width - 20;

			ctx.DrawTextLayout (lay, 10, 10);
			double textHeight = lay.GetSize ().Height + 10;
			if (_xscroll.UpperValue != textHeight) {
				_xscroll.UpperValue = textHeight;
				System.Diagnostics.Debug.WriteLine (_xscroll.UpperValue);
			}
		}
	}
}

