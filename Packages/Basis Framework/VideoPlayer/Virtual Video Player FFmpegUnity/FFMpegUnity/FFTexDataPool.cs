using System.Collections.Concurrent;

namespace FFmpeg.Unity
{
    public class FFTexDataPool
    {
        private readonly ConcurrentQueue<FFTexData> _pool = new ConcurrentQueue<FFTexData>();

        private FFTexData CreateNewFFTexData(int FrameWidth, int FrameHeight,int BytesPerPixel = 3)// Assuming 3 bytes per pixel (RGB)
        {
            return new FFTexData
            {
                data = new byte[FrameWidth * FrameHeight * BytesPerPixel],
                height = FrameHeight,
                width = FrameWidth,
            };
        }

        public FFTexData Get(int FrameWidth, int FrameHeight, int BytesPerPixel = 3)
        {
            if (_pool.TryDequeue(out FFTexData item))
            {
                int Length = FrameWidth * FrameHeight * BytesPerPixel;
                if (item.data == null)
                {
                    item = new FFTexData
                    {
                        data = new byte[Length], // Assuming 3 bytes per pixel (RGB)
                        height = FrameHeight,
                        width = FrameWidth,
                    };
                }
                else
                {
                    if (item.data.Length != Length)
                    {
                        item.data = new byte[Length];
                        item.height = FrameHeight;
                        item.width = FrameWidth;
                    }
                }
                return item;
            }
            else
            {
                // Pool is empty, create a new instance
                return CreateNewFFTexData(FrameWidth, FrameHeight);
            }
        }

        public void Return(FFTexData item)
        {
            // Reset the reusable data object
            item.time = 0;
            _pool.Enqueue(item);
        }

        public void Clear()
        {
            // Clear the pool by creating a new instance of the ConcurrentQueue
            // Optionally, you can do any necessary cleanup on the items if required
            while (_pool.TryDequeue(out _)) { /* Intentionally empty */ }
        }
    }
}