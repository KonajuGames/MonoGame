// MonoGame - Copyright (C) The MonoGame Team
// This file is subject to the terms and conditions defined in
// file 'LICENSE.txt', which is part of this source code package.

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Globalization;


#if MACOS
using MonoMac.AudioToolbox;
#elif WINDOWS
using NAudio.Wave;
using NAudio.MediaFoundation;
using System.Globalization;
#endif

namespace Microsoft.Xna.Framework.Content.Pipeline.Audio
{
    /// <summary>
    /// Encapsulates and provides operations, such as format conversions, on the source audio. This type is produced by the audio importers and used by audio processors to produce compiled audio assets.
    /// </summary>
    public class AudioContent : ContentItem, IDisposable
    {
        internal List<byte> _data;
#if WINDOWS
        WaveStream _reader;
#endif
        TimeSpan _duration;
        string _fileName;
        AudioFileType _fileType;
        AudioFormat _format;
        int _loopLength;
        int _loopStart;
        bool _disposed;

        /// <summary>
        /// Gets the raw audio data.
        /// </summary>
        /// <value>If unprocessed, the source data; otherwise, the processed data.</value>
        public ReadOnlyCollection<byte> Data { get { return _data.AsReadOnly(); } }

        /// <summary>
        /// Gets the duration (in milliseconds) of the audio data.
        /// </summary>
        /// <value>Duration of the audio data.</value>
        public TimeSpan Duration { get { return _duration; } }

        /// <summary>
        /// Gets the file name containing the audio data.
        /// </summary>
        /// <value>The name of the file containing this data.</value>
        [ContentSerializerAttribute]
        public string FileName { get { return _fileName; } }

        /// <summary>
        /// Gets the AudioFileType of this audio source.
        /// </summary>
        /// <value>The AudioFileType of this audio source.</value>
        public AudioFileType FileType { get { return _fileType; } }

        /// <summary>
        /// Gets the AudioFormat of this audio source.
        /// </summary>
        /// <value>The AudioFormat of this audio source.</value>
        public AudioFormat Format { get { return _format; } }

        /// <summary>
        /// Gets the loop length, in samples.
        /// </summary>
        /// <value>The number of samples in the loop.</value>
        public int LoopLength { get { return _loopLength; } }

        /// <summary>
        /// Gets the loop start, in samples.
        /// </summary>
        /// <value>The number of samples to the start of the loop.</value>
        public int LoopStart { get { return _loopStart; } }

        /// <summary>
        /// Initializes a new instance of AudioContent.
        /// </summary>
        /// <param name="audioFileName">Name of the audio source file to be processed.</param>
        /// <param name="audioFileType">Type of the processed audio: WAV, MP3 or WMA.</param>
        /// <remarks>Constructs the object from the specified source file, in the format specified.</remarks>
        public AudioContent(string audioFileName, AudioFileType audioFileType)
        {
            _fileName = audioFileName;
            _fileType = audioFileType;
            Read(_fileName);
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before garbage collection reclaims the object.
        /// </summary>
        ~AudioContent()
        {
            Dispose(false);
        }

        /// <summary>
        /// Returns the sample rate for the given quality setting.  Used for sound effect conversion.
        /// </summary>
        /// <param name="quality">The quality setting.</param>
        /// <returns>The sample rate for the quality.</returns>
        int QualityToSampleRate(ConversionQuality quality)
        {
            switch (quality)
            {
                case ConversionQuality.Low:
                    return Math.Max(8000, _format.SampleRate / 2);
            }

            return Math.Max(8000, _format.SampleRate);
        }

        /// <summary>
        /// Returns the bitrate for the given quality setting.  Used for song conversion.
        /// </summary>
        /// <param name="quality">The quality setting.</param>
        /// <returns>The bitrate for the quality.</returns>
        int QualityToBitRate(ConversionQuality quality)
        {
            switch (quality)
            {
                case ConversionQuality.Low:
                    return 96000;
                case ConversionQuality.Medium:
                    return 128000;
            }

            return 192000;
        }

#if WINDOWS
        public static MediaType SelectMediaType (Guid audioSubtype, WaveFormat inputFormat, int desiredBitRate)
        {
            return MediaFoundationEncoder.GetOutputMediaTypes(audioSubtype)
                .Where(mt => mt.SampleRate >= inputFormat.SampleRate && mt.ChannelCount == inputFormat.Channels)
                .Select(mt => new { MediaType = mt, Delta = Math.Abs(desiredBitRate - mt.AverageBytesPerSecond * 8) })
                .OrderBy(mt => mt.Delta)
                .Select(mt => mt.MediaType)
                .FirstOrDefault();
        }

        /// <summary>
        /// Converts the audio using the specified wave format.
        /// </summary>
        /// <param name="waveFormat">The WaveFormat to use for the conversion.</param>
        void ConvertWav(WaveFormat waveFormat)
        {
            _reader.Position = 0;

            //var mediaTypes = MediaFoundationEncoder.GetOutputMediaTypes(NAudio.MediaFoundation.AudioSubtypes.MFAudioFormat_PCM);
            using (var resampler = new MediaFoundationResampler(_reader, waveFormat))
            {
                using (var outStream = new MemoryStream())
                {
                    // Since we cannot determine ahead of time the number of bytes to be
                    // read, read four seconds worth at a time.
                    byte[] bytes = new byte[_reader.WaveFormat.AverageBytesPerSecond * 4];
                    while (true)
                    {
                        int bytesRead = resampler.Read(bytes, 0, bytes.Length);
                        if (bytesRead == 0)
                            break;
                        outStream.Write(bytes, 0, bytesRead);
                    }
                    _data = new List<byte>(outStream.ToArray());
                    _format = new AudioFormat(waveFormat);
                }
            }
        }
#endif

        /// <summary>
        /// Transcodes the source audio to the target format and quality.
        /// </summary>
        /// <param name="formatType">Format to convert this audio to.</param>
        /// <param name="quality">Quality of the processed output audio. For streaming formats, it can be one of the following: Low (96 kbps), Medium (128 kbps), Best (192 kbps).  For WAV formats, it can be one of the following: Low (11kHz ADPCM), Medium (22kHz ADPCM), Best (44kHz PCM)</param>
        /// <param name="targetFileName">Name of the file containing the processed source audio. Must be null for Wav and Adpcm. Must not be null for streaming compressed formats.</param>
        public void ConvertFormat(ConversionFormat formatType, ConversionQuality quality, string targetFileName)
        {
            if (_disposed)
                throw new ObjectDisposedException("AudioContent");

            if (formatType == ConversionFormat.Pcm || formatType == ConversionFormat.Adpcm || formatType == ConversionFormat.ImaAdpcm)
            {
                if (!String.IsNullOrEmpty(targetFileName))
                    throw new InvalidOperationException("targetFileName must be null for non-streaming formats");
            }

            switch (formatType)
            {
                case ConversionFormat.Adpcm:
#if WINDOWS
                    ConvertWav(new AdpcmWaveFormat(QualityToSampleRate(quality), _format.ChannelCount));
#else
                    {
                        var inputPath = Path.GetTempFileName();
                        using (var input = new FileStream(inputPath, FileMode.Create))
                        {
                            var bytes = _data.ToArray();
                            input.Write(bytes, 0, bytes.Length);
                        }

                        var outputPath = Path.GetTempFileName() + ".wav";

                        var parameters = String.Format(CultureInfo.InvariantCulture, "-t raw -r {0} -e {1} -b {2} -c {3} {4} "
                            , _format.SampleRate
                            , _format.BitsPerSample == 8 ? "unsigned" : "signed"
                            , _format.BitsPerSample
                            , _format.ChannelCount
                            , inputPath);
                        var targetSampleRate = QualityToSampleRate(quality);
                        if (_format.SampleRate != targetSampleRate)
                            parameters += String.Format(CultureInfo.InvariantCulture, "-r {0} ", targetSampleRate);
                        parameters += "-e ms ";
                        parameters += "\"" + outputPath + "\"";
                        var psi = new ProcessStartInfo("sox", parameters);
                        using (var process = Process.Start(psi))
                        {
                            process.WaitForExit();
                            if (process.ExitCode != 0)
                                throw new InvalidContentException("Failed to convert " + _fileName + " to PCM format");
                        }

                        ReadWav(outputPath);
                        File.Delete(inputPath);
                        File.Delete(outputPath);
                    }
#endif
                    break;

                case ConversionFormat.Pcm:
#if WINDOWS
                    ConvertWav(new WaveFormat(QualityToSampleRate(quality), _format.ChannelCount));
#elif LINUX
                    {
                        var inputPath = Path.GetTempFileName();
                        using (var input = new FileStream(inputPath, FileMode.Create))
                        {
                            var bytes = _data.ToArray();
                            input.Write(bytes, 0, bytes.Length);
                        }

                        var outputPath = Path.GetTempFileName() + ".wav";

                        var parameters = String.Format(CultureInfo.InvariantCulture, "-t raw -r {0} -e {1} -b {2} -c {3} \"{4}\" "
                            , _format.SampleRate
                            , _format.BitsPerSample == 8 ? "unsigned" : "signed"
                            , _format.BitsPerSample
                            , _format.ChannelCount
                            , inputPath);
                        var targetSampleRate = QualityToSampleRate(quality);
                        if (_format.SampleRate != targetSampleRate)
                            parameters += String.Format(CultureInfo.InvariantCulture, "-r {0} ", targetSampleRate);
                        if (_format.BitsPerSample != 16)
                            parameters += "-e signed -b 16 ";
                        parameters += "\"" + outputPath + "\"";
                        var psi = new ProcessStartInfo("sox", parameters);
                        try
                        {
                            using (var process = Process.Start(psi))
                            {
                                process.WaitForExit();
                                if (process.ExitCode != 0)
                                    throw new PipelineException("Failed to convert " + _fileName + " to PCM format");
                            }
                        }
                        catch (System.ComponentModel.Win32Exception e)
                        {
                            throw new PipelineException("Failed to launch 'sox' utility for sound conversion. " + e.Message);
                        }
                        ReadWav(outputPath);
                        File.Delete(inputPath);
                        File.Delete(outputPath);
                    }
#else
                    targetFileName = Path.GetTempFileName();
                    if (!ConvertAudio.Convert(_fileName, targetFileName, AudioFormatType.LinearPCM, MonoMac.AudioToolbox.AudioFileType.WAVE, quality))
                        throw new InvalidDataException("Failed to convert to PCM");
                    Read(targetFileName);
                    File.Delete(targetFileName);
#endif
                    break;

                case ConversionFormat.WindowsMedia:
#if WINDOWS
                    _reader.Position = 0;
                    MediaFoundationEncoder.EncodeToWma(_reader, targetFileName, QualityToBitRate(quality));
                    break;
#else
                    throw new NotSupportedException("WindowsMedia encoding supported on Windows only");
#endif

                case ConversionFormat.Xma:
                    throw new NotSupportedException("XMA is not a supported encoding format. It is specific to the Xbox 360.");

                case ConversionFormat.ImaAdpcm:
#if WINDOWS
                    ConvertWav(new ImaAdpcmWaveFormat(QualityToSampleRate(quality), _format.ChannelCount, 4));
#else
                    {
                        var inputPath = Path.GetTempFileName();
                        using (var input = new FileStream(inputPath, FileMode.Create))
                        {
                            var bytes = _data.ToArray();
                            input.Write(bytes, 0, bytes.Length);
                        }

                        var outputPath = Path.GetTempFileName() + ".wav";

                        var parameters = String.Format(CultureInfo.InvariantCulture, "-t raw -r {0} -e {1} -b {2} -c {3} {4} "
                            , _format.SampleRate
                            , _format.BitsPerSample == 8 ? "unsigned" : "signed"
                            , _format.BitsPerSample
                            , _format.ChannelCount
                            , inputPath);
                        var targetSampleRate = QualityToSampleRate(quality);
                        if (_format.SampleRate != targetSampleRate)
                            parameters += String.Format(CultureInfo.InvariantCulture, "-r {0} ", targetSampleRate);
                        parameters += "-e ima ";
                        parameters += "\"" + outputPath + "\"";
                        var psi = new ProcessStartInfo("sox", parameters);
                        using (var process = Process.Start(psi))
                        {
                            process.WaitForExit();
                            if (process.ExitCode != 0)
                                throw new InvalidContentException("Failed to convert " + _fileName + " to PCM format");
                        }

                        ReadWav(outputPath);
                        File.Delete(inputPath);
                        File.Delete(outputPath);
                    }
#endif
                    break;

                case ConversionFormat.Aac:
#if WINDOWS
                    _reader.Position = 0;
                    var mediaType = SelectMediaType(AudioSubtypes.MFAudioFormat_AAC, _reader.WaveFormat, QualityToBitRate(quality));
                    if (mediaType == null)
                        throw new InvalidDataException("Cound not find a suitable mediaType to convert to.");
                    using (var encoder = new MediaFoundationEncoder(mediaType))
                        encoder.Encode(targetFileName, _reader);
#elif LINUX
                    using (var process = Process.Start("sox", _fileName + " " + targetFileName))
                    {
                        process.WaitForExit();
                        if (process.ExitCode != 0)
                            throw new InvalidContentException("Failed to convert " + _fileName + " to AAC format");
                    }
#else
                    if (!ConvertAudio.Convert(fileName, targetFileName, AudioFormatType.MPEG4AAC, MonoMac.AudioToolbox.AudioFileType.MPEG4, quality))
                        throw new InvalidDataException("Failed to convert to AAC");
#endif
                    break;

                case ConversionFormat.Vorbis:
                    throw new NotImplementedException("Vorbis is not yet implemented as an encoding format.");
            }
        }

        /// <summary>
        /// Immediately releases the managed and unmanaged resources used by this object.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Immediately releases the unmanaged resources used by this object.
        /// </summary>
        /// <param name="disposing">True if disposing of the managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
#if WINDOWS
                    // Release managed resources
                    if (_reader != null)
                        _reader.Dispose();
                    _reader = null;
#endif
                }
                _disposed = true;
            }
        }

#if WINDOWS
        /// <summary>
        /// Read an audio file.
        /// </summary>
        void Read(string fileName)
        {
            _reader = new MediaFoundationReader(fileName);
            _duration = _reader.TotalTime;
            _format = new AudioFormat(_reader.WaveFormat);

            var bytes = new byte[_reader.Length];
            var read = _reader.Read(bytes, 0, bytes.Length);
            _data = new List<byte>(bytes);
        }
#else
        void Read(string fileName)
        {
            if (_fileType == AudioFileType.Wav)
                ReadWav(fileName);
            else
                ReadOther(fileName);
        }

        void ReadWav(string fileName)
        {
            UInt32 dataSize;
            using (var fs = new FileStream(fileName, FileMode.Open))
            {
                using (var reader = new BinaryReader(fs))
                {
					/*var riffID =*/ reader.ReadBytes(4);
                    var fileSize = reader.ReadInt32();
					/*var wavID =*/ reader.ReadBytes(4);
					/*var fmtID =*/ reader.ReadBytes(4);
					/*var fmtSize =*/ reader.ReadUInt32();
                    var fmtCode = reader.ReadUInt16();
                    var channels = reader.ReadUInt16();
                    var sampleRate = reader.ReadUInt32();
                    var fmtAvgBPS = reader.ReadUInt32();
                    var fmtBlockAlign = reader.ReadUInt16();
                    var bitDepth = reader.ReadUInt16();

                    string dataId = "data";
                    int index =0;
                    char c;
                    while (true)
                    {
                        c = reader.ReadChar();
                        if (c == dataId[index])
                            index++;
                        else
                            index = 0;
                        if (index == dataId.Length)
                            break;
                    }

                    dataSize = reader.ReadUInt32();
                    this._data = reader.ReadBytes((int)dataSize).ToList();

                    using (var ms = new MemoryStream())
                    {
                        using (var writer = new BinaryWriter(ms))
                        {
                            writer.Write((int)18);
                            writer.Write((short)fmtCode);
                            writer.Write((short)channels);
                            writer.Write((int)sampleRate);
                            writer.Write((int)fmtAvgBPS);
                            writer.Write((short)fmtBlockAlign);
                            writer.Write((short)bitDepth);
                            writer.Write((short)0);
                        }

                        _format = new AudioFormat(ms.ToArray(), (int)bitDepth, (int)fmtBlockAlign, (int)channels, (int)fmtCode, (int)sampleRate);
                    }
                    this._duration = new TimeSpan(0, 0, (int)(fileSize / (sampleRate * channels * bitDepth / 8)));
                }
            }
        }

        void ReadOther(string fileName)
        {
            var outputPath = Path.GetTempFileName() + ".wav";

            var parameters = "\"" + fileName + "\" \"" + outputPath + "\"";
            var psi = new ProcessStartInfo("sox", parameters);
            using (var process = Process.Start(psi))
            {
                process.WaitForExit();
                if (process.ExitCode != 0)
                    throw new InvalidContentException("Failed to convert " + _fileName + " to PCM format");
            }

            ReadWav(outputPath);
            File.Delete(outputPath);
        }
#endif
    }
}
