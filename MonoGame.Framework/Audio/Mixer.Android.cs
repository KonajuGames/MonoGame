// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using Android.Media;
using Android.Content.PM;
using Android.Content;
using System.Globalization;

namespace Microsoft.Xna.Framework.Audio
{
    /// <summary>
    /// Platform-specific portions of the custom audio mixer.
    /// </summary>
    static partial class Mixer
    {
        static AudioTrack _audioTrack;

        static int _bufferSizeInFrames;
        static int _sampleRate;
        static int _updateBuffers;

        static int PlatformBufferSizeInFrames { get { return _bufferSizeInFrames; } }
        static int PlatformSampleRate { get { return _sampleRate; } }
        static int PlatformUpdateBuffers { get { return _updateBuffers; } }

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
            _sampleRate = 44100;
            _bufferSizeInFrames = 4096;
            _updateBuffers = 2;
            if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.JellyBeanMr1)
            {
                Android.Util.Log.Debug("Mixer", Game.Activity.PackageManager.HasSystemFeature(PackageManager.FeatureAudioLowLatency) ? "Supports low latency audio playback." : "Does not support low latency audio playback.");
                var audioManager = Game.Activity.GetSystemService(Context.AudioService) as AudioManager;
                if (audioManager != null)
                {
                    var result = audioManager.GetProperty(AudioManager.PropertyOutputSampleRate);
                    if (!string.IsNullOrEmpty(result))
                        _sampleRate = int.Parse(result, CultureInfo.InvariantCulture);
                    result = audioManager.GetProperty(AudioManager.PropertyOutputFramesPerBuffer);
                    if (!string.IsNullOrEmpty(result))
                        _bufferSizeInFrames = int.Parse(result, CultureInfo.InvariantCulture);
                }

                // If 4.4 or higher, then we don't need to double buffer on the application side.
                // See http://stackoverflow.com/a/15006327
                if (Android.OS.Build.VERSION.SdkInt >= Android.OS.BuildVersionCodes.Kitkat)
                    _updateBuffers = 1;

            }

            var bufferSizeInBytes = _bufferSizeInFrames * sizeof(short) * 2;
            Android.Util.Log.Debug("Mixer", "Mixer starting with buffer of {0} stereo samples. Native sample rate {1}", _bufferSizeInFrames, _sampleRate);
            _audioTrack = new AudioTrack(Android.Media.Stream.Music, _sampleRate, ChannelOut.Stereo, Encoding.Pcm16bit, bufferSizeInBytes, AudioTrackMode.Stream);
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