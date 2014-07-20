using System;
using System.Text;
using Xwt;
using Xwt.Drawing;

namespace MonoGame.Tools.Pipeline
{
	class OutputXwt : TextEntry, IOutput
    {
        private StringBuilder _textBuilder;

        public OutputXwt()
            : base()
        {
            ReadOnly = true;
			ShowFrame = false;
            MultiLine = true;
			//HeightRequest = (double)500;
            ExpandHorizontal = true;
			//ExpandVertical = true;
			TextAlignment = Alignment.Start;

			var faces = new[] { "Consolas", "Lucida Console", "Courier New", "Monospace" };
            for (var f = 0; f < faces.Length; f++)
            {
                this.Font = Font.FromName(faces[f]).WithScaledSize(0.9).WithStyle(FontStyle.Normal);
                if (this.Font.Family == faces[f])
                    break;
            }

            _textBuilder = new StringBuilder();
        }

        void updateText()
        {
            this.Text = _textBuilder.ToString();
        }

        public void Append(string text)
        {
            _textBuilder.AppendLine(text);
            updateText();
        }

        public void Clear()
        {
            _textBuilder.Clear();
            updateText();
        }
    }
}
