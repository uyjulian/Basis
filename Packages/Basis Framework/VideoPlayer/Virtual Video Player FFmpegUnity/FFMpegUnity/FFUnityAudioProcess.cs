using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Remoting.Channels;
using System.Threading;
using FFmpeg.Unity.Helpers;
using UnityEngine;
namespace FFmpeg.Unity
{
    [System.Serializable]
    public class FFUnityAudioProcess
    {
        public List<FFUnityAudio> AudioOutput = new List<FFUnityAudio>();
        [SerializeField] public double _audioTimeBuffer = 1d;
        [SerializeField] public double _audioSkipBuffer = 0.25d;
        public int _audioBufferCount = 1;
        public int _audioBufferSize = 128;

        // Circular buffer for each channel's audio stream
        public List<CircularAudioBuffer> _audioStreams = new List<CircularAudioBuffer>();

        public MemoryStream _audioMemStream;
        public int _audioWriteIndex = 0;
        public bool _lastAudioRead = false;
        public int _audioMissCount = 0;
        public double _lastAudioDecodeTime;
        public VideoStreamDecoder _audioDecoder;
        public FFmpegCtx _streamAudioCtx;
        public AudioClip[] _audioClips; // One for each channel
        public AVFrame[] _audioFrames;
        private byte[] backBuffer2;
        private float[] backBuffer3;
        private bool CanSeek;
        public int Channels;
        public void SeekAudio(double seek)
        {
            // Stop all audio output
            FFUnityAudioHelper.StopAll(AudioOutput);

            // Reset stream
            _audioMemStream.Position = 0;
            for (int Index = 0; Index < _audioStreams.Count; Index++)
            {
                _audioStreams[Index].Clear();
            }

            if (CanSeek)
            {
                _streamAudioCtx.Seek(_audioDecoder, seek);
            }

            _audioDecoder?.Seek();

            for (int channelIndex = 0; channelIndex < Channels; channelIndex++)
            {
                FFUnityAudioHelper.PlayAll(AudioOutput, channelIndex, _audioClips[channelIndex]);
            }
        }

        public void DeInitAudio()
        {
            _audioDecoder?.Dispose();
            _streamAudioCtx?.Dispose();
        }
        public void InitAudio(string name, bool CanSeek)
        {
            this.CanSeek = CanSeek;
            if (_streamAudioCtx == null)
            {
                Debug.LogError("_streamAudioCtx was null!");
                return;
            }

            _audioFrames = new AVFrame[_audioBufferCount];
            _audioMemStream = new MemoryStream();
            _audioStreams = new List<CircularAudioBuffer>();
            _audioDecoder = new VideoStreamDecoder(_streamAudioCtx, AVMediaType.AVMEDIA_TYPE_AUDIO);
            Channels = _audioDecoder.Channels;
            _audioClips = new AudioClip[Channels];
            for (int channel = 0; channel < Channels; channel++)
            {
                _audioStreams.Add(new CircularAudioBuffer(_audioDecoder.SampleRate));
                var duplicate = channel;
                _audioClips[channel] = AudioClip.Create($"{name}-AudioClip-{channel}", _audioBufferSize, 1, _audioDecoder.SampleRate, true, (data) => AudioCallback(data, duplicate));

                FFUnityAudioHelper.PlayAll(AudioOutput, channel, _audioClips[channel]);
            }
        }

        public unsafe void AudioCallback(float[] data, int channel)
        {
            var audioStream = _audioStreams[channel];

            try
            {
                audioStream.Read(data);
            }
            catch (Exception ex)
            {
                UnityEngine.Debug.LogError($"AudioCallback error: {ex}");
            }
        }
        public unsafe void UpdateAudio(int idx)
        {
            var audioFrame = _audioFrames[idx];
            if (audioFrame.data[0] == null)
            {
                return;
            }
            int size = ffmpeg.av_samples_get_buffer_size(null, 1, audioFrame.nb_samples, _audioDecoder.SampleFormat, 1);
            if (size == 0)
            {
                UnityEngine.Debug.LogError("audio buffer size is less than zero");
                return;
            }
            if (backBuffer2 == null || backBuffer2.Length != size)
            {
                backBuffer2 = new byte[size];
                backBuffer3 = new float[size / 4];
            }
            for (uint ch = 0; ch < _audioDecoder.Channels; ch++)
            {
                Marshal.Copy((IntPtr)audioFrame.data[ch], backBuffer2, 0, size);
                Buffer.BlockCopy(backBuffer2, 0, backBuffer3, 0, backBuffer2.Length);
                _audioStreams[(int)ch].Write(backBuffer3);
            }
        }

        public bool ShouldDecodeAudio(FFUnity Unity)
        {
            if (_audioDecoder != null && _audioDecoder.CanDecode() && _streamAudioCtx.TryGetTime(_audioDecoder, out var time))
            {
                if (Unity._elapsedOffset + _audioTimeBuffer < time)
                {
                    return false;
                }

                if (Unity._elapsedOffset > time + _audioSkipBuffer && CanSeek)
                {
                    _streamAudioCtx.NextFrame(out _);
                    Unity.skippedFrames++;
                    return false;
                }
            }
            return true;
        }

        public bool TryProcessAudioFrame(ref double time, FFUnity Unity)
        {
            _streamAudioCtx.NextFrame(out _);
           var aud = _audioDecoder.Decode(out var aFrame);

            if (aud == 0)
            {
                if (_streamAudioCtx.TryGetTime(_audioDecoder, aFrame, out time) && Unity._elapsedOffset > time + _audioSkipBuffer && CanSeek)
                {
                    return false;
                }

                if (_streamAudioCtx.TryGetTime(_audioDecoder, aFrame, out time) && time != 0)
                {
                    _lastAudioDecodeTime = time;
                }

                int position = _audioWriteIndex % _audioFrames.Length;
                _audioFrames[position] = aFrame;
                UpdateAudio(position);

                _audioWriteIndex++;
                return true;
            }
            return false;
        }
    }
}