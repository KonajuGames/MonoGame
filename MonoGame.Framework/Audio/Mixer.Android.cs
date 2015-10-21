// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Android.Media;

namespace Microsoft.Xna.Framework.Audio
{
    /// <summary>
    /// Platform-specific portions of the custom audio mixer.
    /// </summary>
    static partial class Mixer
    {
        static AudioTrack _audioTrack;

        static int _bufferSizeInShorts;
        static int _nativeSampleRate;

        static int PlatformBufferSizeInShorts { get { return _bufferSizeInShorts; } }
        static int PlatformNativeSampleRate { get { return _nativeSampleRate; } }

        static void PlatformInit()
        {
            AndroidGameActivity.Paused += AndroidGameActivity_Paused;
            AndroidGameActivity.Resumed += AndroidGameActivity_Resumed;
        }

        static void PlatformDeinit()
        {
            AndroidGameActivity.Paused -= AndroidGameActivity_Paused;
            AndroidGameActivity.Resumed -= AndroidGameActivity_Resumed;
        }

        static void AndroidGameActivity_Paused(object sender, EventArgs e)
        {
            Mixer.Pause();
        }

        static void AndroidGameActivity_Resumed(object sender, EventArgs e)
        {
            Mixer.Resume();
        }

        /// <summary>
        /// The platform-specific portion of starting the mixer thread.
        /// </summary>
        static void PlatformStart()
        {
            _nativeSampleRate = AudioTrack.GetNativeOutputSampleRate(Android.Media.Stream.Music);
            var bufferSizeInBytes = AudioTrack.GetMinBufferSize(_nativeSampleRate, ChannelOut.Stereo, Encoding.Pcm16bit);
            _bufferSizeInShorts = bufferSizeInBytes / sizeof(short);
            Android.Util.Log.Debug("Mixer", "Mixer starting with buffer of {0} stereo samples. Native sample rate {1}", _bufferSizeInShorts, _nativeSampleRate);
            _audioTrack = new AudioTrack(Android.Media.Stream.Music, _nativeSampleRate, ChannelOut.Stereo, Encoding.Pcm16bit, bufferSizeInBytes, AudioTrackMode.Stream);
            _audioTrack.Play();
        }

        /// <summary>
        /// The platform-specific portion of stopping the mixer thread.
        /// </summary>
        static void PlatformStop()
        {
            if (_audioTrack != null)
            {
                Android.Util.Log.Debug("Mixer", "Mixer terminating");
                _audioTrack.Pause();
                _audioTrack.Flush();
                _audioTrack.Release();
                _audioTrack.Dispose();
                _audioTrack = null;
            }
        }

        /// <summary>
        /// The platform-specific portion of submitting the audio buffer from the mixer thread.
        /// </summary>
        static void PlatformSubmitBuffer(short[] buffer)
        {
            _audioTrack.Write(buffer, 0, buffer.Length);
        }
    }
}