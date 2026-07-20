using System;

namespace TDJS_Vision.Forms.CameraAdd.Quality
{
    /// <summary>
    /// 使用固定容量环形数组计算滑动平均，降低清晰度数值抖动。
    /// </summary>
    public sealed class MovingAverageFilter
    {
        /// <summary>
        /// 样本环形缓冲区。
        /// </summary>
        private readonly double[] _values;

        /// <summary>
        /// 下一个写入位置。
        /// </summary>
        private int _index;

        /// <summary>
        /// 当前有效样本数量。
        /// </summary>
        private int _count;

        /// <summary>
        /// 当前样本总和。
        /// </summary>
        private double _sum;

        /// <summary>
        /// 创建指定容量的滑动平均器。
        /// </summary>
        /// <param name="capacity">滑动窗口容量。</param>
        public MovingAverageFilter(int capacity)
        {
            if (capacity <= 0)
                throw new ArgumentOutOfRangeException(nameof(capacity));

            _values = new double[capacity];
        }

        /// <summary>
        /// 加入一个样本并返回当前平均值。
        /// </summary>
        /// <param name="value">样本值。</param>
        /// <returns>滑动平均值。</returns>
        public double Add(double value)
        {
            if (_count == _values.Length)
                _sum -= _values[_index];
            else
                _count++;

            _values[_index] = value;
            _sum += value;
            _index = (_index + 1) % _values.Length;
            return _sum / _count;
        }

        /// <summary>
        /// 清空当前样本。
        /// </summary>
        public void Reset()
        {
            Array.Clear(_values, 0, _values.Length);
            _index = 0;
            _count = 0;
            _sum = 0;
        }
    }
}
