// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Microsoft.Xna.Framework.Content.Pipeline.Audio;

namespace Microsoft.Xna.Framework.Content.Pipeline
{
    /// <summary>
    /// Provides methods for reading .wav audio files for use in the Content Pipeline.
    /// </summary>
    [ContentImporter(".aac,.mp4,.m4a", DisplayName = "Aac Importer - MonoGame", DefaultProcessor = "SongProcessor")]
    public class AacImporter : ContentImporter<AudioContent>
    {
        /// <summary>
        /// Called by the content pipeline when importing a AAC audio file to be used as a game asset.
        /// </summary>
        /// <param name="filename">Name of a game asset file.</param>
        /// <param name="context">Contains information for importing a game asset, such as a logger interface.</param>
        /// <returns>Resulting game asset.</returns>
        public override AudioContent Import(string filename, ContentImporterContext context)
        {
            var content = new AudioContent(filename, AudioFileType.Aac);
            return content;
        }
    }
}
