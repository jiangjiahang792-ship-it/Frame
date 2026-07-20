using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TDJS_Vision.Device._3D;

namespace TDJS_Vision.Forms.ImageViewer
{
    /// <summary>
    /// 3D 点云预览控件，用 GDI+ 对 3D 相机帧做轻量投影显示。
    /// </summary>
    public class PointCloudPreviewControl : Control
    {
        /// <summary>
        /// 控件内最多参与绘制的点数，避免高分辨率点云拖慢 UI。
        /// </summary>
        private const int MaxRenderPointCount = 160000;

        /// <summary>
        /// 点云绘制颜色数量。
        /// </summary>
        private const int PaletteSize = 64;

        /// <summary>
        /// 当前用于显示的抽样点。
        /// </summary>
        private Camera3DPointCloudPoint[] _points = new Camera3DPointCloudPoint[0];

        /// <summary>
        /// 当前帧原始点数。
        /// </summary>
        private int _totalPointCount;

        /// <summary>
        /// 当前帧号。
        /// </summary>
        private uint _frameNumber;

        /// <summary>
        /// 当前点云边界。
        /// </summary>
        private PointCloudBounds _bounds = PointCloudBounds.Empty;

        /// <summary>
        /// 点云绕 Y 轴旋转角度。
        /// </summary>
        private float _yaw = 35F;

        /// <summary>
        /// 点云绕 X 轴旋转角度。
        /// </summary>
        private float _pitch = 30F;

        /// <summary>
        /// 用户缩放倍数。
        /// </summary>
        private float _zoom = 1F;

        /// <summary>
        /// 用户平移偏移。
        /// </summary>
        private PointF _pan = PointF.Empty;

        /// <summary>
        /// 是否正在旋转点云。
        /// </summary>
        private bool _isRotating;

        /// <summary>
        /// 是否正在平移点云。
        /// </summary>
        private bool _isPanning;

        /// <summary>
        /// 上一次鼠标位置。
        /// </summary>
        private Point _lastMousePoint;

        /// <summary>
        /// 绘制点云使用的固定色带画刷。
        /// </summary>
        private readonly Brush[] _palette = BuildPalette();

        /// <summary>
        /// 创建点云预览控件。
        /// </summary>
        public PointCloudPreviewControl()
        {
            DoubleBuffered = true;
            BackColor = Color.Black;
            ForeColor = Color.White;
            SetStyle(ControlStyles.AllPaintingInWmPaint | ControlStyles.UserPaint | ControlStyles.OptimizedDoubleBuffer, true);
        }

        /// <summary>
        /// 设置当前显示的 3D 帧数据。
        /// </summary>
        /// <param name="frameData">3D 相机帧数据。</param>
        public void SetFrame(Camera3DFrameData frameData)
        {
            _frameNumber = frameData == null ? 0 : frameData.FrameNumber;
            _totalPointCount = frameData == null ? 0 : frameData.TotalPointCount;
            _points = BuildDisplayPoints(frameData);
            if (_totalPointCount <= 0)
                _totalPointCount = _points.Length;
            _bounds = PointCloudBounds.FromPoints(_points);
            Invalidate();
        }

        /// <summary>
        /// 重置点云视角。
        /// </summary>
        public void ResetView()
        {
            _yaw = 35F;
            _pitch = 30F;
            _zoom = 1F;
            _pan = PointF.Empty;
            Invalidate();
        }

        /// <summary>
        /// 根据帧数据构建用于绘制的点云。
        /// </summary>
        /// <param name="frameData">3D 相机帧数据。</param>
        /// <returns>抽样后的点云点。</returns>
        private static Camera3DPointCloudPoint[] BuildDisplayPoints(Camera3DFrameData frameData)
        {
            if (frameData == null)
                return new Camera3DPointCloudPoint[0];

            if (frameData.PointCloudPoints != null && frameData.PointCloudPoints.Length > 0)
                return SamplePoints(frameData.PointCloudPoints, MaxRenderPointCount);

            Camera3DNativePointCloudFrame nativeFrame = frameData.NativePointCloudFrame;
            if (nativeFrame != null && nativeFrame.DataBytes != null && nativeFrame.DataBytes.Length >= 12)
                return BuildPointsFromNativeBytes(nativeFrame.DataBytes, MaxRenderPointCount);

            if (frameData.DepthValues != null && frameData.DepthValues.Length > 0)
                return BuildPointsFromDepth(frameData, MaxRenderPointCount);

            return new Camera3DPointCloudPoint[0];
        }

        /// <summary>
        /// 从托管点云中抽样。
        /// </summary>
        /// <param name="sourcePoints">源点云。</param>
        /// <param name="maxCount">最大点数。</param>
        /// <returns>抽样点云。</returns>
        private static Camera3DPointCloudPoint[] SamplePoints(Camera3DPointCloudPoint[] sourcePoints, int maxCount)
        {
            int step = Math.Max(1, (sourcePoints.Length + maxCount - 1) / maxCount);
            Camera3DPointCloudPoint[] targetPoints = new Camera3DPointCloudPoint[(sourcePoints.Length + step - 1) / step];
            int targetIndex = 0;
            for (int sourceIndex = 0; sourceIndex < sourcePoints.Length && targetIndex < targetPoints.Length; sourceIndex += step)
            {
                Camera3DPointCloudPoint point = sourcePoints[sourceIndex];
                if (point.IsFinite())
                    targetPoints[targetIndex++] = point;
            }

            if (targetIndex != targetPoints.Length)
                Array.Resize(ref targetPoints, targetIndex);

            return targetPoints;
        }

        /// <summary>
        /// 从 SDK 原生点云字节数据中解析抽样点。
        /// </summary>
        /// <param name="bytes">原生点云字节，每个点按 X/Y/Z 三个 float 存储。</param>
        /// <param name="maxCount">最大点数。</param>
        /// <returns>抽样点云。</returns>
        private static Camera3DPointCloudPoint[] BuildPointsFromNativeBytes(byte[] bytes, int maxCount)
        {
            int totalPointCount = bytes.Length / 12;
            int step = Math.Max(1, (totalPointCount + maxCount - 1) / maxCount);
            Camera3DPointCloudPoint[] points = new Camera3DPointCloudPoint[(totalPointCount + step - 1) / step];
            int targetIndex = 0;
            for (int pointIndex = 0; pointIndex < totalPointCount && targetIndex < points.Length; pointIndex += step)
            {
                int byteIndex = pointIndex * 12;
                float x = BitConverter.ToSingle(bytes, byteIndex);
                float y = BitConverter.ToSingle(bytes, byteIndex + 4);
                float z = BitConverter.ToSingle(bytes, byteIndex + 8);
                Camera3DPointCloudPoint point = new Camera3DPointCloudPoint(x, y, z, z);
                if (point.IsFinite())
                    points[targetIndex++] = point;
            }

            if (targetIndex != points.Length)
                Array.Resize(ref points, targetIndex);

            return points;
        }

        /// <summary>
        /// 从深度图生成投影点云，作为没有点云数据时的兜底显示。
        /// </summary>
        /// <param name="frameData">3D 帧数据。</param>
        /// <param name="maxCount">最大点数。</param>
        /// <returns>抽样点云。</returns>
        private static Camera3DPointCloudPoint[] BuildPointsFromDepth(Camera3DFrameData frameData, int maxCount)
        {
            int width = Math.Max(0, frameData.Width);
            int height = Math.Max(0, frameData.Height);
            int pixelCount = Math.Min(width * height, frameData.DepthValues.Length);
            if (width == 0 || height == 0 || pixelCount == 0)
                return new Camera3DPointCloudPoint[0];

            int step = Math.Max(1, (pixelCount + maxCount - 1) / maxCount);
            Camera3DPointCloudPoint[] points = new Camera3DPointCloudPoint[(pixelCount + step - 1) / step];
            int targetIndex = 0;
            for (int index = 0; index < pixelCount && targetIndex < points.Length; index += step)
            {
                int depth = frameData.DepthValues[index];
                if (depth == frameData.DepthInvalidValue)
                    continue;

                int x = index % width;
                int y = index / width;
                Camera3DPointCloudPoint point = new Camera3DPointCloudPoint(x, y, depth, depth);
                if (point.IsFinite())
                    points[targetIndex++] = point;
            }

            if (targetIndex != points.Length)
                Array.Resize(ref points, targetIndex);

            return points;
        }

        /// <summary>
        /// 绘制当前点云画面。
        /// </summary>
        /// <param name="e">绘制参数。</param>
        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.None;
            e.Graphics.Clear(Color.FromArgb(18, 18, 18));

            if (_points == null || _points.Length == 0 || !_bounds.IsValid)
            {
                DrawEmptyState(e.Graphics);
                return;
            }

            DrawPointCloud(e.Graphics);
            DrawOverlay(e.Graphics);
        }

        /// <summary>
        /// 绘制无数据提示。
        /// </summary>
        /// <param name="graphics">绘图对象。</param>
        private void DrawEmptyState(Graphics graphics)
        {
            string text = "未获取到3D点云数据";
            using (Font font = new Font("宋体", 13F, FontStyle.Bold))
            using (Brush brush = new SolidBrush(Color.Gainsboro))
            {
                SizeF size = graphics.MeasureString(text, font);
                graphics.DrawString(text, font, brush, (Width - size.Width) / 2F, (Height - size.Height) / 2F);
            }
        }

        /// <summary>
        /// 绘制点云。
        /// </summary>
        /// <param name="graphics">绘图对象。</param>
        private void DrawPointCloud(Graphics graphics)
        {
            float fitScale = GetFitScale();
            int pointSize = Math.Max(1, (int)Math.Round(Math.Min(3F, _zoom)));
            for (int i = 0; i < _points.Length; i++)
            {
                Camera3DPointCloudPoint point = _points[i];
                PointF screenPoint = ProjectPoint(point, fitScale);
                if (screenPoint.X < -4 || screenPoint.X > Width + 4 || screenPoint.Y < -4 || screenPoint.Y > Height + 4)
                    continue;

                int colorIndex = GetPaletteIndex(point.Value);
                graphics.FillRectangle(_palette[colorIndex], screenPoint.X, screenPoint.Y, pointSize, pointSize);
            }
        }

        /// <summary>
        /// 绘制状态信息。
        /// </summary>
        /// <param name="graphics">绘图对象。</param>
        private void DrawOverlay(Graphics graphics)
        {
            string text = $"帧号：{_frameNumber}  点数：{_points.Length}/{_totalPointCount}  左键旋转  右键平移  滚轮缩放  双击复位";
            using (Font font = new Font("宋体", 10F))
            using (Brush background = new SolidBrush(Color.FromArgb(180, 0, 0, 0)))
            using (Brush foreground = new SolidBrush(Color.White))
            {
                SizeF size = graphics.MeasureString(text, font);
                RectangleF backgroundRect = new RectangleF(8, 8, size.Width + 16, size.Height + 8);
                graphics.FillRectangle(background, backgroundRect);
                graphics.DrawString(text, font, foreground, 16, 12);
            }
        }

        /// <summary>
        /// 将三维点投影到屏幕坐标。
        /// </summary>
        /// <param name="point">三维点。</param>
        /// <param name="fitScale">适配缩放。</param>
        /// <returns>屏幕坐标。</returns>
        private PointF ProjectPoint(Camera3DPointCloudPoint point, float fitScale)
        {
            float x = point.X - _bounds.CenterX;
            float y = point.Y - _bounds.CenterY;
            float z = point.Z - _bounds.CenterZ;

            float yawRad = _yaw * (float)Math.PI / 180F;
            float pitchRad = _pitch * (float)Math.PI / 180F;
            float cosYaw = (float)Math.Cos(yawRad);
            float sinYaw = (float)Math.Sin(yawRad);
            float cosPitch = (float)Math.Cos(pitchRad);
            float sinPitch = (float)Math.Sin(pitchRad);

            float x1 = x * cosYaw + z * sinYaw;
            float z1 = -x * sinYaw + z * cosYaw;
            float y1 = y * cosPitch - z1 * sinPitch;

            return new PointF(
                Width / 2F + _pan.X + x1 * fitScale * _zoom,
                Height / 2F + _pan.Y - y1 * fitScale * _zoom);
        }

        /// <summary>
        /// 计算适配窗口的缩放比例。
        /// </summary>
        /// <returns>基础缩放比例。</returns>
        private float GetFitScale()
        {
            float range = Math.Max(_bounds.RangeX, Math.Max(_bounds.RangeY, _bounds.RangeZ));
            if (range <= 0F)
                return 1F;

            float availableSize = Math.Max(50F, Math.Min(Width, Height) - 80F);
            return availableSize / range;
        }

        /// <summary>
        /// 获取点值对应的色带索引。
        /// </summary>
        /// <param name="value">点值。</param>
        /// <returns>色带索引。</returns>
        private int GetPaletteIndex(float value)
        {
            float range = _bounds.ValueMax - _bounds.ValueMin;
            if (range <= 0F)
                return PaletteSize / 2;

            int index = (int)((value - _bounds.ValueMin) / range * (PaletteSize - 1));
            return Math.Max(0, Math.Min(PaletteSize - 1, index));
        }

        /// <summary>
        /// 构建点云色带。
        /// </summary>
        /// <returns>画刷数组。</returns>
        private static Brush[] BuildPalette()
        {
            Brush[] brushes = new Brush[PaletteSize];
            for (int i = 0; i < brushes.Length; i++)
            {
                float t = i / (float)(brushes.Length - 1);
                int r = (int)(255 * Math.Max(0F, Math.Min(1F, (t - 0.5F) * 2F)));
                int g = (int)(255 * (1F - Math.Abs(t - 0.5F) * 2F));
                int b = (int)(255 * Math.Max(0F, Math.Min(1F, (0.5F - t) * 2F)));
                brushes[i] = new SolidBrush(Color.FromArgb(r, g, b));
            }

            return brushes;
        }

        /// <summary>
        /// 鼠标按下开始旋转或平移。
        /// </summary>
        /// <param name="e">鼠标参数。</param>
        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            _lastMousePoint = e.Location;
            _isRotating = e.Button == MouseButtons.Left;
            _isPanning = e.Button == MouseButtons.Right || e.Button == MouseButtons.Middle;
        }

        /// <summary>
        /// 鼠标移动更新视角。
        /// </summary>
        /// <param name="e">鼠标参数。</param>
        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_isRotating)
            {
                _yaw += (e.X - _lastMousePoint.X) * 0.35F;
                _pitch -= (e.Y - _lastMousePoint.Y) * 0.35F;
                _pitch = Math.Max(-89F, Math.Min(89F, _pitch));
                _lastMousePoint = e.Location;
                Invalidate();
            }
            else if (_isPanning)
            {
                _pan = new PointF(_pan.X + e.X - _lastMousePoint.X, _pan.Y + e.Y - _lastMousePoint.Y);
                _lastMousePoint = e.Location;
                Invalidate();
            }
        }

        /// <summary>
        /// 鼠标抬起结束交互。
        /// </summary>
        /// <param name="e">鼠标参数。</param>
        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            _isRotating = false;
            _isPanning = false;
        }

        /// <summary>
        /// 鼠标滚轮缩放视图。
        /// </summary>
        /// <param name="e">鼠标参数。</param>
        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            float factor = e.Delta > 0 ? 1.15F : 1F / 1.15F;
            _zoom = Math.Max(0.05F, Math.Min(80F, _zoom * factor));
            Invalidate();
        }

        /// <summary>
        /// 双击复位视图。
        /// </summary>
        /// <param name="e">鼠标参数。</param>
        protected override void OnDoubleClick(EventArgs e)
        {
            base.OnDoubleClick(e);
            ResetView();
        }

        /// <summary>
        /// 释放色带资源。
        /// </summary>
        /// <param name="disposing">是否释放托管资源。</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                foreach (Brush brush in _palette)
                    brush.Dispose();
            }

            base.Dispose(disposing);
        }

        /// <summary>
        /// 点云边界。
        /// </summary>
        private struct PointCloudBounds
        {
            /// <summary>
            /// 空边界。
            /// </summary>
            public static readonly PointCloudBounds Empty = new PointCloudBounds { IsValid = false };

            /// <summary>
            /// 是否有效。
            /// </summary>
            public bool IsValid;

            /// <summary>
            /// X 最小值。
            /// </summary>
            public float XMin;

            /// <summary>
            /// X 最大值。
            /// </summary>
            public float XMax;

            /// <summary>
            /// Y 最小值。
            /// </summary>
            public float YMin;

            /// <summary>
            /// Y 最大值。
            /// </summary>
            public float YMax;

            /// <summary>
            /// Z 最小值。
            /// </summary>
            public float ZMin;

            /// <summary>
            /// Z 最大值。
            /// </summary>
            public float ZMax;

            /// <summary>
            /// 点值最小值。
            /// </summary>
            public float ValueMin;

            /// <summary>
            /// 点值最大值。
            /// </summary>
            public float ValueMax;

            /// <summary>
            /// X 中心。
            /// </summary>
            public float CenterX => (XMin + XMax) / 2F;

            /// <summary>
            /// Y 中心。
            /// </summary>
            public float CenterY => (YMin + YMax) / 2F;

            /// <summary>
            /// Z 中心。
            /// </summary>
            public float CenterZ => (ZMin + ZMax) / 2F;

            /// <summary>
            /// X 范围。
            /// </summary>
            public float RangeX => XMax - XMin;

            /// <summary>
            /// Y 范围。
            /// </summary>
            public float RangeY => YMax - YMin;

            /// <summary>
            /// Z 范围。
            /// </summary>
            public float RangeZ => ZMax - ZMin;

            /// <summary>
            /// 从点云计算边界。
            /// </summary>
            /// <param name="points">点云点。</param>
            /// <returns>点云边界。</returns>
            public static PointCloudBounds FromPoints(Camera3DPointCloudPoint[] points)
            {
                if (points == null || points.Length == 0)
                    return Empty;

                PointCloudBounds bounds = new PointCloudBounds
                {
                    IsValid = true,
                    XMin = float.MaxValue,
                    YMin = float.MaxValue,
                    ZMin = float.MaxValue,
                    ValueMin = float.MaxValue,
                    XMax = float.MinValue,
                    YMax = float.MinValue,
                    ZMax = float.MinValue,
                    ValueMax = float.MinValue
                };

                foreach (Camera3DPointCloudPoint point in points)
                {
                    if (!point.IsFinite())
                        continue;

                    bounds.XMin = Math.Min(bounds.XMin, point.X);
                    bounds.XMax = Math.Max(bounds.XMax, point.X);
                    bounds.YMin = Math.Min(bounds.YMin, point.Y);
                    bounds.YMax = Math.Max(bounds.YMax, point.Y);
                    bounds.ZMin = Math.Min(bounds.ZMin, point.Z);
                    bounds.ZMax = Math.Max(bounds.ZMax, point.Z);
                    bounds.ValueMin = Math.Min(bounds.ValueMin, point.Value);
                    bounds.ValueMax = Math.Max(bounds.ValueMax, point.Value);
                }

                return bounds.XMin == float.MaxValue ? Empty : bounds;
            }
        }
    }
}
