// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
#if WINDOWS
using NAudio.Wave;
#endif
using System.IO;

namespace Microsoft.Xna.Framework.Content.Pipeline.Audio
{
    /// <summary>
    /// Encapsulates the native audio format (WAVEFORMATEX) information of the audio content.
    /// </summary>
    public sealed class AudioFormat
    {
        int _averageBytesPerSecond;
        int _bitsPerSample;
        int _blockAlign;
        int _channelCount;
        int _format;
        internal List<byte> _nativeWaveFormat;
        int _sampleRate;

        /// <summary>
        /// Gets the average bytes processed per second.
        /// </summary>
        /// <value>Average bytes processed per second.</value>
        public int AverageBytesPerSecond { get { return _averageBytesPerSecond; } }

        /// <summary>
        /// Gets the bit depth of the audio content.
        /// </summary>
        /// <value>If the audio has not been processed, the source bit depth; otherwise, the bit depth of the new format.</value>
        public int BitsPerSample { get { return _bitsPerSample; } }

        /// <summary>
        /// Gets the number of bytes per sample block, taking channels into consideration. For example, for 16-bit stereo audio (PCM format), the size of each sample block is 4 bytes.
        /// </summary>
        /// <value>Number of bytes, per sample block.</value>
        public int BlockAlign { get { return _blockAlign; } }

        /// <summary>
        /// Gets the number of channels.
        /// </summary>
        /// <value>If the audio has not been processed, the source channel count; otherwise, the new channel count.</value>
        public int ChannelCount { get { return _channelCount; } }

        /// <summary>
        /// Gets the format of the audio content.
        /// </summary>
        /// <value>If the audio has not been processed, the format tag of the source content; otherwise, the new format tag.</value>
        public int Format { get { return _format; } }

        /// <summary>
        /// Gets the raw byte buffer for the format. For non-PCM formats, this buffer contains important format-specific information beyond the basic format information exposed in other properties of the AudioFormat type.
        /// </summary>
        /// <value>The raw byte buffer represented in a collection.</value>
        public ReadOnlyCollection<byte> NativeWaveFormat { get { return _nativeWaveFormat.AsReadOnly(); } }

        /// <summary>
        /// Gets the sample rate of the audio content.
        /// </summary>
        /// <value>If the audio has not been processed, the source sample rate; otherwise, the new sample rate.</value>
        public int SampleRate { get { return _sampleRate; } }

#if WINDOWS
        /// <summary>
        /// Creates a new instance of the AudioFormat class
        /// </summary>
        /// <param name="waveFormat">The WaveFormat representing the WAV header.</param>
        internal AudioFormat(WaveFormat waveFormat)
        {
            _averageBytesPerSecond = waveFormat.AverageBytesPerSecond;
            _bitsPerSample = waveFormat.BitsPerSample;
            _blockAlign = waveFormat.BlockAlign;
            _channelCount = waveFormat.Channels;
            _format = (int)waveFormat.Encoding;
            _sampleRate = waveFormat.SampleRate;

            var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream))
            {
                waveFormat.Serialize(writer);
                _nativeWaveFormat = new List<byte>(stream.ToArray());
            }
        }
#endif

        internal AudioFormat(byte[] waveFormat, int bitsPerSample, int blockAlign, int channels, int encoding, int sampleRate)
        {
            _nativeWaveFormat = new List<byte>(waveFormat);
            _bitsPerSample = bitsPerSample;
            _blockAlign = blockAlign;
            _channelCount = channels;
            _format = encoding;
            _sampleRate = sampleRate;
        }
    }
}
