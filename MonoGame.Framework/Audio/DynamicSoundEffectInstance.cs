// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Audio
{
    /// <summary>
    /// Provides a sound effect instance where the waveform data is supplied by the application.
    /// </summary>
    public sealed partial class DynamicSoundEffectInstance : SoundEffectInstance
    {
        AudioChannels _channels;
        int _sampleRate;
        int _bitsPerSample;

        /// <summary>
        /// Called when the number of queued buffers is equal to or drops below two.
        /// </summary>
        /// <remarks>Occurs when PendingBufferCount is less than or equal to two. The BufferNeeded event is raised if
        /// <list type=">">
        /// <item>During playback when the pending buffer count is less than or equal to two.</item>
        /// <item>Each time a buffer is completed and the pending buffer count is updated.</item>
        /// <item>The state is transitioned from Stopped to Playing and the pending buffer count is less than or equal to two.</item>
        /// </list>
        /// </remarks>
        public event EventHandler<EventArgs> BufferNeeded;

        /// <summary>
        /// Indicates if the DynamicSoundEffectInstance is looped or not.
        /// </summary>
        /// <remarks>A DynamicSoundEffectInstance cannot be looped, so this property always returns <c>false</c>.</remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to IsLooped being called.</exception>
        /// <exception cref="InvalidOperationException">Thrown if an attempt is made to set the DynamicSoundEffectInstance to loop.</exception>
        public override bool IsLooped
        {
            get
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(GetType().Name);
                return false;
            }
            set
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(GetType().Name);
                if (value)
                    throw new InvalidOperationException("DynamicSoundEffectInstance cannot be set to loop");
            }
        }

        /// <summary>
        /// Creates a new instance of DynamicSoundEffectInstance with the given sample rate and channels.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hertz (Hz). Must be between 8000 Hz and 48000 Hz.</param>
        /// <param name="channels">The number of channels.</param>
        /// <exception cref="ArgumentOutOfRangeException">The sample rate provided is less than 8000 Hz or greater than 48000 Hz.</exception>
        public DynamicSoundEffectInstance(int sampleRate, AudioChannels channels)
        {
            if (sampleRate < 8000 || sampleRate > 48000)
                throw new ArgumentOutOfRangeException("sampleRate", "Sample rate must be between 8000 Hz and 48000 Hz inclusive.");
            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = sizeof(short) * 8;
            PlatformInitialize();
        }

        /// <summary>
        /// Creates a new instance of DynamicSoundEffectInstance with the given sample rate and channels.
        /// </summary>
        /// <param name="sampleRate">Sample rate in Hertz (Hz). Must be between 8000 Hz and 48000 Hz.</param>
        /// <param name="channels">The number of channels.</param>
        /// <param name="bitsPerSample">The number of bits for each sample. 16 for 16-bit PCM and 32 for float PCM.</param>
        /// <exception cref="ArgumentOutOfRangeException">The sample rate provided is less than 8000 Hz or greater than 48000 Hz.</exception>
        public DynamicSoundEffectInstance(int sampleRate, AudioChannels channels, int bitsPerSample)
        {
            if (sampleRate < 8000 || sampleRate > 48000)
                throw new ArgumentOutOfRangeException("sampleRate", "Sample rate must be between 8000 Hz and 48000 Hz inclusive.");
            if (bitsPerSample != sizeof(short) * 8 && bitsPerSample != sizeof(float) * 8)
                throw new ArgumentOutOfRangeException("bitsPerSample", "bitsPerSample must be 16 (16-bit PCM) or 32 (float PCM).");
            _sampleRate = sampleRate;
            _channels = channels;
            _bitsPerSample = bitsPerSample;
            PlatformInitialize();
        }

        /// <summary>
        /// Returns the duration of the sound based on the given buffer size in bytes.
        /// </summary>
        /// <param name="sizeInBytes">The size of the buffer in bytes.</param>
        /// <returns>The TimeSpan representing the duration of buffer.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to GetSampleDuration being called.</exception>
        public TimeSpan GetSampleDuration(int sizeInBytes)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            return TimeSpan.FromSeconds((double)(sizeInBytes / PlatformBlockAlign) / _sampleRate);
        }

        /// <summary>
        /// Returns the buffer size in bytes required to play for the given duration.
        /// </summary>
        /// <param name="duration">TimeSpan representing the requested duration.</param>
        /// <returns>The buffer size in bytes required to play for the requested duration.</returns>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to GetSampleSizeInBytes being called.</exception>
        public int GetSampleSizeInBytes(TimeSpan duration)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            var blockAlign = PlatformBlockAlign;
            // Round to the nearest block alignment
            var sampleSize = (int)(duration.TotalSeconds * _sampleRate * blockAlign);
            var m = sampleSize % blockAlign;
            if (m != 0)
                sampleSize += blockAlign - m;
            return sampleSize;
        }

        /// <summary>
        /// Begins or resumes playback of the sound.
        /// </summary>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to Play being called.</exception>
        public override void Play()
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            if (State == SoundState.Playing)
                return;
            if (State == SoundState.Paused)
                Resume();
            else
                PlatformPlay();
        }

        /// <summary>
        /// Submit a buffer for playback.
        /// </summary>
        /// <param name="buffer">The audio data buffer.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to SubmitBuffer being called.</exception>
        public void SubmitBuffer(byte[] buffer)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentException("buffer must be not null and not zero length");
            var blockAlign = PlatformBlockAlign;
            // For performance purposes, assume blockAlign is 2 (16-bit PCM mono), 4 (16-bit PCM stereo or 32-bit float mono) or 8 (32-bit float stereo)
            // so we can do a simple mask to test block alignment
            var mask = blockAlign - 1;
            if ((buffer.Length & mask) != 0)
                throw new ArgumentException("Buffer length must comply with format alignment restrictions. Block alignment = 2 * channels");

            PlatformSubmitBuffer(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Submit a partial buffer for playback.
        /// </summary>
        /// <param name="buffer">The audio data buffer.</param>
        /// <param name="offset">The offset in bytes where the playback will start.</param>
        /// <param name="count">The length in bytes of the data that will be played.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to SubmitBuffer being called.</exception>
        /// <exception cref="ArgumentException">Thrown if buffer is null or zero length, offset is less than zero or exceeds the buffer length, or the buffer, count or offset do not comply with the format alignment restrictions.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the count is less than or equal to zero or the sum of the offset and count exceeds the buffer length.</exception>
        public void SubmitBuffer(byte[] buffer, int offset, int count)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentException("buffer must be not null and not zero length");
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentException("offset must be greater than or equal to zero and less than the size of the buffer");
            if (count <= 0 || (offset + count) > buffer.Length)
                throw new ArgumentOutOfRangeException("count must be greater than zero and the sum of offset and count must be less than the size of the buffer");
            var blockAlign = PlatformBlockAlign;
            // For performance purposes, assume blockAlign is 2 (16-bit PCM mono) or 4 (16-bit PCM stereo) so we can do a simple mask to test block alignment
            var mask = blockAlign - 1;
            if ((buffer.Length & mask) != 0 || (offset & mask) != 0 || (count & mask) != 0)
                throw new ArgumentException("Buffer length, count and offset must comply with format alignment restrictions. Block alignment = 2 * channels");

            PlatformSubmitBuffer(buffer, offset, count);
        }

        /// <summary>
        /// Submit a buffer for playback.
        /// </summary>
        /// <param name="buffer">The audio data buffer.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to SubmitBuffer being called.</exception>
        public void SubmitBuffer(float[] buffer)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentException("buffer must be not null and not zero length");
            var blockAlign = PlatformBlockAlign;
            // For performance purposes, assume blockAlign is 2 (16-bit PCM mono), 4 (16-bit PCM stereo or 32-bit float mono) or 8 (32-bit float stereo)
            // so we can do a simple mask to test block alignment
            var mask = blockAlign - 1;
            if (((buffer.Length * 4) & mask) != 0)
                throw new ArgumentException("Buffer length must comply with format alignment restrictions. Block alignment = 2 * channels");

            PlatformSubmitBuffer(buffer, 0, buffer.Length);
        }

        /// <summary>
        /// Submit a partial buffer for playback.
        /// </summary>
        /// <param name="buffer">The audio data buffer.</param>
        /// <param name="offset">The offset in elements where the playback will start.</param>
        /// <param name="count">The length in elements of the data that will be played.</param>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to SubmitBuffer being called.</exception>
        /// <exception cref="ArgumentException">Thrown if buffer is null or zero length, offset is less than zero or exceeds the buffer length, or the buffer, count or offset do not comply with the format alignment restrictions.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the count is less than or equal to zero or the sum of the offset and count exceeds the buffer length.</exception>
        public void SubmitBuffer(float[] buffer, int offset, int count)
        {
            if (IsDisposed)
                throw new ObjectDisposedException(GetType().Name);
            if (buffer == null || buffer.Length == 0)
                throw new ArgumentException("buffer must be not null and not zero length");
            if (offset < 0 || offset >= buffer.Length)
                throw new ArgumentException("offset must be greater than or equal to zero and less than the size of the buffer");
            if (count <= 0 || (offset + count) > buffer.Length)
                throw new ArgumentOutOfRangeException("count must be greater than zero and the sum of offset and count must be less than the size of the buffer");
            var blockAlign = PlatformBlockAlign;
            // For performance purposes, assume blockAlign is 2 (16-bit PCM mono) or 4 (16-bit PCM stereo) so we can do a simple mask to test block alignment
            var mask = blockAlign - 1;
            if (((buffer.Length * 4) & mask) != 0 || ((offset * 4) & mask) != 0 || ((count * 4) & mask) != 0)
                throw new ArgumentException("Buffer length, count and offset must comply with format alignment restrictions. Block alignment = 2 * channels");

            PlatformSubmitBuffer(buffer, offset, count);
        }

        /// <summary>
        /// Returns the number of queued buffers awaiting playback.
        /// </summary>
        /// <remarks>The pending buffer count will be updated as each buffer is used. The first buffer in the queue is the buffer currently being played.</remarks>
        /// <exception cref="ObjectDisposedException">Thrown if the DynamicSoundEffectInstance has been disposed prior to PendingBufferCount being called.</exception>
        public int PendingBufferCount 
        {
            get
            {
                if (IsDisposed)
                    throw new ObjectDisposedException(GetType().Name);
                return PlatformPendingBufferCount;
            }
        }

        /// <summary>
        /// Releases the resources held by this DynamicSoundEffectInstance.
        /// </summary>
        /// <param name="disposing">If set to <c>true</c>, Dispose was called explicitly.</param>
        /// <remarks>If the disposing parameter is true, the Dispose method was called explicitly. This
        /// means that managed objects referenced by this instance should be disposed or released as
        /// required.  If the disposing parameter is false, Dispose was called by the finalizer and
        /// no managed objects should be touched because we do not know if they are still valid or
        /// not at that time.  Unmanaged resources should always be released.</remarks>
        protected override void Dispose(bool disposing)
        {
            if (!IsDisposed)
            {
                PlatformDispose(disposing);
            }
            base.Dispose(disposing);
        }
    }
}
