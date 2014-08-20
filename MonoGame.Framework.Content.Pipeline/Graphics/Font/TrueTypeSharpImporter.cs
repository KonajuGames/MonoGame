// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Drawing;
using TrueTypeSharp;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
    internal class TrueTypeSharpImporter : IFontImporter
	{
        #region IFontImporter implementation

        public void Import(FontDescription options, string fontName)
        {
            var font = new TrueTypeFont(fontName);

            // Convert points to pixels as pointSize * 96 / 72
            float pixelHeight = options.Size * 96.0f / 72.0f;
            float scale = font.GetScaleForMappingEmToPixels(pixelHeight);

            int lineAscenderI, lineDescenderI, lineGapI;
            font.GetFontVMetrics(out lineAscenderI, out lineDescenderI, out lineGapI);
            float lineAscender = lineAscenderI * scale;
            float lineDescender = lineDescenderI * scale;
            float lineGap = lineGapI * scale;

            YOffsetMin = -(int)Math.Round(lineAscender);

            var glyphList = new List<Glyph>();
            // Rasterize each character in turn.
            foreach (char ch in options.Characters)
            {
                int width, height, xOffset, yOffset;

                int index = font.FindGlyphIndex(ch);
                byte[] data = font.GetGlyphBitmap(index, scale, scale, out width, out height, out xOffset, out yOffset);
                if (data.Length == 0 || width == 0 || height == 0)
                {
                    data = new byte[] { 0 };
                    width = 1;
                    height = 1;
                }

                // Create the bitmap from the byte array
                Bitmap bitmap = new Bitmap(width, height);
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte opacity = data[y * width + x];
                        // Set pixel in pre-multiplied format
                        bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(opacity, opacity, opacity, opacity));
                    }
                }

                int advanceWidthI, leftSideBearingI;
                font.GetGlyphHMetrics(index, out advanceWidthI, out leftSideBearingI);
                float advanceWidth = advanceWidthI * scale;
                float leftSideBearing = leftSideBearingI * scale;

                // not sure about this at all
                var abc = new ABCFloat();
                abc.A = leftSideBearing;
                abc.B = width;
                abc.C = advanceWidth - (abc.A + abc.B);

                // Construct the output Glyph object.
                var glyph = new Glyph(ch, bitmap)
                {
                    XOffset = xOffset,
                    XAdvance = advanceWidth,
                    YOffset = yOffset,
                    CharacterWidths = abc
                };

                glyphList.Add(glyph);
            }
            Glyphs = glyphList;

            // Store the font height.
            LineSpacing = lineAscender + -lineDescender + lineGap;
        }

        public IEnumerable<Glyph> Glyphs { get; private set; }

        public float LineSpacing { get; private set; }

        public int YOffsetMin { get; private set; }

        #endregion
	}
}

