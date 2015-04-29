// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using SharpDX.XAudio2;
using SharpDX.Multimedia;
using SharpDX;
using System.Collections.Generic;

namespace Microsoft.Xna.Framework.Audio
{
    public sealed partial class DynamicSoundEffectInstance : SoundEffectInstance
    {
        struct QueuedBuffer
        {
            public AudioBuffer Audio;
            public byte[] Buffer;
        }

        WaveFormat _format;

        static Queue<QueuedBuffer> _bufferPool = new Queue<QueuedBuffer>();
        static object _bufferPoolLock = new object();
        static Queue<QueuedBuffer> _activeBuffers = new Queue<QueuedBuffer>();
        static object _activeBuffersLock = new object();

        int PlatformPendingBufferCount
        {
            get
            {
                if (_voice == null)
                    return 0;
                return _voice.State.BuffersQueued;
            }
        }

        static DynamicSoundEffectInstance()
        {
            SoundEffect.InitializeSoundEffect();
        }

        void PlatformInitialize()
        {
            _format = new WaveFormatExtensible(_sampleRate, _bitsPerSample, (int)_channels);
        }

        void PlatformPlay()
        {
            if (SoundEffect.Device == null)
                throw new InvalidOperationException("No audio device created");

            if (_voice == null)
                CreateVoice();

            // Request more buffers from the application if required
            if (_voice.State.BuffersQueued == 0 && BufferNeeded != null)
                BufferNeeded(this, EventArgs.Empty);

            _voice.Start();
        }

        void PlatformDispose(bool disposing)
        {
            if (disposing)
                SoundEffect.RemoveDynamicSoundEffectInstance(this);
        }

        void CreateVoice()
        {
            _voice = new SourceVoice(SoundEffect.Device, _format, true);
            _voice.BufferEnd += PlatformNextBuffer;
            SoundEffect.AddDynamicSoundEffectInstance(this);
        }

        int PlatformBlockAlign
        {
            get
            {
                return _format.BlockAlign;
            }
        }

        void PlatformSubmitBuffer(byte[] buffer, int offset, int count)
        {
            // Copy the buffer first so the caller can re-use the buffer immediately on return
            QueuedBuffer newBuffer = new QueuedBuffer();
            lock (_bufferPoolLock)
            {
                if (_bufferPool.Count > 0)
                    newBuffer = _bufferPool.Dequeue();
            }
            if (newBuffer.Buffer == null || newBuffer.Buffer.Length < count)
                newBuffer = new QueuedBuffer() { Audio = new AudioBuffer(), Buffer = new byte[count] };
            Buffer.BlockCopy(buffer, offset, newBuffer.Buffer, 0, count);

            SubmitInternal(ref newBuffer, count);
        }

        void PlatformSubmitBuffer(float[] buffer, int offset, int count)
        {
            // Copy the buffer first so the caller can re-use the buffer immediately on return
            QueuedBuffer newBuffer = new QueuedBuffer();
            lock (_bufferPoolLock)
            {
                if (_bufferPool.Count > 0)
                    newBuffer = _bufferPool.Dequeue();
            }
            // We want the count in bytes
            count = count * 4;
            if (newBuffer.Buffer == null || newBuffer.Buffer.Length < count)
                newBuffer = new QueuedBuffer() { Audio = new AudioBuffer(), Buffer = new byte[count] };
            Buffer.BlockCopy(buffer, offset * 4, newBuffer.Buffer, 0, count);

            SubmitInternal(ref newBuffer, count);
        }

        void SubmitInternal(ref QueuedBuffer newBuffer, int byteCount)
        {
            var stream = DataStream.Create(newBuffer.Buffer, true, true);

            var audio = newBuffer.Audio;
            audio.Stream = stream;
            audio.AudioBytes = byteCount;

            var key = audio.GetHashCode();
            audio.Context = new IntPtr(key);
            lock (_activeBuffersLock)
                _activeBuffers.Enqueue(newBuffer);

            if (_voice == null)
                CreateVoice();
            _voice.SubmitSourceBuffer(audio, null);
        }

        void PlatformNextBuffer(IntPtr context)
        {
            // Remove this buffer from the active list
            QueuedBuffer audio = new QueuedBuffer();
            lock (_activeBuffersLock)
            {
                if (_activeBuffers.Count > 0)
                {
                    audio = _activeBuffers.Dequeue();
                    if (audio.Audio.Stream != null)
                        audio.Audio.Stream.Dispose();
                }
            }

            // Return this buffer to the pool
            if (audio.Audio != null)
            {
                lock (_bufferPoolLock)
                    _bufferPool.Enqueue(audio);
            }

            // Request more buffers from the application
            var count = _voice.State.BuffersQueued;
            if (BufferNeeded != null && count > 0 && count < 3)
                BufferNeeded(this, EventArgs.Empty);
        }
    }
}
