// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.ComponentModel;
using Microsoft.Xna.Framework.Content.Pipeline.Graphics;

namespace Microsoft.Xna.Framework.Content.Pipeline.Processors
{
    /// <summary>
    /// Processes textures into a format for easy loading on the target platform.
    /// </summary>
    [ContentProcessor(DisplayName = "Texture - MonoGame")]
    public class TextureProcessor : ContentProcessor<TextureContent, TextureContent>
    {
        Color _colorKeyColor = Color.Magenta;
        /// <summary>
        /// Pixels matching this color are converted to transparent black if ColorKeyEnabled is true.
        /// </summary>
        [DefaultValueAttribute(typeof(Color), "255, 0, 255, 255")]
        public virtual Color ColorKeyColor { get { return _colorKeyColor; } set { _colorKeyColor = value; } }

        bool _colorKeyEnabled = true;
        /// <summary>
        /// If enabled, pixels matching ColorKeyColor are converted to transparent black.
        /// </summary>
        [DefaultValueAttribute(true)]
        public virtual bool ColorKeyEnabled { get { return _colorKeyEnabled; } set { _colorKeyEnabled = value; } }

        /// <summary>
        /// Generates all mipmap levels for the texture.
        /// </summary>
        public virtual bool GenerateMipmaps { get; set; }

        bool _premultiplyAlpha = true;
        /// <summary>
        /// Convertes the texture to a pre-multipled alpha format.
        /// </summary>
        [DefaultValueAttribute(true)]
        public virtual bool PremultiplyAlpha { get { return _premultiplyAlpha; } set { _premultiplyAlpha = value; } }

        /// <summary>
        /// Resizes the texture if required so both dimensions are a power of two.
        /// </summary>
        public virtual bool ResizeToPowerOfTwo { get; set; }

        /// <summary>
        /// The format of the exported texture.
        /// </summary>
        public virtual TextureProcessorOutputFormat TextureFormat { get; set; }

        /// <summary>
        /// Prcoess the input texture to a format suitable for loading on the target platform.
        /// </summary>
        /// <param name="input">The texture to process.</param>
        /// <param name="context">The context of the processor.</param>
        /// <returns>The processed texture.</returns>
        public override TextureContent Process(TextureContent input, ContentProcessorContext context)
        {
            if (ColorKeyEnabled)
            {
                var replaceColor = System.Drawing.Color.FromArgb(0);
                for (var x = 0; x < input._bitmap.Width; x++)
                {
                    for (var y = 0; y < input._bitmap.Height; y++)
                    {
                        var col = input._bitmap.GetPixel(x, y);

                        if (col.ColorsEqual(ColorKeyColor))
                        {
                            input._bitmap.SetPixel(x, y, replaceColor);
                        }
                    }
                }
            }

            var face = input.Faces[0][0];
            if (ResizeToPowerOfTwo)
            {
                if (!GraphicsUtil.IsPowerOfTwo(face.Width) || !GraphicsUtil.IsPowerOfTwo(face.Height))
                    input.Resize(GraphicsUtil.GetNextPowerOfTwo(face.Width), GraphicsUtil.GetNextPowerOfTwo(face.Height));
            }

            if (PremultiplyAlpha)
            {
                for (var x = 0; x < input._bitmap.Width; x++)
                {
                    for (var y = 0; y < input._bitmap.Height; y++)
                    {
                        var oldCol = input._bitmap.GetPixel(x, y);
                        var preMultipliedColor = Color.FromNonPremultiplied(oldCol.R, oldCol.G, oldCol.B, oldCol.A);
                        input._bitmap.SetPixel(x, y, System.Drawing.Color.FromArgb(preMultipliedColor.A, 
                                                                                   preMultipliedColor.R,
                                                                                   preMultipliedColor.G,
                                                                                   preMultipliedColor.B));
                    }
                }
            }

            // Set the first layer
            input.Faces[0][0].SetPixelData(input._bitmap.GetData());

            if (TextureFormat == TextureProcessorOutputFormat.NoChange)
                return input;
			try 
			{
			if (TextureFormat == TextureProcessorOutputFormat.DXTCompressed || 
                TextureFormat == TextureProcessorOutputFormat.Compressed ) {
                	context.Logger.LogMessage("Compressing using {0}",TextureFormat);
                	GraphicsUtil.CompressTexture(input, context, GenerateMipmaps, PremultiplyAlpha);
					context.Logger.LogMessage("Compression {0} Suceeded", TextureFormat);
				}
			}
			catch(EntryPointNotFoundException ex) {
				context.Logger.LogImportantMessage ("Could not find the entry point to compress the texture", ex.ToString());
				TextureFormat = TextureProcessorOutputFormat.Color;
			}
			catch(DllNotFoundException ex) {
				context.Logger.LogImportantMessage ("Could not compress texture. Required shared lib is missing. {0}", ex.ToString());
				TextureFormat = TextureProcessorOutputFormat.Color;
			}
			catch(Exception ex)
			{
				context.Logger.LogImportantMessage ("Could not compress texture {0}", ex.ToString());
				TextureFormat = TextureProcessorOutputFormat.Color;
			}

            return input;
        }


    }
}
