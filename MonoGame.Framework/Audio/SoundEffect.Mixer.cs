// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.
﻿
using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using MonoGame.Utilities;

namespace Microsoft.Xna.Framework.Audio
{
    public sealed partial class SoundEffect : IDisposable
    {
        internal short[] _data;
        internal int _sampleRate;
        internal AudioChannels _channels;
        internal int _frames;
        internal int _loopStart;
        internal int _loopLength;

        private void PlatformLoadAudioStream(Stream s)
        {
        }

        private void PlatformInitialize(byte[] buffer, int sampleRate, AudioChannels channels)
        {
            int bytesPerFrame = channels == AudioChannels.Mono ? 2 : 4;
            PlatformInitialize(buffer, 0, buffer.Length, sampleRate, channels, 0, buffer.Length / bytesPerFrame);
        }
        
        private void PlatformInitialize(byte[] buffer, int offset, int count, int sampleRate, AudioChannels channels, int loopStart, int loopLength)
        {
            int sampleCount = count / 2;
            _data = new short[sampleCount];
            Buffer.BlockCopy(buffer, offset, _data, 0, count);
            _sampleRate = sampleRate;
            _channels = channels;
            _frames = _data.Length;
            if (channels == AudioChannels.Stereo)
                _frames /= 2;
            _loopStart = loopStart;
            _loopLength = loopLength;
        }
        
        private void PlatformSetupInstance(SoundEffectInstance instance)
        {
            instance._position = Fix64.Zero;
            instance._state = SoundState.Stopped;
        }

        private void PlatformDispose(bool disposing)
        {
        }

        internal static void PlatformShutdown()
        {
        }
    }
}

