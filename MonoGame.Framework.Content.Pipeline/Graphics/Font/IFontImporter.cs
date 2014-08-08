// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Content.Pipeline.Graphics
{
	// Importer interface allows the conversion tool to support multiple source font formats.
	internal interface IFontImporter
	{
		void Import(FontDescription options, string fontName);

		IEnumerable<Glyph> Glyphs { get; }

		float LineSpacing { get; }

		int YOffsetMin { get; }
	}
}
