using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using FFmpeg.Unity.Helpers;
using UnityEngine;
namespace FFmpeg.Unity
{
    public class FFUnity : MonoBehaviour
    {
        // Video-related fields
        [SerializeField] public bool _paused;
        public bool IsPaused => _paused;
        private bool _wasPaused = false;
        [SerializeField] public bool CanSeek = true;

        // Time controls
        [SerializeField] public double _offset = 0.0d;
        [SerializeField] public double _videoOffset = 0.0d;
        private double _prevTime = 0.0d;
        private double _timeOffset = 0.0d;
        private Stopwatch _videoWatch;
        private double? _lastPts;
        private int? _lastPts2;
        public double PlaybackTime => _lastVideoTex?.pts ?? _elapsedOffset;
        public double _elapsedTotalSeconds => _videoWatch?.Elapsed.TotalSeconds ?? 0d;
        public double _elapsedOffsetVideo => _elapsedTotalSeconds + _videoOffset - _timeOffset;
        public double _elapsedOffset => _elapsedTotalSeconds - _timeOffset;
        private double? seekTarget = null;

        // Video buffer controls
        [SerializeField] public double _videoTimeBuffer = 1d;
        [SerializeField] public double _videoSkipBuffer = 0.25d;
        private int _videoBufferCount = 4;

        // Unity assets (video textures)
        private Queue<TexturePool.TexturePoolState> _videoTextures;
        private TexturePool.TexturePoolState _lastVideoTex;
        private TexturePool _texturePool;
        private FFTexData _lastTexData;
        private MaterialPropertyBlock propertyBlock;
        // Decoders and video processing

        [SerializeField] public AVHWDeviceType _hwType = AVHWDeviceType.AV_HWDEVICE_TYPE_NONE;
        private FFmpegCtx _streamVideoCtx;
        private VideoStreamDecoder _videoDecoder;
        private VideoFrameConverter _videoConverter;
        private ConcurrentQueue<FFTexData> _videoFrameClones;
        private Thread _decodeThread;

        // Video frame buffers
        private AVFrame[] _videoFrames;
        private int _videoWriteIndex = 0;
        private double _lastVideoDecodeTime;
        [NonSerialized] public int skippedFrames = 0;
        public double VideoCatchupMultiplier = 5;
        public int LastTotalSize;
        public int synchronizingmaxIterations = 16;
        public double VideoUpdate = 0.25d;
        public int ColorCount = 3;

        // Unity texture generation
        [SerializeField] public FFUnityTextureGeneration unityTextureGeneration = new FFUnityTextureGeneration();

        // Audio processing
        [SerializeField] public FFUnityAudioProcess AudioProcessing = new FFUnityAudioProcess();
        public FFTexDataPool _ffTexDataPool;
        public double FallbackFramerate = 30d;
        public double targetFps;
        private int iterations;
        public void DeInit()
        {
            _paused = true;
            if (_decodeThread != null && _decodeThread.IsAlive)
            {
                _decodeThread.Abort();
            }
            _videoConverter?.Dispose();
            if (_videoFrameClones != null)
            {
                _videoFrameClones.Clear();
            }
            if (_videoTextures != null)
            {
                foreach (TexturePool.TexturePoolState tex in _videoTextures)
                {
                    _texturePool.Release(tex);
                }

                _videoTextures.Clear();
            }
            _texturePool?.Dispose();
            _videoDecoder?.Dispose();
            _streamVideoCtx?.Dispose();

            Texture2D.Destroy(unityTextureGeneration.texture);
            if (AudioProcessing != null)
            {
                AudioProcessing.DeInitAudio();
            }
            else
            {
                UnityEngine.Debug.LogWarning("Audio Processing was null cant release!");
            }
        }
        private void OnEnable()
        {
            _ffTexDataPool = new FFTexDataPool();
            _paused = false;
        }
        private void OnDisable()
        {
            _paused = true;
        }
        private void OnDestroy()
        {
            JoinThreads();
            DeInit();
        }
        public void JoinThreads()
        {
            _paused = true;
            try
            {
                UnityEngine.Debug.Log("Joining Video Threads");
               _decodeThread?.Join();
            }
            catch (ThreadInterruptedException ex)
            {
                UnityEngine.Debug.LogError($"Thread interrupted: {ex.Message}");
            }
        }
        public void Seek(double seek)
        {
            UnityEngine.Debug.Log(nameof(Seek));
            _paused = true;
            seekTarget = seek;
        }

        // Handles seeking in video
        private void SeekVideo(double seek)
        {
            // Clear video frame clones and textures
            _videoFrameClones.Clear();
            foreach (TexturePool.TexturePoolState tex in _videoTextures)
            {
                _texturePool.Release(tex);
            }
            _videoTextures.Clear();

            // Reset video tracking variables
            _lastVideoTex = null;
            _videoWatch.Restart();
            ResetTimers();
            _timeOffset = -seek;
            _prevTime = _offset;
            _lastPts = null;
            _lastPts2 = null;

            // Seek video decoder if seeking is enabled
            if (CanSeek)
            {
                _streamVideoCtx.Seek(_videoDecoder, seek);
            }

            // Explicitly seek video decoder
            _videoDecoder.Seek();
        }
        public void PlayAsync(Stream video, Stream audio)
        {
            JoinThreads();
            DeInit();
            // Initialize video context
            _streamVideoCtx = new FFmpegCtx(video);
            AudioProcessing._streamAudioCtx = new FFmpegCtx(audio);
            Init();
        }
        public void PlayAsync(string urlV, string urlA)
        {
            JoinThreads();
            DeInit();
            // Initialize video context
            _streamVideoCtx = new FFmpegCtx(urlV);
            AudioProcessing._streamAudioCtx = new FFmpegCtx(urlA);
            Init();
        }
        public void Resume()
        {
            if (!CanSeek)
            {
                Init();
            }
            _paused = false;
        }
        public void Pause()
        {
            _paused = true;
        }
        private void ResetTimers()
        {
            _videoWriteIndex = 0;
            AudioProcessing._audioWriteIndex = 0;
            _lastPts = null;
            _lastPts2 = null;
            _offset = 0d;
            _prevTime = 0d;
            _timeOffset = 0d;
        }
        private void Init()
        {
            _paused = true;
            // Stopwatches are more accurate than Time.timeAsDouble(?)
            _videoWatch = new Stopwatch();

            ResetTimers();

            unityTextureGeneration.InitializeTexture();

            InitVideo();
            AudioProcessing.InitAudio(nameof(this.gameObject), CanSeek);

            UnityEngine.Debug.Log(nameof(PlayAsync));
            Seek(0d);
        }
        private void InitVideo()
        {
            // pre-allocate buffers, prevent the C# GC from using CPU
            _texturePool = new TexturePool(_videoBufferCount);
            _videoTextures = new Queue<TexturePool.TexturePoolState>(_videoBufferCount);
            _lastVideoTex = null;
            _videoFrames = new AVFrame[_videoBufferCount];
            _videoFrameClones = new ConcurrentQueue<FFTexData>();
            _videoDecoder = new VideoStreamDecoder(_streamVideoCtx, AVMediaType.AVMEDIA_TYPE_VIDEO, _hwType);
        }
        private void LateUpdate()
        {
            if (!IsInitialized())
            {
                return;
            }
            if (CanSeek && IsStreamAtEnd() && !_paused)
            {
                Pause();
            }
            if (seekTarget.HasValue && (_decodeThread == null || !_decodeThread.IsAlive))
            {
                AudioProcessing.SeekAudio(seekTarget.Value);
                SeekVideo(seekTarget.Value);
                ResetTimers();

                seekTarget = null;
                _paused = false;
                StartDecodeThread();  // Assuming you have this method to restart decoding
                return;
            }
            if (_paused)
            {
                if (_videoWatch.IsRunning)
                {
                    _videoWatch.Stop();
                    FFUnityAudioHelper.PauseAll(AudioProcessing.AudioOutput);
                }
            }
            else
            {
                _offset = _elapsedOffset;

                if (!_videoWatch.IsRunning)
                {
                    _videoWatch.Start();
                    FFUnityAudioHelper.UnPauseAll(AudioProcessing.AudioOutput);
                }
                if (_decodeThread == null || !_decodeThread.IsAlive)
                {
                    StartDecodeThread();
                }

                iterations = 0;
                while (ShouldUpdateVideo() && iterations < synchronizingmaxIterations)
                {
                    if (_videoFrameClones.TryDequeue(out FFTexData videoFrame))
                    {
                        _lastTexData = videoFrame;
                        // Once done, return it to the pool
                        _ffTexDataPool.Return(videoFrame);
                        iterations++;
                        _lastVideoTex = new TexturePool.TexturePoolState()
                        {
                            pts = _lastTexData.time,
                        };

                        unityTextureGeneration.UpdateTexture(_lastTexData);
                    }
                    else
                    {
                        break;
                    }
                }
            }
            unityTextureGeneration.DisplayFrame();
            _prevTime = _offset;
            _wasPaused = _paused;
        }
        /// <summary>
        /// Checks if the stream and video context are initialized.
        /// </summary>
        private bool IsInitialized()
        {
            if (_videoWatch == null || _streamVideoCtx == null)
            {
                return false;
            }
            return true;
        }
        /// <summary>
        /// Determines whether the video and audio streams have reached their end.
        /// </summary>
        /// <returns>True if both streams are at the end, false otherwise.</returns>
        private bool IsStreamAtEnd()
        {
            return _offset >= _streamVideoCtx.GetLength()
                || (_streamVideoCtx.EndReached &&
                    (AudioProcessing._audioDecoder == null || AudioProcessing._streamAudioCtx.EndReached)
                    && _videoTextures.Count == 0
                    && (AudioProcessing._audioDecoder == null || AudioProcessing._audioStreams.Count == 0));
        }
        /// <summary>
        /// Determines whether the video display should be updated.
        /// </summary>
        /// <returns>True if the video display needs updating, false otherwise.</returns>
        private bool ShouldUpdateVideo()
        {
            return Math.Abs(_elapsedOffsetVideo - (PlaybackTime + _videoOffset)) >= VideoUpdate || _lastVideoTex == null;
        }
        private void UpdateThread()
        {
            UnityEngine.Debug.Log("AV Thread started.");

            GetVideoFps();//this works correctly -LD

            double frameTimeMs = GetFrameTimeMs(targetFps);
            double frameInterval = 1d / targetFps;

            // Continuously update video frames while not paused
            while (!_paused)
            {
                try
                {
                    double elapsedMs = FillVideoBuffers(frameInterval, frameTimeMs);

                    // Calculate the sleep duration, ensuring a minimum of 5ms sleep to avoid tight loops
                    int sleepDurationMs = (int)Math.Max(1, frameTimeMs - elapsedMs);
                    Thread.Sleep(sleepDurationMs);
                }
                catch (ThreadInterruptedException ex)
                {
                    UnityEngine.Debug.LogWarning($"Thread interrupted: {ex.Message}");
                    break; // Exit loop if thread is interrupted
                }
                catch (Exception e)
                {
                    UnityEngine.Debug.LogError($"Error in video update thread: {e}");
                    // Consider implementing a retry logic or a fail-safe here
                    // Optionally you can break if the error is critical
                    break;
                }
            }

            // Ensure proper cleanup before exiting the thread
            CleanupAfterThread();

            // Log and finalize thread operation
            UnityEngine.Debug.Log("AV Thread stopped.");
            _videoWatch.Stop();
            _paused = true;
        }
        private void CleanupAfterThread()
        {
            // Perform any necessary cleanup here, such as releasing resources or memory
            if (_videoFrames != null)
            {
                foreach (var frame in _videoFrames)
                {
                    // Release or dispose frame if necessary
                }
                _videoFrames = null; // Clear reference for garbage collection
            }

            // Example: if you're using a texture pool or any other resource, clear it here
            _ffTexDataPool.Clear();

            // Optionally reset any state variables if needed
        }
        /// <summary>
        /// Gets the frame rate of the video, defaulting to 30fps if not available.
        /// </summary>
        private void GetVideoFps()
        {
            if (_streamVideoCtx.TryGetFps(_videoDecoder, out targetFps))
            {
                UnityEngine.Debug.Log("frame rate found is " + targetFps);
                // Validate target FPS; set fallback if necessary
                if (targetFps <= 0) targetFps = FallbackFramerate;
            }
            else
            {
                UnityEngine.Debug.LogError("frame rate not found falling back!");
                targetFps = FallbackFramerate; // Ensure fallback is set if fetching fails
            }
        }
        /// <summary>
        /// Calculates the time per frame in milliseconds based on the FPS.
        /// </summary>
        private double GetFrameTimeMs(double fps)
        {
            return (1d / fps) * 1000;
        }
        private void StartDecodeThread()
        {
            if (_decodeThread == null || !_decodeThread.IsAlive)
            {
                _decodeThread = new Thread(UpdateThread)
                {
                    Name = $"AV Decode Thread {name}"
                };
               UnityEngine.Debug.Log("StartDecodeThread");
                _decodeThread.Start();
            }
        }
        /// <summary>
        /// this code is faulty techanon! 
        /// </summary>
        /// <param name="invFps"></param>
        /// <param name="fpsMs"></param>
        /// <returns></returns>
        private double FillVideoBuffers(double invFps, double fpsMs)
        {
            if (!IsInitialized())
            {
                return 0;
            }

            // Initialize state variables
            double time = default;
            double elapsedMilliseconds = 0;

            // Calculate the target frame interval in milliseconds
            double targetFrameTimeMs = fpsMs;

            // Record the start time using DateTime
            DateTime startTime = DateTime.UtcNow;

            // Main loop that runs while we are within the target frame time
            while (elapsedMilliseconds < targetFrameTimeMs)
            {
                // Record the current time at the start of the loop
                DateTime loopStartTime = DateTime.UtcNow;

                // Process video frames
                if (ShouldDecodeVideo() && TryProcessVideoFrame(ref time))
                {
                    // Update elapsed time for the loop
                    elapsedMilliseconds = (DateTime.UtcNow - loopStartTime).TotalMilliseconds;
                    continue;
                }
                // Process audio frames
                if (AudioProcessing._audioDecoder != null && AudioProcessing.ShouldDecodeAudio(this) && AudioProcessing.TryProcessAudioFrame(ref time, this))
                {
                    // Update elapsed time for the loop
                    elapsedMilliseconds = (DateTime.UtcNow - loopStartTime).TotalMilliseconds;
                    continue;
                }
                // Update elapsed time for the loop
                elapsedMilliseconds = (DateTime.UtcNow - loopStartTime).TotalMilliseconds;
            }

            // Calculate the total elapsed time for this fill operation
            return (DateTime.UtcNow - startTime).TotalMilliseconds;
        }
        /// <summary>
        /// Determines whether the video frame should be decoded.
        /// </summary>
        private bool ShouldDecodeVideo()
        {
            if (_lastVideoTex != null)
            {
                // Adjust the time offset if playback and video are out of sync
                if (Math.Abs(_elapsedOffsetVideo - PlaybackTime) > _videoTimeBuffer * VideoCatchupMultiplier && !CanSeek)
                {
                    _timeOffset = -PlaybackTime;
                    UnityEngine.Debug.LogWarning($"Time offset adjusted: {_timeOffset} due to sync issue.");
                }

                // Check if the current video decoder can decode and is in sync
                if (_videoDecoder.CanDecode() && _streamVideoCtx.TryGetTime(_videoDecoder, out double time))
                {
                    if (_elapsedOffsetVideo + _videoTimeBuffer < time)
                    {
                        if (LogVerBoseErrors)
                        {
                            UnityEngine.Debug.LogError($"Frame not decoded. Elapsed offset ({_elapsedOffsetVideo}) + buffer ({_videoTimeBuffer}) < current time ({time}).");
                        }
                        return false;
                    }

                    if (_elapsedOffsetVideo > time + _videoSkipBuffer && CanSeek)
                    {
                        _streamVideoCtx.NextFrame(out _);
                        skippedFrames++;
                        if (LogVerBoseErrors)
                        {
                            UnityEngine.Debug.LogWarning($"Frame skipped due to out-of-sync: elapsed offset ({_elapsedOffsetVideo}) > current time ({time}) + skip buffer ({_videoSkipBuffer}). Total skipped frames: {skippedFrames}");
                        }
                        return false;
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Video decoder cannot decode or time retrieval failed.");
                }
            }
            return true;
        }
        /// <summary>
        /// Processes the video frame, decodes it, and manages the buffer.
        /// </summary>
        private bool TryProcessVideoFrame(ref double time)
        {
            // Attempt to decode the next video frame
            _streamVideoCtx.NextFrame(out _);
            var vid = _videoDecoder.Decode(out var vFrame);

            if (vid == 0) // Successful decoding
            {
                // Check if the frame is out of sync and needs to be skipped
                if (_streamVideoCtx.TryGetTime(_videoDecoder, vFrame, out time) && _elapsedOffsetVideo > time + _videoSkipBuffer && CanSeek)
                {
                    UnityEngine.Debug.LogWarning($"Skipping frame. Elapsed offset: {_elapsedOffsetVideo}, Frame time: {time}, Skip buffer: {_videoSkipBuffer}.");
                    return false;
                }

                // Update the last decode time if time is valid
                if (_streamVideoCtx.TryGetTime(_videoDecoder, vFrame, out time) && time != 0)
                {
                    _lastVideoDecodeTime = time;
                    if (LogVerBoseErrors)
                    {
                        UnityEngine.Debug.Log($"Video frame decoded. New last decode time: {_lastVideoDecodeTime}.");
                    }
                }
                else
                {
                    UnityEngine.Debug.LogWarning("Failed to retrieve valid time from the decoded frame.");
                }

                // Store the video frame in the buffer
                _videoFrames[_videoWriteIndex % _videoFrames.Length] = vFrame;

                EnqueueVideoFrame(vFrame, time);
                _videoWriteIndex++;
                if (LogVerBoseErrors)
                {
                    UnityEngine.Debug.Log($"Video frame processed and stored. Write index: {_videoWriteIndex}.");
                }
                return true;
            }
            else
            {
                if (vid == 1)
                {
                    UnityEngine.Debug.LogError($"there is no data available right now, try again later... Decoding failed with error code: {vid}. Check decoder status or video stream.");
                }
                else
                {
                    if (LogVerBoseErrors)
                    {
                        UnityEngine.Debug.LogError($"Decoding failed with error code: {vid}. Check decoder status or video stream.");
                    }
                }
            }

            return false;
        }
        public bool LogVerBoseErrors = false;
        private void EnqueueVideoFrame(AVFrame vFrame, double time)
        {
            // Retrieve a frame from the pool
            FFTexData frameData = _ffTexDataPool.Get(vFrame.width, vFrame.height);

            // Clone the frame data
            if (FFUnityFrameHelper.SaveFrame(ref _videoConverter, vFrame, vFrame.width, vFrame.height, ColorCount, frameData.data, _videoDecoder.HWPixelFormat))
            {
                _streamVideoCtx.TryGetTime(_videoDecoder, vFrame, out time);
                _lastPts = time;
                frameData.time = time;
                _videoFrameClones.Enqueue(frameData); // Enqueue the frame data for processing
            }
            else
            {
                UnityEngine.Debug.LogError("Could not save frame");
                _videoWriteIndex--;
                _ffTexDataPool.Return(frameData); // Return it to the pool on failure
            }
        }
    }
}