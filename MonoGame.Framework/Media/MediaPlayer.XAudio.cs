// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Diagnostics;
using System.Threading;
using SharpDX;
using SharpDX.MediaFoundation;
using SharpDX.Win32;
using SharpDX.XAudio2;
using SharpDX.Multimedia;
using System.Threading.Tasks;
using System.Runtime.InteropServices;

namespace Microsoft.Xna.Framework.Media
{
    public static partial class MediaPlayer
    {
        const int StreamingBufferSize = 65536;
        const int MaxBufferCount = 3;

        static XAudio2 _device;
        static MasteringVoice _masterVoice;
        static SourceVoice _sourceVoice;
        static SourceReader _reader;
        static MediaType _mediaType;
        static Object _readerLock = new Object();
        static WaveFormat _waveFormat;
        static uint _maxStreamLengthInBytes;
        static Task _renderTask;
        static CancellationTokenSource _renderCancellationToken;
        static long _timeStamp;
        static bool _engineStarted;

        private static void PlatformInitialize()
        {
            try
            {
                if (_device == null)
                {
#if !WINRT && DEBUG
                    try
                    {
                        //Fails if the XAudio2 SDK is not installed
                        _device = new XAudio2(XAudio2Flags.DebugEngine, ProcessorSpecifier.DefaultProcessor);
                    }
                    catch
#endif
                    {
                        _device = new XAudio2(XAudio2Flags.None, ProcessorSpecifier.DefaultProcessor);
                    }
                }

                // Just use the default device.
#if WINRT
                string deviceId = null;
#else
                const int deviceId = 0;
#endif

                if (_masterVoice == null)
                {
                    // Let windows autodetect number of channels and sample rate.
                    _masterVoice = new MasteringVoice(_device, XAudio2.DefaultChannels, XAudio2.DefaultSampleRate, deviceId);
                }

                MediaManagerState.CheckStartup();
            }
            catch
            {
                // Release the device and null it as
                // we have no audio support.
                if (_device != null)
                {
                    _device.Dispose();
                    _device = null;
                }

                _masterVoice = null;
            }
        }

        private static void PlatformDeinitialize()
        {
            if (_renderTask != null)
            {
                _renderCancellationToken.Cancel();
                try
                {
                    _renderTask.Wait();
                }
                catch
                {

                }
            }
            SharpDX.Utilities.Dispose(ref _renderTask);
            SharpDX.Utilities.Dispose(ref _reader);
            SharpDX.Utilities.Dispose(ref _mediaType);
            SharpDX.Utilities.Dispose(ref _sourceVoice);
            SharpDX.Utilities.Dispose(ref _masterVoice);
            SharpDX.Utilities.Dispose(ref _device);
        }

        #region Properties

        private static bool PlatformGetIsMuted()
        {
            return _isMuted;
        }

        private static void PlatformSetIsMuted(bool muted)
        {
            _isMuted = muted;
            if (_sourceVoice != null)
                _sourceVoice.SetVolume(_volume * (_isMuted ? 0.0f : 1.0f));
        }

        private static bool PlatformGetIsRepeating()
        {
            return _isRepeating;
        }

        private static void PlatformSetIsRepeating(bool repeating)
        {
            _isRepeating = repeating;
        }

        private static bool PlatformGetIsShuffled()
        {
            return _isShuffled;
        }

        private static void PlatformSetIsShuffled(bool shuffled)
        {
            _isShuffled = shuffled;
        }

        private static TimeSpan PlatformGetPlayPosition()
        {
            return _reader != null ? TimeSpan.FromTicks(_timeStamp) : TimeSpan.Zero;
        }

        private static bool PlatformGetGameHasControl()
        {
            // TODO: Fix me!
            return true;
        }

        private static MediaState PlatformGetState()
        {
            return _state;
        }

        private static float PlatformGetVolume()
        {
            return _volume;
        }

        private static void PlatformSetVolume(float volume)
        {
            _volume = volume;
            if (!_isMuted && _sourceVoice != null)
                _sourceVoice.SetVolume(_volume);
        }
		
		#endregion

        private static void PlatformPause()
        {
            if (_engineStarted)
            {
                _device.StopEngine();
                _engineStarted = false;
            }
        }

        private static void PlatformPlaySong(Song song)
        {
            // Cleanup the last song first.
            if (State != MediaState.Stopped)
                PlatformStop();

            if (!_engineStarted)
            {
                _device.StartEngine();
                _engineStarted = true;
            }

            _reader = new SourceReader(song.FilePath);

            // Set the decoded output format as PCM.
            // XAudio2 on Windows can process PCM and ADPCM-encoded buffers.
            // When this sample uses Media Foundation, it always decodes into PCM.

            _mediaType = new MediaType();
            _mediaType.Set<Guid>(MediaTypeAttributeKeys.MajorType, MediaTypeGuids.Audio);
            _mediaType.Set<Guid>(MediaTypeAttributeKeys.Subtype, AudioFormatGuids.Pcm);
            _reader.SetCurrentMediaType(SourceReaderIndex.FirstAudioStream, _mediaType);

            // Get the complete WAVEFORMAT from the Media Type.
            using (var outputMediaType = _reader.GetCurrentMediaType(SourceReaderIndex.FirstAudioStream))
            {
                int bufferSize;
                _waveFormat = outputMediaType.ExtracttWaveFormat(out bufferSize);
            }

            // Get the total length of the stream, in bytes.
            var duration = _reader.GetPresentationAttribute(SourceReaderIndex.MediaSource, PresentationDescriptionAttributeKeys.Duration);
            _maxStreamLengthInBytes = (uint)(((duration * _waveFormat.AverageBytesPerSecond) + 10000000) / 10000000);

            if (_sourceVoice == null)
            {
                _sourceVoice = new SourceVoice(_device, _waveFormat, VoiceFlags.Music);
            }
            _sourceVoice.SetVolume(_volume * (_isMuted ? 0.0f : 1.0f));
            _sourceVoice.Start();

            if (_renderTask == null || _renderTask.IsCanceled || _renderTask.IsCompleted || _renderTask.IsFaulted)
            {
                _renderCancellationToken = new CancellationTokenSource();
                _renderTask = Task.Factory.StartNew(Render, _renderCancellationToken.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }

        private static void PlatformResume()
        {
            if (!_engineStarted)
            {
                _device.StartEngine();
                _engineStarted = true;
            }
        }

        private static void PlatformStop()
		{
            if (_renderTask != null)
            {
                _renderCancellationToken.Cancel();
                try
                {
                    _renderTask.Wait();
                }
                catch
                {
                }
                SharpDX.Utilities.Dispose(ref _renderTask);
            }
        }

        static void StopInternal()
        {
            if (_sourceVoice != null)
                _sourceVoice.Stop();
            SharpDX.Utilities.Dispose(ref _reader);
            SharpDX.Utilities.Dispose(ref _mediaType);
            State = MediaState.Stopped;
        }

        static unsafe void Render()
        {
            Debug.WriteLine("Starting audio render thread");
            // Allocate buffers for streaming
            var audioBuffers = new byte[MaxBufferCount][];
            for (int i = 0; i < MaxBufferCount; ++i)
                audioBuffers[i] = new byte[StreamingBufferSize];
            var currentBuffer = 0;

            var buffer = new AudioBuffer();

            while (!_renderCancellationToken.Token.IsCancellationRequested)
            {
                var state = _sourceVoice.State;
                while (!_renderCancellationToken.Token.IsCancellationRequested && state.BuffersQueued < MaxBufferCount)
                {
                    int bufferLength;
                    bool streamComplete;
                    streamComplete = GetNextBuffer(audioBuffers[currentBuffer], out bufferLength);
                    
                    if (bufferLength > 0)
                    {
                        fixed (byte* p = audioBuffers[currentBuffer])
                        {
                            buffer.AudioBytes = bufferLength;
                            buffer.AudioDataPointer = (IntPtr)p;
                            buffer.Flags = streamComplete ? BufferFlags.EndOfStream : BufferFlags.None;
                            buffer.Context = IntPtr.Zero;

                            _sourceVoice.SubmitSourceBuffer(buffer, null);
                        }

                        currentBuffer = (currentBuffer + 1) % MaxBufferCount;
                    }

                    if (streamComplete)
                    {
                        if (_isRepeating)
                        {
                            Debug.WriteLine("Restarting music at position 0");
                            _reader.SetCurrentPosition(0);
                        }
                        else
                        {
                            Debug.WriteLine("Stopping music at end of track");
                            _renderCancellationToken.Cancel();
                            break;
                        }
                    }

                    state = _sourceVoice.State;
                }
            }
            StopInternal();
            Debug.WriteLine("Ending audio render thread");
        }

        static bool GetNextBuffer(byte[] buffer, out int bufferLength)
        {
            int streamIndex;
            SourceReaderFlags flags = SourceReaderFlags.None;
            long timeStampRef = 0;
            Sample sample = null;
            lock (_readerLock)
            {
                sample = _reader != null ? _reader.ReadSample(SourceReaderIndex.FirstAudioStream, SourceReaderControlFlags.None, out streamIndex, out flags, out timeStampRef) : null;
            }
            _timeStamp = timeStampRef;
            bufferLength = 0;
            if (sample == null)
                return (flags & SourceReaderFlags.Endofstream) == SourceReaderFlags.Endofstream;

            var mediaBuffer = sample.ConvertToContiguousBuffer();
            int maxLengthRef;
            var audioData = mediaBuffer.Lock(out maxLengthRef, out bufferLength);

            // Only copy the sample if the remaining buffer is large enough
            if (bufferLength <= buffer.Length)
                Marshal.Copy(audioData, buffer, 0, bufferLength);

            mediaBuffer.Dispose();
            sample.Dispose();

            return (flags & SourceReaderFlags.Endofstream) == SourceReaderFlags.Endofstream;
        }
    }
}

