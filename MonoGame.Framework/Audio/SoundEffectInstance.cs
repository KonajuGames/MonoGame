#region License
// /*
// Microsoft Public License (Ms-PL)
// MonoGame - Copyright © 2009 The MonoGame Team
// 
// All rights reserved.
// 
// This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
// accept the license, do not use the software.
// 
// 1. Definitions
// The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
// U.S. copyright law.
// 
// A "contribution" is the original software, or any additions or changes to the software.
// A "contributor" is any person that distributes its contribution under this license.
// "Licensed patents" are a contributor's patent claims that read directly on its contribution.
// 
// 2. Grant of Rights
// (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
// (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
// 
// 3. Conditions and Limitations
// (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
// (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
// your patent license from such contributor to the software ends automatically.
// (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
// notices that are present in the software.
// (D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
// a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
// code form, you may only do so under a license that complies with this license.
// (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
// or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
// permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
// purpose and non-infringement.
// */
#endregion License

#region Using Statements
using System;
#if DIRECTX
using SharpDX.XAudio2;
using SharpDX.X3DAudio;
using SharpDX.Multimedia;
#elif AUDIOTRACK
using Android.Media;
#else
using System.IO;
#endif
#endregion Statements

namespace Microsoft.Xna.Framework.Audio
{
	public class SoundEffectInstance : IDisposable
	{
		private bool isDisposed = false;
#if !DIRECTX && !AUDIOTRACK
        private SoundState soundState = SoundState.Stopped;
#endif

#if DIRECTX        
        private SourceVoice _voice;
        internal SoundEffect _effect;

        private bool _paused;
        private bool _loop;
#elif AUDIOTRACK
        AudioTrack _audioTrack;
        internal SoundEffect _effect;
        float _volume;
        bool _loop;
        float _pan;
        float _pitch;
#else
        private Sound _sound;
		internal Sound Sound 
		{ 
			get
			{
				return _sound;
			} 
			
			set
			{
				_sound = value;
			} 
		}
#endif

#if DIRECTX
        internal SoundEffectInstance(SoundEffect effect, SourceVoice voice)
        {
            _effect = effect;
            _voice = voice;
        }
#elif AUDIOTRACK
        internal SoundEffectInstance(SoundEffect soundEffect)
        {
            _effect = soundEffect;
        }
#else
        internal SoundEffectInstance()
        {
        }

        /* Creates a standalone SoundEffectInstance from given wavedata. */
        internal SoundEffectInstance(byte[] buffer, int sampleRate, int channels)
        {
            // buffer should contain 16-bit PCM wave data
            short bitsPerSample = 16;

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
                writer.Write((int)buffer.Length); //data size

                writer.Write(buffer);

                _sound = new Sound(mStream.ToArray(), 1.0f, false);
                _sound.Rate = sampleRate;
            }
        }
#endif

        ~SoundEffectInstance()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
		}
		
        protected virtual void Dispose(bool disposing)
        {
            if (!isDisposed)
            {
                if (disposing)
                {
#if DIRECTX
                    if (_voice != null)
                    {
                        _voice.DestroyVoice();
                        _voice.Dispose();
                        _voice = null;
                    }
		            _effect = null;
#elif AUDIOTRACK
                    if (_audioTrack != null)
                    {
                        _audioTrack.Release();
                        _audioTrack.Dispose();
                        _audioTrack = null;
                    }
                    _effect = null;
#else
                    // When disposing a SoundEffectInstance, the Sound should
                    // just be stopped as it will likely be reused later
                    _sound.Stop();
#endif
                }
                isDisposed = true;
            }
        }

		public void Apply3D(AudioListener listener, AudioEmitter emitter)
        {
#if DIRECTX	
            // If we have no voice then nothing to do.
            if (_voice == null)
                return;

            // Convert from XNA Emitter to a SharpDX Emitter
            var e = emitter.ToEmitter();
            e.CurveDistanceScaler = SoundEffect.DistanceScale;
            e.DopplerScaler = SoundEffect.DopplerScale;
            e.ChannelCount = _effect._format.Channels;

            // Convert from XNA Listener to a SharpDX Listener
            var l = listener.ToListener();                        
            
            // Number of channels in the sound being played.
            // Not actually sure if XNA supported 3D attenuation of sterio sounds, but X3DAudio does.
            var srcChannelCount = _effect._format.Channels;            

            // Number of output channels.
            var dstChannelCount = SoundEffect.MasterVoice.VoiceDetails.InputChannelCount;

            // XNA supports distance attenuation and doppler.            
            var dpsSettings = SoundEffect.Device3D.Calculate(l, e, CalculateFlags.Matrix | CalculateFlags.Doppler, srcChannelCount, dstChannelCount);

            // Apply Volume settings (from distance attenuation) ...
            _voice.SetOutputMatrix(SoundEffect.MasterVoice, srcChannelCount, dstChannelCount, dpsSettings.MatrixCoefficients, 0);

            // Apply Pitch settings (from doppler) ...
            _voice.SetFrequencyRatio(dpsSettings.DopplerFactor);
#endif
        }
		
		public void Apply3D(AudioListener[] listeners,AudioEmitter emitter)
		{
            foreach ( var l in listeners )
                Apply3D(l, emitter);            
		}		
		
		public void Pause()
        {
#if DIRECTX         
            if (_voice != null)
                _voice.Stop();
            _paused = true;
#elif AUDIOTRACK
            if (_audioTrack != null)
                _audioTrack.Pause();
#else
            if ( _sound != null )
			{
				_sound.Pause();
                soundState = SoundState.Paused;
			}
#endif
		}
		
		public void Play()
        {
            if (State == SoundState.Playing)
                return;

            if (State == SoundState.Paused)
            {
                Resume();
                return;
            }

#if DIRECTX
            if (_voice != null)
            {
                // Choose the correct buffer depending on if we are looped.            
                var buffer = _loop ? _effect._loopedBuffer : _effect._buffer;

                if (_voice.State.BuffersQueued > 0)
                {
                    _voice.Stop();
                    _voice.FlushSourceBuffers();
                }

                _voice.SubmitSourceBuffer(buffer, null);
                _voice.Start();
            }

		    _paused = false;
#elif AUDIOTRACK
            if (_audioTrack == null)
            {
                _audioTrack = new AudioTrack(Stream.Music, _effect._sampleRate, _effect._channelConfig, Encoding.Pcm16bit, _effect._data.Length, AudioTrackMode.Static);
                var bytesWritten = _audioTrack.Write(_effect._data, 0, _effect._data.Length);
                if (bytesWritten > 0)
                {
                    float left = _volume * (_pan > 0.0f ? 1.0f - _pan : 1.0f);
                    float right = _volume * (_pan < 0.0f ? 1.0f + _pan : 1.0f);
                    _audioTrack.SetStereoVolume(left, right);
                    if (_loop)
                        _audioTrack.SetLoopPoints(0, _effect._frames, -1);
                    if (_pitch != 0.0f)
                    {
                        float convertedPitch = XnaPitchToAlPitch(_pitch);
                        _audioTrack.SetPlaybackRate((int)((float)_effect._sampleRate * convertedPitch));
                    }
                    _audioTrack.Play();
                }
                else
                {
                    _audioTrack.Release();
                    _audioTrack.Dispose();
                    _audioTrack = null;
                    throw new InstancePlayLimitException();
                }
            }
            else
            {
                if (_audioTrack.PlayState == PlayState.Stopped)
                    _audioTrack.ReloadStaticData();
                _audioTrack.Play();
            }
#else
            if ( _sound != null )
			{
				if (soundState == SoundState.Paused)
					_sound.Resume();
				else
					_sound.Play();
				soundState = SoundState.Playing;
			}
#endif
		}

		/// <summary>
		/// Tries to play the sound, returns true if successful
		/// </summary>
		/// <returns></returns>
		internal bool TryPlay()
		{
			Play();
			return true;
		}

		public void Resume()
        {
#if DIRECTX
            if (_voice != null)
            {
                // Restart the sound if (and only if) it stopped playing
                if (!_loop)
                {
                    if (_voice.State.BuffersQueued == 0)
                    {
                        _voice.Stop();
                        _voice.FlushSourceBuffers();
                        _voice.SubmitSourceBuffer(_effect._buffer, null);
                    }
                }
                _voice.Start();
            }
            _paused = false;
#elif AUDIOTRACK
            if (_audioTrack != null)
            {
                if (_audioTrack.PlayState == PlayState.Paused)
                    _audioTrack.Play();
            }
#else
            if ( _sound != null )
			{
				if (soundState == SoundState.Paused)
				{
                    _sound.Resume();
                }
				soundState = SoundState.Playing;
 			}
#endif
		}
		
		public void Stop()
        {
#if DIRECTX
            if (_voice != null)
            {
                _voice.Stop(0);
                _voice.FlushSourceBuffers();
            }

		    _paused = false;
#elif AUDIOTRACK
            if (_audioTrack != null)
            {
                if (_audioTrack.PlayState != PlayState.Stopped)
                    _audioTrack.Stop();
            }
#else
            if ( _sound != null )
			{
                _sound.Stop();
                soundState = SoundState.Stopped;
			}
#endif
        }

        public void Stop(bool immediate)
        {
#if DIRECTX            
            if (_voice != null)
                _voice.Stop(immediate ? 0 : (int)PlayFlags.Tails);

            _paused = false;
#elif AUDIOTRACK
            if (_audioTrack != null)
            {
                if (_audioTrack.PlayState != PlayState.Stopped)
                    _audioTrack.Stop();
            }
#else
            if ( _sound != null )
			{
                _sound.Stop();
				soundState = SoundState.Stopped;
			}
#endif
        }		
		
#if AUDIOTRACK
        internal void Recycle()
        {
            if (_audioTrack != null)
            {
                _audioTrack.Release();
                _audioTrack.Dispose();
                _audioTrack = null;
            }
        }

        private float XnaPitchToAlPitch(float xnaPitch)
        {
            /* 
            XNA sets pitch bounds to [-1.0f, 1.0f], each end being one octave.
             -OpenAL's AL_PITCH boundaries are (0.0f, INF). *
             -Consider the function f(x) = 2 ^ x
             -The domain is (-INF, INF) and the range is (0, INF). *
             -0.0f is the original pitch for XNA, 1.0f is the original pitch for OpenAL.
             -Note that f(0) = 1, f(1) = 2, f(-1) = 0.5, and so on.
             -XNA's pitch values are on the domain, OpenAL's are on the range.
             -Remember: the XNA limit is arbitrarily between two octaves on the domain. *
             -To convert, we just plug XNA pitch into f(x). 
                    */
            return (float)Math.Pow(2, xnaPitch);
        }
#endif

		public bool IsDisposed 
		{ 
			get
			{
				return isDisposed;
			}
		}
		
		public bool IsLooped 
		{ 
			get
            {
#if DIRECTX || AUDIOTRACK
                return _loop;
#else
                if ( _sound != null )
				{
					return _sound.Looping;
				}
				else
				{
					return false;
				}
#endif
			}
			
			set
            {
#if DIRECTX
                _loop = value;
#elif AUDIOTRACK
                if (_loop != value)
                {
                    _loop = value;
                    if (_audioTrack != null)
                    {
                        if (_loop)
                            _audioTrack.SetLoopPoints(0, _effect._frames, -1);
                        else
                            _audioTrack.SetLoopPoints(0, 0, 0);
                    }
                }
#else
                if ( _sound != null )
				{
					if ( _sound.Looping != value )
					{
						_sound.Looping = value;
					}
				}
#endif
			}
		}

#if DIRECTX
        private float _pan;
        private static float[] _panMatrix;
#endif

        public float Pan 
		{ 
			get
            {
#if DIRECTX || AUDIOTRACK
                return _pan;
#else
                if ( _sound != null )
				{
					return _sound.Pan;
				}
				else
				{
					return 0.0f;
				}
#endif
			}
			
			set
            {
#if DIRECTX       
                // According to XNA documentation:
                // "Panning, ranging from -1.0f (full left) to 1.0f (full right). 0.0f is centered."
                _pan = MathHelper.Clamp(value, -1.0f, 1.0f);

                // If we have no voice then nothing more to do.
                if (_voice == null)
                    return;
                
                var srcChannelCount = _effect._format.Channels;
                var dstChannelCount = SoundEffect.MasterVoice.VoiceDetails.InputChannelCount;
                
                if ( _panMatrix == null || _panMatrix.Length < dstChannelCount )
                    _panMatrix = new float[Math.Max(dstChannelCount,8)];                

                // Default to full volume for all channels/destinations   
                for (var i = 0; i < _panMatrix.Length; i++)
                    _panMatrix[i] = 1.0f;

                // From X3DAudio documentation:
                /*
                    For submix and mastering voices, and for source voices without a channel mask or a channel mask of 0, 
                       XAudio2 assumes default speaker positions according to the following table. 

                    Channels

                    Implicit Channel Positions

                    1 Always maps to FrontLeft and FrontRight at full scale in both speakers (special case for mono sounds) 
                    2 FrontLeft, FrontRight (basic stereo configuration) 
                    3 FrontLeft, FrontRight, LowFrequency (2.1 configuration) 
                    4 FrontLeft, FrontRight, BackLeft, BackRight (quadraphonic) 
                    5 FrontLeft, FrontRight, FrontCenter, SideLeft, SideRight (5.0 configuration) 
                    6 FrontLeft, FrontRight, FrontCenter, LowFrequency, SideLeft, SideRight (5.1 configuration) (see the following remarks) 
                    7 FrontLeft, FrontRight, FrontCenter, LowFrequency, SideLeft, SideRight, BackCenter (6.1 configuration) 
                    8 FrontLeft, FrontRight, FrontCenter, LowFrequency, BackLeft, BackRight, SideLeft, SideRight (7.1 configuration) 
                    9 or more No implicit positions (one-to-one mapping)                      
                 */

                // Notes:
                //
                // Since XNA does not appear to expose any 'master' voice channel mask / speaker configuration,
                // I assume the mappings listed above should be used.
                //
                // Assuming it is correct to pan all channels which have a left/right component.

                var lVal = 1.0f - _pan;
                var rVal = 1.0f + _pan;
                                
                switch (SoundEffect.Speakers)
                {
                    case Speakers.Stereo:
                    case Speakers.TwoPointOne:
                    case Speakers.Surround:
                        _panMatrix[0] = lVal;
                        _panMatrix[1] = rVal;
                        break;

                    case Speakers.Quad:
                        _panMatrix[0] = _panMatrix[2] = lVal;
                        _panMatrix[1] = _panMatrix[3] = rVal;
                        break;

                    case Speakers.FourPointOne:
                        _panMatrix[0] = _panMatrix[3] = lVal;
                        _panMatrix[1] = _panMatrix[4] = rVal;
                        break;

                    case Speakers.FivePointOne:
                    case Speakers.SevenPointOne:
                    case Speakers.FivePointOneSurround:
                        _panMatrix[0] = _panMatrix[4] = lVal;
                        _panMatrix[1] = _panMatrix[5] = rVal;
                        break;

                    case Speakers.SevenPointOneSurround:
                        _panMatrix[0] = _panMatrix[4] = _panMatrix[6] = lVal;
                        _panMatrix[1] = _panMatrix[5] = _panMatrix[7] = rVal;
                        break;

                    case Speakers.Mono:
                    default:
                        // don't do any panning here   
                        break;
                }

                _voice.SetOutputMatrix(srcChannelCount, dstChannelCount, _panMatrix);

#elif AUDIOTRACK
                // According to XNA documentation:
                // "Panning, ranging from -1.0f (full left) to 1.0f (full right). 0.0f is centered."
                float clamped = MathHelper.Clamp(value, -1.0f, 1.0f);
                if (clamped != _pan)
                {
                    _pan = clamped;
                    if (_audioTrack != null)
                    {
                        float left = _volume * (_pan > 0.0f ? 1.0f - _pan : 1.0f);
                        float right = _volume * (_pan < 0.0f ? 1.0f + _pan : 1.0f);
                        _audioTrack.SetStereoVolume(left, right);
                    }
                }
#else
                if ( _sound != null )
				{
					if ( _sound.Pan != value )
					{
						_sound.Pan = value;
					}
				}
#endif
            }
		}
		
		public float Pitch         
		{             
            get
            {
#if DIRECTX
                    if (_voice == null)
                        return 0.0f;

                    // NOTE: This is copy of what XAudio2.FrequencyRatioToSemitones() does
                    // which avoids the native call and is actually more accurate.
                    var pitch = 39.86313713864835 * Math.Log10(_voice.FrequencyRatio);

                    // Convert from semitones to octaves.
                    pitch /= 12.0;

                    return (float)pitch;
#elif AUDIOTRACK
                return _pitch;
#else
                if ( _sound != null)
				    {
	                   return _sound.Rate;
				    }
				    return 0.0f;
#endif
            }
            set
            {
#if DIRECTX
                    if (_voice == null)
                        return;

                    // NOTE: This is copy of what XAudio2.SemitonesToFrequencyRatio() does
                    // which avoids the native call and is actually more accurate.
                    var ratio = Math.Pow(2.0, value);
                    _voice.SetFrequencyRatio((float)ratio);
#elif AUDIOTRACK
                float clamped = MathHelper.Clamp(value, -1.0f, 1.0f);
                if (clamped != _pitch)
                {
                    _pitch = clamped;
                    if (_audioTrack != null)
                    {
                        float convertedPitch = XnaPitchToAlPitch(_pitch);
                        _audioTrack.SetPlaybackRate((int)((float)_effect._sampleRate * convertedPitch));
                    }
                }
#else
                if ( _sound != null && _sound.Rate != value)
				    {
	                   _sound.Rate = value;
				    } 
#endif
	            }        
		 }				
		
		public SoundState State 
		{ 
			get
            {
#if DIRECTX           
                // If no voice or no buffers queued the sound is stopped.
                if (_voice == null || _voice.State.BuffersQueued == 0)
                    return SoundState.Stopped;
                
                // Because XAudio2 does not actually provide if a SourceVoice is Started / Stopped
                // we have to save the "paused" state ourself.
                if (_paused)
                    return SoundState.Paused;

                return SoundState.Playing;
#elif AUDIOTRACK
                if (_audioTrack != null)
                {
                    switch (_audioTrack.PlayState)
                    {
                        case PlayState.Paused:
                            return SoundState.Paused;
                        case PlayState.Playing:
                            if (_audioTrack.PlaybackHeadPosition < _effect._frames)
                                return SoundState.Playing;
                            return SoundState.Stopped;
                    }
                }
                return SoundState.Stopped;
#else
                if (_sound != null && soundState == SoundState.Playing && !_sound.Playing) 
                {
                    soundState = SoundState.Stopped;
                }

                return soundState;
#endif
			} 
		}
		
		public float Volume
		{ 
			get
            {
#if DIRECTX
                if (_voice == null)
                    return 0.0f;
                else
                    return _voice.Volume;
#elif AUDIOTRACK
                return _volume;
#else
                if (_sound != null)
				{
					return _sound.Volume;
				}
				else
				{
					return 0.0f;
				}
#endif
			}
			
			set
            {
#if DIRECTX
                if (_voice != null)
                    _voice.SetVolume(value, XAudio2.CommitNow);
#elif AUDIOTRACK
                float clamped = MathHelper.Clamp(value, 0.0f, 1.0f);
                if (clamped != _volume)
                {
                    _volume = clamped;
                    if (_audioTrack != null)
                    {
                        float left = _volume * (_pan > 0.0f ? 1.0f - _pan : 1.0f);
                        float right = _volume * (_pan < 0.0f ? 1.0f + _pan : 1.0f);
                        _audioTrack.SetStereoVolume(left, right);
                    }
                }
#else
                if ( _sound != null )
				{
					if ( _sound.Volume != value )
					{
						_sound.Volume = value;
					}
				}
#endif
			}
		}	
	}
}
