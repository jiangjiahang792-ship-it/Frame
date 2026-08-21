using System.Threading;
using OpenCvSharp;

namespace HikDualCameraMatBenchmark
{
    /// <summary>
    /// 容量为1的最新Mat槽位；新帧覆盖旧帧，防止UI显示排队反压相机回调。
    /// </summary>
    internal sealed class LatestMatSlot
    {
        /// <summary>等待UI消费的最新Mat对象。</summary>
        private Mat _latest;

        /// <summary>被新帧覆盖并释放的旧帧数量。</summary>
        private long _replacedCount;

        /// <summary>
        /// 发布最新帧，并立即释放尚未显示的旧帧。
        /// </summary>
        /// <param name="mat">拥有独立内存且由槽位接管生命周期的Mat。</param>
        public void Publish(Mat mat)
        {
            Mat previous = Interlocked.Exchange(ref _latest, mat);
            if (previous != null)
            {
                previous.Dispose();
                Interlocked.Increment(ref _replacedCount);
            }
        }

        /// <summary>
        /// 取得并移除当前最新帧；调用方负责释放返回的Mat。
        /// </summary>
        /// <returns>最新Mat，没有新帧时返回null。</returns>
        public Mat Take()
        {
            return Interlocked.Exchange(ref _latest, null);
        }

        /// <summary>
        /// 获取因UI来不及显示而被覆盖的帧数。
        /// </summary>
        public long ReplacedCount
        {
            get { return Interlocked.Read(ref _replacedCount); }
        }

        /// <summary>
        /// 释放槽位中尚未消费的Mat。
        /// </summary>
        public void Clear()
        {
            Mat remaining = Interlocked.Exchange(ref _latest, null);
            if (remaining != null)
            {
                remaining.Dispose();
            }
        }
    }
}
