#region License
/*
Microsoft Public License (Ms-PL)
MonoGame - Copyright © 2009 The MonoGame Team

All rights reserved.

This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
accept the license, do not use the software.

1. Definitions
The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
U.S. copyright law.

A "contribution" is the original software, or any additions or changes to the software.
A "contributor" is any person that distributes its contribution under this license.
"Licensed patents" are a contributor's patent claims that read directly on its contribution.

2. Grant of Rights
(A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
(B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.

3. Conditions and Limitations
(A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
(B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
your patent license from such contributor to the software ends automatically.
(C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
notices that are present in the software.
(D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
code form, you may only do so under a license that complies with this license.
(E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
purpose and non-infringement.
*/
#endregion License

#if ANDROID
//#define TRACK_SOUNDEFFECTS
#endif
﻿
using System;
using System.Collections.Generic;
using System.IO;

using Microsoft.Xna;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;

#if DIRECTX
using SharpDX;
using SharpDX.XAudio2;
using SharpDX.Multimedia;
using SharpDX.X3DAudio;
#elif OPENAL
using OpenTK.Audio.OpenAL;
#elif AUDIOTRACK
using Android.Media;
using System.Threading.Tasks;
using System.Threading;
#endif

namespace Microsoft.Xna.Framework.Audio
{
    public sealed class SoundEffect : IDisposable
    {
        private bool isDisposed = false;

        #region Internal Audio Data

        private string _name;

#if TRACK_SOUNDEFFECTS
        static List<SoundEffect> _soundEffects = new List<SoundEffect>();
#endif

#if DIRECTX || OPENAL || AUDIOTRACK
        // These fields are used for keeping track of instances created
        // internally when Play is called directly on SoundEffect.
        static internal List<SoundEffectInstance> _playingInstances = new List<SoundEffectInstance>(64);
        private List<SoundEffectInstance> _availableInstances;
#endif

#if DIRECTX
        internal DataStream _dataStream;
        internal AudioBuffer _buffer;
        internal AudioBuffer _loopedBuffer;
        internal WaveFormat _format;
#else
#if OPENAL
        internal byte[] _data;

        // OpenAL-specific information

        internal int Size
        {
            get;
            set;
        }

        internal float Rate
        {
            get;
            set;
        }

        internal ALFormat Format
        {
            get;
            set;
        }
#elif AUDIOTRACK
        internal short[] _data;
        internal int _sampleRate;
        internal AudioChannels _channels;
        internal int _frames;

        static Task _mixerTask;
        static CancellationToken _mixerCancellationToken;
#else
        internal byte[] _data;
        private string _filename = "";
        private Sound _sound;
        private SoundEffectInstance _instance;
#endif

#endif

        #endregion

        #region Static constructor
        static SoundEffect()
        {
#if DIRECTX
            InitializeSoundEffect();
#elif AUDIOTRACK
            _mixerTask = Task.Factory.StartNew(MixerTask, _mixerCancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Current);
#endif
        }
        #endregion

        #region Mixer thread
#if AUDIOTRACK
        static void MixerTask()
        {
            float compressor = 0.5f;
            int sampleRate = 22050;
            var nativeSampleRate = AudioTrack.GetNativeOutputSampleRate(Android.Media.Stream.Music);
            var bufferSizeBytes = AudioTrack.GetMinBufferSize(sampleRate, ChannelOut.Stereo, Encoding.Pcm16bit);
            var bufferSize = bufferSizeBytes / 2;
            var bufferFrames = bufferSize / 2;
            Android.Util.Log.Debug("Mixer", "Mixer starting with buffer of {0} stereo samples. Native sample rate {1}", bufferSize, nativeSampleRate);
            var audioTrack = new AudioTrack(Android.Media.Stream.Music, sampleRate, ChannelConfiguration.Stereo, Encoding.Pcm16bit, bufferSizeBytes, AudioTrackMode.Stream);
            var buffer0 = new short[bufferSize];
            var buffer1 = new short[bufferSize];
            var workBuffer = new float[bufferSize];
            var currentBuffer = buffer1;
            int index = 1;
            Array.Clear(buffer0, 0, bufferSize);
            audioTrack.Play();
            audioTrack.Write(buffer0, 0, bufferSize);
            while (!_mixerCancellationToken.IsCancellationRequested)
            {
                Array.Clear(workBuffer, 0, bufferSize);
                try
                {
                    lock (_playingInstances)
                    {
                        //Android.Util.Log.Debug("Mixer", "Feeding the buffer. {0} playing instances", _playingInstances.Count);
                        // Iterate backwards so we can remove instances if they finish
                        for (int i = _playingInstances.Count - 1; i >= 0; --i)
                        {
                            var wbi = 0;
                            var inst = _playingInstances[i];
                            var pan = inst.Pan;
                            var volume = inst.Volume * compressor;
                            var leftVolume = (pan > 0.0f ? 1.0f - pan : 1.0f) * volume;
                            var rightVolume = (pan < 0.0f ? 1.0f + pan : 1.0f) * volume;
                            switch (inst.soundState)
                            {
                                case SoundState.Playing:
                                    if (inst._effect._channels == AudioChannels.Mono)
                                    {
                                        var framesToMix = inst._effect._frames - inst._position;
                                        if (framesToMix > bufferFrames)
                                            framesToMix = bufferFrames;
                                        if (framesToMix > 0)
                                        {
                                            var data = inst._effect._data;
                                            for (int s = 0; s < framesToMix; ++s)
                                            {
                                                float sample = data[inst._position + s];
                                                workBuffer[wbi++] += sample * leftVolume;
                                                workBuffer[wbi++] += sample * rightVolume;
                                            }
                                            inst._position += framesToMix;
                                        }

                                        if (inst._position >= inst._effect._frames)
                                        {
                                            if (inst._loop)
                                            {
                                                // Start back at the beginning of the sample data and fill the rest of the buffer
                                                var data = inst._effect._data;
                                                var remainingFramesToMix = bufferFrames - framesToMix;
                                                inst._position = remainingFramesToMix;
                                                for (int s = 0; s < remainingFramesToMix; ++s)
                                                {
                                                    float sample = data[s];
                                                    workBuffer[wbi++] += sample * leftVolume;
                                                    workBuffer[wbi++] += sample * rightVolume;
                                                }
                                            }
                                            else
                                            {
                                                //Android.Util.Log.Debug("Mixer", "Instance {0} finished ({1})", inst._effect._name, inst._id);
                                                inst.Stop(true);
                                                // Instance has finished, so remove it
                                                _playingInstances.RemoveAt(i);
                                                // Auto-created instances are returned to a pool for use again later
                                                if (inst._autoCreated)
                                                    inst._effect._availableInstances.Add(inst);
                                            }
                                        }
                                    }
                                    else
                                    {
                                        // Stereo samples
                                        var framesToMix = inst._effect._frames - inst._position;
                                        if (framesToMix > bufferFrames)
                                            framesToMix = bufferFrames;
                                        if (framesToMix > 0)
                                        {
                                            var data = inst._effect._data;
                                            var di = inst._position;
                                            for (int s = 0; s < framesToMix; ++s)
                                            {
                                                float leftSample = data[di++];
                                                float rightSample = data[di++];
                                                workBuffer[wbi++] += leftSample * leftVolume;
                                                workBuffer[wbi++] += rightSample * rightVolume;
                                            }
                                            inst._position += framesToMix;
                                        }

                                        if (inst._position >= inst._effect._frames)
                                        {
                                            if (inst._loop)
                                            {
                                                // Start back at the beginning of the sample data and fill the rest of the buffer
                                                var data = inst._effect._data;
                                                var remainingFramesToMix = bufferFrames - framesToMix;
                                                inst._position = remainingFramesToMix;
                                                var di = 0;
                                                for (int s = 0; s < remainingFramesToMix; ++s)
                                                {
                                                    float leftSample = data[di++];
                                                    float rightSample = data[di++];
                                                    workBuffer[wbi++] += leftSample * leftVolume;
                                                    workBuffer[wbi++] += rightSample * rightVolume;
                                                }
                                            }
                                            else
                                            {
                                                //Android.Util.Log.Debug("Mixer", "Instance {0} finished ({1})", inst._effect._name, inst._id);
                                                inst.Stop(true);
                                                // Instance has finished, so remove it
                                                _playingInstances.RemoveAt(i);
                                                // Auto-created instances are returned to a pool for use again later
                                                if (inst._autoCreated)
                                                    inst._effect._availableInstances.Add(inst);
                                            }
                                        }
                                    }
                                    break;
                                case SoundState.Stopped:
                                    //Android.Util.Log.Debug("Mixer", "Instance {0} stopped ({1})", inst._effect._name, inst._id);
                                    // Instance has finished, so remove it
                                    _playingInstances.RemoveAt(i);
                                    // Auto-created instances are returned to a pool for use again later
                                    if (inst._autoCreated)
                                        inst._effect._availableInstances.Add(inst);
                                    break;
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    Android.Util.Log.Debug("Mixer", ex.Message);
                }
                // Copy from work buffer to 16-bit signed buffer
                for (int i = 0; i < workBuffer.Length; ++i)
                    currentBuffer[i] = (short)workBuffer[i];
                audioTrack.Write(currentBuffer, 0, bufferSize);
                index = 1 - index;
                currentBuffer = index == 0 ? buffer0 : buffer1;
            }
            Android.Util.Log.Debug("Mixer", "Mixer terminating");
            audioTrack.Stop();
            audioTrack.Release();
            audioTrack.Dispose();
        }
#endif
        #endregion

        #region Internal Constructors
#if DIRECTX
        internal SoundEffect()
        {
#if TRACK_SOUNDEFFECTS
            _soundEffects.Add(this);
#endif
        }

        // Extended constructor which supports custom formats / compression.
        internal SoundEffect(WaveFormat format, byte[] buffer, int offset, int count, int loopStart, int loopLength)
        {
            Initialize(format, buffer, offset, count, loopStart, loopLength);
#if TRACK_SOUNDEFFECTS
            _soundEffects.Add(this);
#endif
        }
#elif AUDIOTRACK
#else
        internal SoundEffect(string fileName)
        {
            _filename = fileName;

            if (_filename == string.Empty )
            {
                throw new FileNotFoundException("Supported Sound Effect formats are wav, mp3, aac, aiff");
            }

            _name = Path.GetFileNameWithoutExtension(fileName);

#if OPENAL
            Stream s;
            try
            {
                s = TitleContainer.OpenStream(fileName);
#if ANDROID
                // Copy to a MemoryStream a) to decrease load time, and b) because the Android Asset stream does not support some of the Stream properties
                MemoryStream memStream = new MemoryStream();
                s.CopyTo(memStream);
                memStream.Seek(0, SeekOrigin.Begin);
                s.Close();
                s = memStream;
#endif
            }
            catch (IOException e)
            {
                throw new Content.ContentLoadException("Could not load audio data", e);
            }

            try
            {
                _data = LoadAudioStream(s, 1.0f, false);
            }
            finally
            {
                s.Dispose();
            }
#else
            _sound = new Sound(_filename, 1.0f, false);
#endif
#if TRACK_SOUNDEFFECTS
            _soundEffects.Add(this);
#endif
        }

        //SoundEffect from playable audio data
        internal SoundEffect(string name, byte[] data)
        {
            _data = data;
            _name = name;

#if OPENAL
            Stream s;
            try
            {
                s = new MemoryStream(data);
            }
            catch (IOException e)
            {
                throw new Content.ContentLoadException("Could not load audio data", e);
            }

            try
            {
                _data = LoadAudioStream(s, 1.0f, false);
            }
            finally
            {
                s.Dispose();
            }
#else
            _sound = new Sound(_data, 1.0f, false);
#endif
#if TRACK_SOUNDEFFECTS
            _soundEffects.Add(this);
#endif
        }        
#endif

#if !AUDIOTRACK
        internal SoundEffect(Stream s)
        {
#if OPENAL
            _data = LoadAudioStream(s, 1.0f, false);
#elif !DIRECTX
            var data = new byte[s.Length];
            s.Read(data, 0, (int)s.Length);

            _data = data;
            _sound = new Sound(_data, 1.0f, false);
#endif
#if TRACK_SOUNDEFFECTS
            _soundEffects.Add(this);
#endif
        }
#endif

        internal SoundEffect(string name, byte[] buffer, int sampleRate, AudioChannels channels)
            : this(buffer, sampleRate, channels)
        {
            _name = name;
        }

        ~SoundEffect()
        {
            Dispose(false);
        }

        #endregion

        #region Public Constructors

        public SoundEffect(byte[] buffer, int sampleRate, AudioChannels channels)
        {
#if DIRECTX            
            Initialize(new WaveFormat(sampleRate, (int)channels), buffer, 0, buffer.Length, 0, buffer.Length);
#elif AUDIOTRACK
            int sampleCount = buffer.Length / 2;
            _data = new short[sampleCount];
            Buffer.BlockCopy(buffer, 0, _data, 0, buffer.Length);
            _sampleRate = sampleRate;
            _channels = channels;
            _frames = _data.Length;
            if (channels == AudioChannels.Stereo)
                _frames /= 2;
#elif OPENAL
            _data = buffer;
            Size = buffer.Length;
            Format = (channels == AudioChannels.Stereo) ? ALFormat.Stereo16 : ALFormat.Mono16;
            Rate = sampleRate;
#else
            //buffer should contain 16-bit PCM wave data
            short bitsPerSample = 16;

            _name = "";

            using (var mStream = new MemoryStream(44+buffer.Length))
            using (var writer = new BinaryWriter(mStream))
            {
                writer.Write("RIFF".ToCharArray()); //chunk id
                writer.Write((int)(36 + buffer.Length)); //chunk size
                writer.Write("WAVE".ToCharArray()); //RIFF type

                writer.Write("fmt ".ToCharArray()); //chunk id
                writer.Write((int)16); //format header size
                writer.Write((short)1); //format (PCM)
                writer.Write((short)channels);
                writer.Write((int)sampleRate);
                short blockAlign = (short)((bitsPerSample / 8) * (int)channels);
                writer.Write((int)(sampleRate * blockAlign)); //byte rate
                writer.Write((short)blockAlign);
                writer.Write((short)bitsPerSample);

                writer.Write("data".ToCharArray()); //chunk id
                writer.Write((int)buffer.Length); //data size   MonoGame.Framework.Windows8.DLL!Microsoft.Xna.Framework.Audio.Sound.Sound(byte[] audiodata, float volume, bool looping) Line 199    C#

                writer.Write(buffer);

                _data = mStream.ToArray();
            }

            _sound = new Sound(_data, 1.0f, false);
#endif
#if TRACK_SOUNDEFFECTS
            _soundEffects.Add(this);
#endif
        }

        public SoundEffect(byte[] buffer, int offset, int count, int sampleRate, AudioChannels channels, int loopStart, int loopLength)
        {
#if DIRECTX
            Initialize(new WaveFormat(sampleRate, (int)channels), buffer, offset, count, loopStart, loopLength);
#if TRACK_SOUNDEFFECTS
            _soundEffects.Add(this);
#endif
#else
            throw new NotImplementedException();
#endif
        }

        #endregion

        #region Additional SoundEffect/SoundEffectInstance Creation Methods

        public SoundEffectInstance CreateInstance()
        {
#if DIRECTX
            SourceVoice voice = null;
            if (Device != null)
                voice = new SourceVoice(Device, _format, VoiceFlags.None, XAudio2.MaximumFrequencyRatio);

            var instance = new SoundEffectInstance(this, voice);
#elif OPENAL || AUDIOTRACK
            var instance = new SoundEffectInstance(this);
#else
            var instance = new SoundEffectInstance();
            instance.Sound = _sound;
#endif
            return instance;
        }

#if !AUDIOTRACK
        public static SoundEffect FromStream(Stream stream)
        {            
            return new SoundEffect(stream);
        }
#endif

        #endregion

        #region Play

        public bool Play()
        {
#if OPENAL || AUDIOTRACK
            return Play(MasterVolume, 0.0f, 0.0f);
#else
            return Play(1.0f, 0.0f, 0.0f);
#endif
        }

        public bool Play(float volume, float pitch, float pan)
        {
            if (isDisposed)
                throw new ObjectDisposedException(GetType().Name);
#if DIRECTX || OPENAL || AUDIOTRACK
            if (MasterVolume > 0.0f)
            {
                // Allocate lists first time we need them.
                if (_availableInstances == null)
                    _availableInstances = new List<SoundEffectInstance>();

#if !AUDIOTRACK
                // Cleanup instances which have finished playing.
                var count = _playingInstances.Count;
                for (int i = count - 1; i >= 0; --i)
                {
                    var inst = _playingInstances[i];
                    if (inst.IsDisposed)
                    {
                        _playingInstances.RemoveAt(i);
                    }
                    else if (inst.State == SoundState.Stopped)
                    {
                        if (inst._autoCreated)
                            inst._effect._availableInstances.Add(inst);
                        _playingInstances.RemoveAt(i);
                    }
                }
#endif

                // Locate a SoundEffectInstance either one already
                // allocated and not in use or allocate a new one.
                SoundEffectInstance instance = null;
                if (_availableInstances.Count > 0)
                {
                    instance = _availableInstances[0];
                    _availableInstances.RemoveAt(0);
                }
                else
                {
                    instance = CreateInstance();
                    instance._autoCreated = true;
                }
#if !AUDIOTRACK
                _playingInstances.Add(instance);
#endif

                instance.Volume = volume;
                instance.Pitch = pitch;
                instance.Pan = pan;
                try
                {
                    //Android.Util.Log.Debug("Mixer", "Instance {0} playing ({1})", _name, instance._id);
                    instance.Play();
                }
                catch (InstancePlayLimitException)
                {
                    _playingInstances.Remove(instance);
                    _availableInstances.Add(instance);
                    return false;
                }
            }

            return true;
#else
            if ( MasterVolume > 0.0f )
            {
                if(_instance == null)
                    _instance = CreateInstance();
                _instance.Volume = volume;
                _instance.Pitch = pitch;
                _instance.Pan = pan;
                _instance.Play();
                return _instance.Sound.Playing;
            }
            return true;
#endif
        }

        #endregion

        #region Public Properties

#if OPENAL
        private TimeSpan _duration = TimeSpan.Zero;
#endif

        public TimeSpan Duration
        {
            get
            {
#if DIRECTX                    
                var sampleCount = _buffer.PlayLength;
                var avgBPS = _format.AverageBytesPerSecond;
                
                return TimeSpan.FromSeconds((float)sampleCount / (float)avgBPS);
#elif AUDIOTRACK
                var sampleCount = _data.Length / 2;
                if (_channels == AudioChannels.Stereo)
                    sampleCount /= 2;
                return TimeSpan.FromSeconds((float)sampleCount / (float)_sampleRate);
#elif OPENAL
                return _duration;
#else
                if ( _sound != null )
                {
                    return new TimeSpan(0,0,(int)_sound.Duration);
                }
                else
                {
                    return new TimeSpan(0);
                }
#endif
            }
        }

        public string Name
        {
            get
            {
                return _name;
            }
            set 
            {
                _name = value;
            }
        }

        #endregion

        #region Static Members

        static float _masterVolume = 1.0f;
        public static float MasterVolume 
        { 
            get
            {
                return _masterVolume;
            }
            set
            {
                if (_masterVolume != value)
                {
                    _masterVolume = value;
                }
#if DIRECTX
                MasterVoice.SetVolume(_masterVolume, 0);
#endif
            }
        }

        static float _distanceScale = 1.0f;
        public static float DistanceScale
        {
            get
            {
                return _distanceScale;
            }
            set
            {
                if (value <= 0f)
                {
                    throw new ArgumentOutOfRangeException ("value of DistanceScale");
                }
                _distanceScale = value;
            }
        }

        static float _dopplerScale = 1f;
        public static float DopplerScale
        {
            get
            {
                return _dopplerScale;
            }
            set
            {
                // As per documenation it does not look like the value can be less than 0
                //   although the documentation does not say it throws an error we will anyway
                //   just so it is like the DistanceScale
                if (value < 0f)
                {
                    throw new ArgumentOutOfRangeException ("value of DopplerScale");
                }
                _dopplerScale = value;
            }
        }

        static float speedOfSound = 343.5f;
        public static float SpeedOfSound
        {
            get
            {
                return speedOfSound;
            }
            set
            {
                speedOfSound = value;
            }
        }

        #endregion

        #region IDisposable Members

        public bool IsDisposed
        {
            get
            {
                return isDisposed;
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
#if DIRECTX || OPENAL || AUDIOTRACK
                    int count = _playingInstances.Count;
                    for (int i = count - 1; i >= 0; --i)
                    {
                        var inst = _playingInstances[i];
                        if (ReferenceEquals(this, inst._effect))
                        {
                            inst.Dispose();
                            _playingInstances.RemoveAt(i);
                        }
                    }
                    if (_availableInstances != null)
                    {
                        foreach (var instance in _availableInstances)
                            instance.Dispose();
                        _availableInstances = null;
                    }
#endif

#if DIRECTX
                    if (_dataStream != null)
                    {
                        _dataStream.Dispose();
                        _dataStream = null;
                    }
#elif AUDIOTRACK
#elif !OPENAL
                    if (_sound != null)
                    {
                        _sound.Dispose();
                        _sound = null;
                    }
#endif
                }
                isDisposed = true;
            }
        }

        #endregion

        #region Additional OpenTK SoundEffect Code

#if OPENAL
        byte[] LoadAudioStream(Stream s, float volume, bool looping)
        {
            ALFormat format;
            int size;
            int freq;
            byte[] data;

            data = AudioLoader.Load(s, out format, out size, out freq);

            Format = format;
            Size = size;
            Rate = freq;
            return data;
        }
#endif

        #endregion

        #region Additional DirectX SoundEffect Code

#if DIRECTX
        internal static XAudio2 Device { get; private set; }
        internal static MasteringVoice MasterVoice { get; private set; }

        private static bool _device3DDirty = true;
        private static Speakers _speakers = Speakers.Stereo;

        // XNA does not expose this, but it exists in X3DAudio.
        [CLSCompliant(false)]
        public static Speakers Speakers
        {
            get
            {
                return _speakers;
            }

            set
            {
                if (_speakers != value)
                {
                    _speakers = value;
                    _device3DDirty = true;
                }
            }
        }

        private static X3DAudio _device3D;

        internal static X3DAudio Device3D
        {
            get
            {
                if (_device3DDirty)
                {
                    _device3DDirty = false;
                    _device3D = new X3DAudio(_speakers);
                }

                return _device3D;
            }
        }

        internal static void InitializeSoundEffect()
        {
            try
            {
                if (Device == null)
                {
#if !WINRT && DEBUG
                    try
                    {
                        //Fails if the XAudio2 SDK is not installed
                        Device = new XAudio2(XAudio2Flags.DebugEngine, ProcessorSpecifier.DefaultProcessor);
                        Device.StartEngine();
                    }
                    catch
#endif
                    {
                        Device = new XAudio2(XAudio2Flags.None, ProcessorSpecifier.DefaultProcessor);
                        Device.StartEngine();
                    }
                }

                // Just use the default device.
#if WINRT
                string deviceId = null;
#else
                const int deviceId = 0;
#endif

                if (MasterVoice == null)
                {
                    // Let windows autodetect number of channels and sample rate.
                    MasterVoice = new MasteringVoice(Device, XAudio2.DefaultChannels, XAudio2.DefaultSampleRate, deviceId);
                    MasterVoice.SetVolume(_masterVolume, 0);
                }

                // The autodetected value of MasterVoice.ChannelMask corresponds to the speaker layout.
#if WINRT
                Speakers = (Speakers)MasterVoice.ChannelMask;
#else
                var deviceDetails = Device.GetDeviceDetails(deviceId);
                Speakers = deviceDetails.OutputFormat.ChannelMask;
#endif
            }
            catch
            {
                // Release the device and null it as
                // we have no audio support.
                if (Device != null)
                {
                    Device.Dispose();
                    Device = null;
                }

                MasterVoice = null;
            }
        }

        private void Initialize(WaveFormat format, byte[] buffer, int offset, int count, int loopStart, int loopLength)
        {
            _format = format;

            _dataStream = DataStream.Create<byte>(buffer, true, false);

            // Use the loopStart and loopLength also as the range
            // when playing this SoundEffect a single time / unlooped.
            _buffer = new AudioBuffer()
            {
                Stream = _dataStream,
                AudioBytes = count,
                Flags = BufferFlags.EndOfStream,
                PlayBegin = loopStart,
                PlayLength = loopLength,
                Context = new IntPtr(42),
            };

            _loopedBuffer = new AudioBuffer()
            {
                Stream = _dataStream,
                AudioBytes = count,
                Flags = BufferFlags.EndOfStream,
                LoopBegin = loopStart,
                LoopLength = loopLength,
                LoopCount = AudioBuffer.LoopInfinite,
                Context = new IntPtr(42),
            };            
        }

        // Does someone actually need to call this if it only happens when the whole
        // game closes? And if so, who would make the call?
        internal static void Shutdown()
        {
            if (MasterVoice != null)
            {
                MasterVoice.DestroyVoice();
                MasterVoice.Dispose();
                MasterVoice = null;
            }

            if (Device != null)
            {
                Device.StopEngine();
                Device.Dispose();
                Device = null;
            }

            _device3DDirty = true;
            _speakers = Speakers.Stereo;
        }
#endif
        #endregion
    }
}

