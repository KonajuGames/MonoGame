// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using MonoGame.Utilities;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Microsoft.Xna.Framework.Audio
{
    /// <summary>
    /// A custom audio mixer and resampler.
    /// </summary>
    static partial class Mixer
    {
        static Task _mixerTask;
        static CancellationTokenSource _mixerCancellationTokenSource;
        static List<SoundEffectInstance> _playingInstances;
        static List<SoundEffectInstance> _pooledInstances;

        /// <summary>
        /// Gets the mixer sample rate.
        /// </summary>
        internal static int SampleRate
        {
            get
            {
                return PlatformSampleRate;
            }
        }

        static int InterpolationMax = 256;
        static Vector4[] InterpolationCoefficients;

        static Mixer()
        {
            // Populate lookup table for resampling
            InterpolationCoefficients = new Vector4[InterpolationMax];
            for (int i = 0; i < InterpolationMax; ++i)
            {
                double x = (double)i / (double)InterpolationMax;
                InterpolationCoefficients[i] = new Vector4(
                    (float)(x * (-0.5 + x * (1 - 0.5 * x))),
                    (float)(1.0 + x * x * (1.5 * x - 2.5)),
                    (float)(x * (0.5 + x * (2.0 - 1.5 * x))),
                    (float)(0.5 * x * x * (x - 1.0))
                    );
            }
        }

        /// <summary>
        /// Initialise and start the mixer.
        /// </summary>
        /// <param name="playing">The list of currently playing instances.</param>
        /// <param name="pooled">The list of pooled instances.</param>
        internal static void Start(List<SoundEffectInstance> playing, List<SoundEffectInstance> pooled)
        {
            PlatformInit();
            _playingInstances = playing;
            _pooledInstances = pooled;
            Resume();
        }

        /// <summary>
        /// Stop the mixer thread and clean up.
        /// </summary>
        internal static void Stop()
        {
            Pause();
            _playingInstances = null;
            _pooledInstances = null;
            PlatformDeinit();
        }

        /// <summary>
        /// Starts the mixer thread.  Call Pause to stop it.
        /// </summary>
        internal static void Resume()
        {
            if (_mixerCancellationTokenSource == null)
            {
                _mixerCancellationTokenSource = new CancellationTokenSource();
                // Start the mixer thread
                _mixerTask = Task.Factory.StartNew(MixerTask, _mixerCancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            }
        }

        /// <summary>
        /// Stops the mixer thread.  Call Resume to restart it.
        /// </summary>
        internal static void Pause()
        {
            if (_mixerCancellationTokenSource != null)
            {
                // Ask the mixer thread to stop
                _mixerCancellationTokenSource.Cancel();
                // Wait for the thread to stop
                _mixerTask.Wait();
                // Cleanup
                _mixerCancellationTokenSource.Dispose();
                _mixerCancellationTokenSource = null;
                _mixerTask = null;
            }
        }

        /// <summary>
        /// Resamples a portion of the mono SoundEffectInstance to fill the given stereo buffer.
        /// </summary>
        /// <param name="instance">The SoundEffectInstance to resample.</param>
        /// <param name="buffer">The buffer to fill with resampled data.</param>
        /// <returns>The number of frames in the output buffer.</returns>
        static int ResampleMono(SoundEffectInstance instance, float[] buffer)
        {
            int startIndex;
            short startPoint;
            short endPoint1;
            short endPoint2;
            var effect = instance._effect;
            var dspData = effect._data;
            int loopEnd = effect._loopStart + effect._loopLength;
            Fix64 dspPhase = instance._position;
            Fix64 dspPhaseIncr = instance._step;
            UInt32 dspPhaseIndex = dspPhase.Index;
            int dspI = 0;

            // Voice is currently looping?
            var looping = instance._isLooped;

            // Last index before 4th interpolation point must be specially handled
            var endIndex = (looping ? loopEnd - 1 : effect._frames) - 2;

            // Set start index and start point if looped or not
            if (instance._hasLooped)
            {
                startIndex = effect._loopStart;
                // Last point in loop (wrap around)
                startPoint = dspData[loopEnd - 1];
            }
            else
            {
                startIndex = 0;
                // Just duplicate the point
                startPoint = dspData[0];
            }

            // Get points off the end (loop start if looping, duplicate point if end)
            if (looping)
            {
                endPoint1 = dspData[effect._loopStart];
                endPoint2 = dspData[effect._loopStart + 1];
            }
            else
            {
                endPoint1 = dspData[effect._frames - 1];
                endPoint2 = endPoint1;
            }

            while (true)
            {
                dspPhaseIndex = dspPhase.Index;

                // Interpolate first sample point (start or loop start) if needed
                for (; dspPhaseIndex == startIndex && dspI < buffer.Length; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    buffer[dspI] = buffer[dspI + 1] = (coeffs.X * startPoint)
                        + (coeffs.Y * dspData[dspPhaseIndex])
                        + (coeffs.Z * dspData[dspPhaseIndex + 1])
                        + (coeffs.W * dspData[dspPhaseIndex + 2]);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // Interpolate the sequence of sample points
                for (; dspPhaseIndex <= endIndex - 2 && dspI < buffer.Length; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    buffer[dspI] = buffer[dspI + 1] = (coeffs.X * dspData[dspPhaseIndex - 1])
                        + (coeffs.Y * dspData[dspPhaseIndex])
                        + (coeffs.Z * dspData[dspPhaseIndex + 1])
                        + (coeffs.W * dspData[dspPhaseIndex + 2]);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;

                // Interpolate within the 2nd to last point
                for (; dspPhaseIndex <= endIndex - 1 && dspI < buffer.Length; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    buffer[dspI] = buffer[dspI + 1] = (coeffs.X * dspData[dspPhaseIndex - 1])
                        + (coeffs.Y * dspData[dspPhaseIndex])
                        + (coeffs.Z * dspData[dspPhaseIndex + 1])
                        + (coeffs.W * endPoint1);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // Interpolate within the last point
                for (; dspPhaseIndex <= endIndex && dspI < buffer.Length; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    buffer[dspI] = buffer[dspI + 1] = (coeffs.X * dspData[dspPhaseIndex - 1])
                        + (coeffs.Y * dspData[dspPhaseIndex])
                        + (coeffs.Z * endPoint1)
                        + (coeffs.W * endPoint2);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // Break out if not looping (end of sample)
                if (!looping)
                    break;

                // Go back to loop start
                if (dspPhaseIndex > endIndex)
                {
                    dspPhase -= new Fix64(effect._loopLength);

                    if (!instance._hasLooped)
                    {
                        instance._hasLooped = true;
                        startIndex = effect._loopStart;
                        startPoint = dspData[loopEnd - 1];
                    }
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;
            }

            instance._position = dspPhase;

            return dspI >> 1;
        }

        /// <summary>
        /// Resamples a portion of the mono SoundEffectInstance to fill the given stereo buffer.
        /// </summary>
        /// <param name="instance">The SoundEffectInstance to resample.</param>
        /// <param name="buffer">The buffer to fill with resampled data.</param>
        /// <returns>The number of frames in the output buffer.</returns>
        static int ResampleStereo(SoundEffectInstance instance, float[] buffer)
        {
            int frames = 0;
            Fix64 dspPhase = instance._position;
            Fix64 dspPhaseIncr = instance._step;
            ResampleStereoChannel(instance, buffer, 0, ref dspPhase, ref dspPhaseIncr);
            dspPhase = instance._position;
            frames = ResampleStereoChannel(instance, buffer, 1, ref dspPhase, ref dspPhaseIncr);
            instance._position = dspPhase;
            return frames;
        }

        /// <summary>
        /// Resamples a portion of the mono SoundEffectInstance to fill the given stereo buffer.
        /// </summary>
        /// <param name="instance">The SoundEffectInstance to resample.</param>
        /// <param name="buffer">The buffer to fill with resampled data.</param>
        /// <param name="channel">The channel to resample. 0 = left, 1 = right.</param>
        /// <param name="dspPhase">The current position in the sample.</param>
        /// <param name="dspPhaseIncr">The step to use when advancing through the sample.</param>
        /// <returns>The number of frames in the output buffer.</returns>
        static int ResampleStereoChannel(SoundEffectInstance instance, float[] buffer, int channel, ref Fix64 dspPhase, ref Fix64 dspPhaseIncr)
        {
            int startIndex;
            short startPoint;
            short endPoint1;
            short endPoint2;
            var effect = instance._effect;
            var dspData = effect._data;
            int loopEnd = effect._loopStart + effect._loopLength;
            UInt32 dspPhaseIndex = dspPhase.Index;
            int dspI = 0;

            // Voice is currently looping?
            var looping = instance._isLooped;

            // Last index before 4th interpolation point must be specially handled
            var endIndex = (looping ? loopEnd - 1 : effect._frames) - 2;

            // Set start index and start point if looped or not
            if (instance._hasLooped)
            {
                startIndex = effect._loopStart;
                // Last point in loop (wrap around)
                startPoint = dspData[((loopEnd - 1) << 1) + channel];
            }
            else
            {
                startIndex = 0;
                // Just duplicate the point
                startPoint = dspData[0 + channel];
            }

            // Get points off the end (loop start if looping, duplicate point if end)
            if (looping)
            {
                endPoint1 = dspData[((effect._loopStart) << 1) + channel];
                endPoint2 = dspData[((effect._loopStart + 1) << 1) + channel];
            }
            else
            {
                endPoint1 = dspData[((effect._frames - 1) << 1) + channel];
                endPoint2 = endPoint1;
            }

            while (true)
            {
                dspPhaseIndex = dspPhase.Index;

                // Interpolate first sample point (start or loop start) if needed
                for (; dspPhaseIndex == startIndex && dspI < buffer.Length; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    int i = (int)(dspPhaseIndex << 1) + channel;
                    buffer[dspI + channel] = (coeffs.X * startPoint)
                        + (coeffs.Y * dspData[i])
                        + (coeffs.Z * dspData[i + 2])
                        + (coeffs.W * dspData[i + 4]);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // Interpolate the sequence of sample points
                for (; dspI < buffer.Length && dspPhaseIndex <= endIndex; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    int i = (int)(dspPhaseIndex << 1) + channel;
                    buffer[dspI + channel] = (coeffs.X * dspData[i - 2])
                        + (coeffs.Y * dspData[i])
                        + (coeffs.Z * dspData[i + 2])
                        + (coeffs.W * dspData[i + 4]);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;

                // We're now interpolating the 2nd to last point
                ++endIndex;

                // Interpolate within the 2nd to last point
                for (; dspPhaseIndex <= endIndex && dspI < buffer.Length; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    int i = (int)(dspPhaseIndex << 1) + channel;
                    buffer[dspI + channel] = (coeffs.X * dspData[i - 2])
                        + (coeffs.Y * dspData[i])
                        + (coeffs.Z * dspData[i + 2])
                        + (coeffs.W * endPoint1);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // We're now interpolating the last point
                ++endIndex;

                // Interpolate within the last point
                for (; dspPhaseIndex <= endIndex && dspI < buffer.Length; dspI += 2)
                {
                    var coeffs = InterpolationCoefficients[(dspPhase.Fract & 0xFF000000) >> 24];
                    int i = (int)(dspPhaseIndex << 1) + channel;
                    buffer[dspI + channel] = (coeffs.X * dspData[i - 2])
                        + (coeffs.Y * dspData[i])
                        + (coeffs.Z * endPoint1)
                        + (coeffs.W * endPoint2);

                    // Increment phase
                    dspPhase += dspPhaseIncr;
                    dspPhaseIndex = dspPhase.Index;
                }

                // Break out if not looping (end of sample)
                if (!looping)
                    break;

                // Go back to loop start
                if (dspPhaseIndex > endIndex)
                {
                    dspPhase -= new Fix64(effect._loopLength);

                    if (!instance._hasLooped)
                    {
                        instance._hasLooped = true;
                        startIndex = effect._loopStart;
                        startPoint = dspData[((loopEnd - 1) << 1) + channel];
                    }
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;

                // Set end back to third to last sample point
                endIndex -= 2;
            }

            return dspI >> 1;
        }

        /// <summary>
        /// Copies a portion of the mono SoundEffectInstance to fill the given stereo buffer.
        /// </summary>
        /// <param name="instance">The SoundEffectInstance to copy.</param>
        /// <param name="buffer">The buffer to fill with copied data.</param>
        /// <returns>The number of frames in the output buffer.</returns>
        static int CopyMono(SoundEffectInstance instance, float[] buffer)
        {
            int startIndex;
            var effect = instance._effect;
            var dspData = effect._data;
            int loopEnd = effect._loopStart + effect._loopLength;
            Fix64 dspPhase = instance._position;
            UInt32 dspPhaseIndex = dspPhase.Index;
            int dspI = 0;

            // Voice is currently looping?
            var looping = instance._isLooped;

            // Last index before 4th interpolation point must be specially handled
            var endIndex = (looping ? loopEnd - 1 : effect._frames) - 1;

            // Set start index and start point if looped or not
            if (instance._hasLooped)
                startIndex = effect._loopStart;
            else
                startIndex = 0;

            while (true)
            {
                dspPhaseIndex = dspPhase.Index;

                // Interpolate the sequence of sample points
                for (; dspI < buffer.Length && dspPhaseIndex <= endIndex; dspI += 2)
                {
                    buffer[dspI] = buffer[dspI + 1] = dspData[dspPhaseIndex];
                    ++dspPhaseIndex;
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;

                // Break out if not looping (end of sample)
                if (!looping)
                    break;

                // Go back to loop start
                if (dspPhaseIndex > endIndex)
                {
                    dspPhaseIndex -= (UInt32)effect._loopLength;

                    if (!instance._hasLooped)
                    {
                        instance._hasLooped = true;
                        startIndex = effect._loopStart;
                    }
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;
            }
            instance._position = new Fix64(dspPhaseIndex, 0);
            return dspI >> 1;
        }

        /// <summary>
        /// Copies a portion of the stereo SoundEffectInstance to fill the given stereo buffer.
        /// </summary>
        /// <param name="instance">The SoundEffectInstance to copy.</param>
        /// <param name="buffer">The buffer to fill with copied data.</param>
        /// <returns>The number of frames in the output buffer.</returns>
        static int CopyStereo(SoundEffectInstance instance, float[] buffer)
        {
            int startIndex;
            var effect = instance._effect;
            var dspData = effect._data;
            int loopEnd = effect._loopStart + effect._loopLength;
            Fix64 dspPhase = instance._position;
            UInt32 dspPhaseIndex = dspPhase.Index;
            int dspI = 0;

            // Voice is currently looping?
            var looping = instance._isLooped;

            // Last index before 4th interpolation point must be specially handled
            var endIndex = (looping ? loopEnd - 1 : effect._frames) - 1;

            // Set start index and start point if looped or not
            if (instance._hasLooped)
                startIndex = effect._loopStart;
            else
                startIndex = 0;

            while (true)
            {
                dspPhaseIndex = dspPhase.Index;

                // Interpolate the sequence of sample points
                for (; dspI < buffer.Length && dspPhaseIndex <= endIndex; dspI += 2)
                {
                    buffer[dspI] = dspData[dspPhaseIndex << 1];
                    buffer[dspI + 1] = dspData[(dspPhaseIndex << 1) + 1];
                    ++dspPhaseIndex;
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;

                // Break out if not looping (end of sample)
                if (!looping)
                    break;

                // Go back to loop start
                if (dspPhaseIndex > endIndex)
                {
                    dspPhaseIndex -= (UInt32)effect._loopLength;

                    if (!instance._hasLooped)
                    {
                        instance._hasLooped = true;
                        startIndex = effect._loopStart;
                    }
                }

                // Break out if buffer filled
                if (dspI >= buffer.Length)
                    break;
            }
            instance._position = new Fix64(dspPhaseIndex, 0);
            return dspI >> 1;
        }

        static void MixerTask()
        {
            float compressor = 0.5f;
            PlatformStart();

            var bufferFrames = PlatformBufferSizeInFrames;
            var nativeSampleRate = PlatformSampleRate;
            var updateBuffers = PlatformUpdateBuffers;

            var bufferSize = bufferFrames * 2;
            var buffers = new short[updateBuffers][];
            for (var i = 0; i < updateBuffers; ++i)
                buffers[i] = new short[bufferSize];
            int index = 1 % updateBuffers;
            var currentBuffer = buffers[index];
            var workBuffer = new float[bufferSize];
            var copyBuffer = new float[bufferSize];
            Array.Clear(buffers[0], 0, bufferSize);
            PlatformSubmitBuffer(buffers[0]);
            while (!_mixerCancellationTokenSource.Token.IsCancellationRequested)
            {
                Array.Clear(workBuffer, 0, bufferSize);
                //Android.Util.Log.Debug("Mixer", "Feeding the buffer. {0} playing instances", _playingInstances.Count);
                // Iterate backwards so we can remove instances if they finish
                int i;
                lock (_playingInstances)
                    i = _playingInstances.Count - 1;
                while (i >= 0)
                {
                    var wbi = 0;
                    SoundEffectInstance inst;
                    lock (_playingInstances)
                        inst = _playingInstances[i];
                    if (!inst.IsDisposed && !inst._effect.IsDisposed)
                    {
                        var pan = inst.Pan;
                        var volume = inst.Volume * compressor;
                        var leftVolume = (pan > 0.0f ? 1.0f - pan : 1.0f) * volume;
                        var rightVolume = (pan < 0.0f ? 1.0f + pan : 1.0f) * volume;
                        switch (inst.State)
                        {
                            case SoundState.Playing:
                                {
                                    try
                                    {
                                        int frames = 0;
                                        if (inst._effect._channels == AudioChannels.Mono)
                                        {
                                            // Mono samples
                                            if (inst._step == Fix64.One)
                                                frames = CopyMono(inst, copyBuffer);
                                            else
                                                frames = ResampleMono(inst, copyBuffer);
                                        }
                                        else
                                        {
                                            // Stereo samples
                                            if (inst._step == Fix64.One)
                                                frames = CopyStereo(inst, copyBuffer);
                                            else
                                                frames = ResampleStereo(inst, copyBuffer);
                                        }
                                        if (frames > 0)
                                        {
                                            var data = inst._effect._data;
                                            for (int s = 0; s < frames; ++s)
                                            {
                                                workBuffer[wbi++] += copyBuffer[s << 1] * leftVolume;
                                                workBuffer[wbi++] += copyBuffer[(s << 1) + 1] * rightVolume;
                                            }
                                        }
                                        if (frames < bufferFrames)
                                        {
                                            //Android.Util.Log.Debug("Mixer", "Instance {0} finished ({1})", inst._effect.Name, inst._id);
                                            inst.Stop(true);
                                        }
                                    }
                                    catch
                                    {
                                        // If there was any unhandled exception thrown during the mix, immediately stop the instance so it gets cleaned up next update
                                        inst.Stop(true);
                                    }
                                }
                                break;
                            case SoundState.Stopped:
                                //Android.Util.Log.Debug("Mixer", "Instance {0} stopped ({1})", inst._effect.Name, inst._id);
                                // Instance has finished, so remove it
                                lock (_playingInstances)
                                    _playingInstances.RemoveAt(i);
                                // Auto-created instances are returned to a pool for use again later
                                if (inst._isPooled)
                                {
                                    lock (_pooledInstances)
                                        _pooledInstances.Add(inst);
                                }
                                break;
                        }
                    }
                    else
                    {
                        // The instance or its effect has been disposed, so remove it
                        lock (_playingInstances)
                            _playingInstances.RemoveAt(i);
                        if (!inst.IsDisposed)
                        {
                            if (inst._effect.IsDisposed)
                            {
                                // Pooled instances are returned to the pool. Created instances are disposed.
                                if (inst._isPooled)
                                {
                                    lock (_pooledInstances)
                                        _pooledInstances.Add(inst);
                                }
                                else
                                    inst.Dispose();
                            }
                        }
                    }
                    --i;
                }

                // Copy from work buffer to 16-bit signed buffer
                for (int j = 0; j < workBuffer.Length; ++j)
                    currentBuffer[j] = (short)workBuffer[j];
                // Submit the buffer to the audio stream
                PlatformSubmitBuffer(currentBuffer);
                // Swap to the other working buffer
                index = (index + 1) % updateBuffers;
                currentBuffer = buffers[index];
            }
            PlatformStop();
        }
    }
}