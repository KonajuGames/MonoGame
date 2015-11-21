// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Utilities;
using System;

namespace Microsoft.Xna.Framework.Audio
{
    public partial class SoundEffectInstance : IDisposable
    {
        internal bool _isLooped;
        // The true value of IsLooped, as _isLooped is set to false when
        // Stop(false) is called to allow the sound to play out.
        bool _isLoopedCopy;

        // A 32:32 fixed point position used in the mixer
        internal Fix64 _position;
        // A 32:32 fixed point step used for resampling in the mixer
        internal Fix64 _step;

        internal SoundState _state = SoundState.Stopped;
        internal bool _hasLooped;

        internal void PlatformInitialize(byte[] buffer, int sampleRate, int channels)
        {
        }

        private void PlatformApply3D(AudioListener listener, AudioEmitter emitter)
        {
        }

        private void PlatformPause()
        {
            if (_state == SoundState.Playing)
                _state = SoundState.Paused;
        }

        Fix64 CalculateStep()
        {
            int mixerRate = Mixer.SampleRate;
            int effectRate = _effect._sampleRate;
            if (mixerRate == effectRate && _pitch == 0.0f)
                return Fix64.One;
            return new Fix64(((double)effectRate * Math.Pow(2, _pitch)) / (double)mixerRate);
        }

        private void PlatformPlay()
        {
            if (_state == SoundState.Stopped)
            {
                _position = Fix64.Zero;
                _step = CalculateStep();
                _hasLooped = false;
                _isLooped = _isLoopedCopy;
            }
            _state = SoundState.Playing;
        }

        private void PlatformResume()
        {
            if (_state == SoundState.Paused)
                _state = SoundState.Playing;
        }

        private void PlatformStop(bool immediate)
        {
            if (immediate)
                // Stop the sound immediately
                _state = SoundState.Stopped;
            else
                // Turn off looping and allow the sound to finish as authored
                _isLooped = false;
        }

        private void PlatformSetIsLooped(bool value)
        {
            if (_state != SoundState.Stopped)
                throw new InvalidOperationException("Cannot set IsLooped while SoundEffectInstance is not stopped");
            _isLooped = _isLoopedCopy = value;
        }

        private bool PlatformGetIsLooped()
        {
            return _isLoopedCopy;
        }

        private void PlatformSetPan(float value)
        {
        }

        private void PlatformSetPitch(float value)
        {
            if (_effect != null)
                _step = CalculateStep();
        }

        private SoundState PlatformGetState()
        {
            return _state;
        }

        private void PlatformSetVolume(float value)
        {
        }

        private void PlatformDispose(bool disposing)
        {
        }
    }
}
