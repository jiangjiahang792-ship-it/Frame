using System.Collections.Generic;
using System;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using OpenCvSharp;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public class NodeResultImageSource : INodeResult
    {
        public int RunTime { get; set; }
        /// <summary>
        /// 相机采集到的图像
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

    }
    /// <summary>
    /// 节点输出图像数据，图像源会同时提供原图、常规图像列表和灰度图缓存。
    /// </summary>
    public class OutputImage : IDisposable
    {
        /// <summary>
        /// 图像所有权与租约计数同步锁。
        /// </summary>
        private readonly object _resourceLock = new object();

        /// <summary>
        /// 当前输出明确拥有并最终负责释放的Mat集合，按对象引用去重。
        /// </summary>
        private readonly HashSet<Mat> _ownedImages = new HashSet<Mat>(MatReferenceComparer.Instance);

        /// <summary>
        /// 当前输出为借用上游图像而持有的父级资源租约。
        /// </summary>
        private readonly List<IImageResourceLease> _ownedDependencies = new List<IImageResourceLease>();

        /// <summary>
        /// 当前仍在使用图像的后台租约数量。
        /// </summary>
        private int _activeLeaseCount;

        /// <summary>
        /// 标记拥有者已经请求释放，新的使用者不得再取得租约。
        /// </summary>
        private bool _disposeRequested;

        /// <summary>
        /// 标记拥有的Mat已经完成最终释放。
        /// </summary>
        private bool _ownedResourcesReleased;

        /// <summary>
        /// 原图像
        /// </summary>
        public Mat SrcImg { get; set; }

        /// <summary>
        /// 灰度图缓存，供卡尺、测量等只读灰度算法直接订阅，避免每个节点重复整图转灰度。
        /// </summary>
        public Mat GrayImg { get; set; }

        /// <summary>
        /// 节点输出多张图像就存在图像列表；兼容旧节点时第一张通常也是原图。
        /// </summary>
        public List<Mat> Bitmaps { get; set; } = new List<Mat>();
        /// <summary>
        /// 截图一类节点会输出裁剪的图像相对原图的偏移量列表
        /// </summary>
        public List<Rect> Rectangles { get; set; } = new List<Rect>();

        /// <summary>
        /// 显示叠加层结果。图像数据保持干净，检测框、线、圆、文本等只在显示控件中绘制。
        /// </summary>
        public AlgorithmResult DisplayResult { get; set; } = new AlgorithmResult();

        /// <summary>
        /// 当前已经登记的自产Mat数量，供资源诊断和测试读取。
        /// </summary>
        public int OwnedImageCount
        {
            get
            {
                lock (_resourceLock)
                    return _ownedImages.Count;
            }
        }

        /// <summary>
        /// 当前仍在持有图像的租约数量。
        /// </summary>
        public int ActiveLeaseCount
        {
            get
            {
                lock (_resourceLock)
                    return _activeLeaseCount;
            }
        }

        /// <summary>
        /// 当前输出为借用上游图像而持有的父级租约数量。
        /// </summary>
        public int OwnedDependencyCount
        {
            get
            {
                lock (_resourceLock)
                    return _ownedDependencies.Count;
            }
        }

        /// <summary>
        /// 当前拥有者是否已经请求释放。
        /// </summary>
        public bool IsDisposeRequested
        {
            get
            {
                lock (_resourceLock)
                    return _disposeRequested;
            }
        }

        /// <summary>
        /// 当前登记的自产Mat是否已经完成最终释放。
        /// </summary>
        public bool AreOwnedResourcesReleased
        {
            get
            {
                lock (_resourceLock)
                    return _ownedResourcesReleased;
            }
        }

        /// <summary>
        /// 使用单张图像构建标准输出，兼容旧节点的 Bitmaps[0]，并尽量复用已有灰度图缓存。
        /// </summary>
        /// <param name="source">输出图像。</param>
        /// <param name="grayImage">可复用的灰度图缓存。</param>
        /// <returns>标准图像输出对象。</returns>
        public static OutputImage FromSingleImage(Mat source, Mat grayImage = null)
        {
            return FromOwnedSingleImage(source, grayImage);
        }

        /// <summary>
        /// 使用当前节点自产的单张图像构建输出，输出对象负责最终释放源图和自产灰度图。
        /// </summary>
        /// <param name="source">当前节点创建并转交所有权的源图。</param>
        /// <param name="grayImage">当前节点创建并转交所有权的灰度图；为空时按需创建。</param>
        /// <returns>拥有自产Mat的标准输出对象。</returns>
        public static OutputImage FromOwnedSingleImage(Mat source, Mat grayImage = null)
        {
            Mat suppliedGray = grayImage;
            Mat effectiveGray = grayImage;
            try
            {
                if (!HasValidImage(effectiveGray))
                {
                    effectiveGray = HasValidImage(source) ? BuildGrayImage(source) : null;
                    if (!ReferenceEquals(suppliedGray, source) && !ReferenceEquals(suppliedGray, effectiveGray))
                    {
                        Mat invalidSuppliedGray = suppliedGray;
                        suppliedGray = null;
                        try { invalidSuppliedGray?.Dispose(); } catch { }
                    }
                }

                OutputImage outputImage = new OutputImage
                {
                    SrcImg = source,
                    Bitmaps = HasValidImage(source) ? new List<Mat> { source } : new List<Mat>(),
                    GrayImg = effectiveGray
                };
                outputImage.TakeOwnership(source);
                outputImage.TakeOwnership(effectiveGray);
                return outputImage;
            }
            catch
            {
                try { source?.Dispose(); } catch { }
                if (!ReferenceEquals(effectiveGray, source))
                {
                    try { effectiveGray?.Dispose(); } catch { }
                }
                if (!ReferenceEquals(suppliedGray, source) && !ReferenceEquals(suppliedGray, effectiveGray))
                {
                    try { suppliedGray?.Dispose(); } catch { }
                }

                throw;
            }
        }

        /// <summary>
        /// 使用上游借用的单张图像构建输出，只拥有本方法按需新建的灰度缓存。
        /// </summary>
        /// <param name="source">上游拥有的只读源图。</param>
        /// <param name="grayImage">上游拥有的只读灰度缓存；为空时按需创建本地缓存。</param>
        /// <returns>不接管上游Mat释放权的标准输出对象。</returns>
        public static OutputImage FromBorrowedSingleImage(Mat source, Mat grayImage = null)
        {
            bool hasBorrowedGray = HasValidImage(grayImage);
            Mat effectiveGray = hasBorrowedGray
                ? grayImage
                : HasValidImage(source) ? BuildGrayImage(source) : null;
            OutputImage outputImage = new OutputImage
            {
                SrcImg = source,
                Bitmaps = HasValidImage(source) ? new List<Mat> { source } : new List<Mat>(),
                GrayImg = effectiveGray
            };

            if (!hasBorrowedGray && !ReferenceEquals(effectiveGray, source))
                outputImage.TakeOwnership(effectiveGray);

            return outputImage;
        }

        /// <summary>
        /// 从另一个输出图像借用单张Mat，并把父级租约随当前输出生命周期向下传递。
        /// </summary>
        /// <param name="owner">真正拥有或继续借用源Mat的上游输出。</param>
        /// <param name="source">从上游选择的只读源图。</param>
        /// <param name="grayImage">从上游选择的只读灰度缓存。</param>
        /// <returns>持有父级租约的借用输出。</returns>
        public static OutputImage FromBorrowedSingleImage(OutputImage owner, Mat source, Mat grayImage = null)
        {
            OutputImage outputImage = FromBorrowedSingleImage(source, grayImage);
            try
            {
                outputImage.TakeDependency(owner);
                return outputImage;
            }
            catch
            {
                outputImage.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 登记当前输出最终负责释放的一张自产Mat；同一对象重复登记只保留一份。
        /// </summary>
        /// <param name="image">由当前输出接管的自产Mat。</param>
        /// <returns>当前输出对象，便于构建结果时连续调用。</returns>
        public OutputImage TakeOwnership(Mat image)
        {
            if (image == null)
                return this;

            lock (_resourceLock)
            {
                if (_disposeRequested)
                    throw new ObjectDisposedException(nameof(OutputImage), "图像输出已经进入释放阶段，不能再登记资源。");

                _ownedImages.Add(image);
            }

            return this;
        }

        /// <summary>
        /// 批量登记当前输出最终负责释放的自产Mat。
        /// </summary>
        /// <param name="images">由当前输出接管的自产Mat集合。</param>
        /// <returns>当前输出对象。</returns>
        public OutputImage TakeOwnership(IEnumerable<Mat> images)
        {
            if (images == null)
                return this;

            foreach (Mat image in images)
                TakeOwnership(image);

            return this;
        }

        /// <summary>
        /// 持有上游输出的一份父级租约，使当前借用链释放前上游Mat保持有效。
        /// </summary>
        /// <param name="owner">当前输出所借用资源的上游拥有者。</param>
        /// <returns>当前输出对象。</returns>
        public OutputImage TakeDependency(OutputImage owner)
        {
            if (owner == null || ReferenceEquals(owner, this))
                return this;

            IImageResourceLease dependency = owner.AcquireLease();
            lock (_resourceLock)
            {
                if (_disposeRequested)
                {
                    dependency.Dispose();
                    throw new ObjectDisposedException(nameof(OutputImage), "图像输出已经进入释放阶段，不能再登记父级租约。");
                }

                _ownedDependencies.Add(dependency);
            }

            return this;
        }

        /// <summary>
        /// 为异步保存或显示任务取得临时图像租约，租约释放前不会回收自产Mat。
        /// </summary>
        /// <returns>必须由使用者释放的图像租约。</returns>
        public IImageResourceLease AcquireLease()
        {
            lock (_resourceLock)
            {
                if (_disposeRequested)
                    throw new ObjectDisposedException(nameof(OutputImage), "图像输出已经进入释放阶段，不能再取得租约。");

                _activeLeaseCount++;
                return new OutputImageLease(this);
            }
        }

        /// <summary>
        /// 估算当前输出租约会固定在内存中的全部Mat字节数，包括灰度图、多图输出和父级依赖链。
        /// </summary>
        /// <returns>按Mat实际行跨度去重累计的估算字节数。</returns>
        public long EstimateRetainedImageBytes()
        {
            HashSet<OutputImage> visitedOutputs = new HashSet<OutputImage>(OutputImageReferenceComparer.Instance);
            HashSet<Mat> visitedImages = new HashSet<Mat>(MatReferenceComparer.Instance);
            return CollectRetainedImageBytes(this, visitedOutputs, visitedImages);
        }

        /// <summary>
        /// 递归收集当前输出及父级依赖链中的Mat，并按托管引用去重。
        /// </summary>
        /// <param name="output">当前需要统计的输出对象。</param>
        /// <param name="visitedOutputs">已经统计过的输出对象集合。</param>
        /// <param name="visitedImages">已经统计过的Mat集合。</param>
        /// <returns>当前分支新增的估算字节数。</returns>
        private static long CollectRetainedImageBytes(
            OutputImage output,
            HashSet<OutputImage> visitedOutputs,
            HashSet<Mat> visitedImages)
        {
            if (output == null || !visitedOutputs.Add(output))
                return 0L;

            List<Mat> images = new List<Mat>();
            List<OutputImage> dependencies = new List<OutputImage>();
            lock (output._resourceLock)
            {
                images.AddRange(output._ownedImages);
                images.Add(output.SrcImg);
                images.Add(output.GrayImg);
                if (output.Bitmaps != null)
                    images.AddRange(output.Bitmaps);

                foreach (IImageResourceLease dependency in output._ownedDependencies)
                {
                    OutputImage dependencyImage = dependency?.Image;
                    if (dependencyImage != null)
                        dependencies.Add(dependencyImage);
                }
            }

            long totalBytes = 0L;
            foreach (Mat image in images)
            {
                if (!HasValidImage(image) || !visitedImages.Add(image))
                    continue;

                long imageBytes;
                try
                {
                    imageBytes = Math.Max(1L, checked((long)image.Rows * (long)image.Step()));
                }
                catch (OverflowException)
                {
                    return long.MaxValue;
                }

                if (imageBytes > long.MaxValue - totalBytes)
                    return long.MaxValue;
                totalBytes += imageBytes;
            }

            foreach (OutputImage dependency in dependencies)
            {
                long dependencyBytes = CollectRetainedImageBytes(dependency, visitedOutputs, visitedImages);
                if (dependencyBytes > long.MaxValue - totalBytes)
                    return long.MaxValue;
                totalBytes += dependencyBytes;
            }

            return totalBytes;
        }

        /// <summary>
        /// 请求释放当前输出拥有的资源；存在租约时延迟到最后一个租约释放。
        /// </summary>
        public void Dispose()
        {
            List<Mat> imagesToDispose;
            List<IImageResourceLease> dependenciesToDispose;
            lock (_resourceLock)
            {
                if (_disposeRequested)
                    return;

                _disposeRequested = true;
                imagesToDispose = CollectResourcesForFinalRelease(out dependenciesToDispose);
            }

            DisposeResources(imagesToDispose, dependenciesToDispose);
        }

        /// <summary>
        /// 释放一个租约，并在最后一个使用者退出后执行延迟的最终回收。
        /// </summary>
        private void ReleaseLease()
        {
            List<Mat> imagesToDispose;
            List<IImageResourceLease> dependenciesToDispose;
            lock (_resourceLock)
            {
                if (_activeLeaseCount <= 0)
                    return;

                _activeLeaseCount--;
                imagesToDispose = CollectResourcesForFinalRelease(out dependenciesToDispose);
            }

            DisposeResources(imagesToDispose, dependenciesToDispose);
        }

        /// <summary>
        /// 在同步锁内判断是否满足最终释放条件并取出待释放Mat。
        /// </summary>
        /// <param name="dependencies">需要在锁外释放的父级租约集合。</param>
        /// <returns>需要在锁外释放的Mat集合；条件未满足时返回空。</returns>
        private List<Mat> CollectResourcesForFinalRelease(out List<IImageResourceLease> dependencies)
        {
            dependencies = null;
            if (!_disposeRequested || _activeLeaseCount != 0 || _ownedResourcesReleased)
                return null;

            _ownedResourcesReleased = true;
            List<Mat> images = _ownedImages.ToList();
            dependencies = new List<IImageResourceLease>(_ownedDependencies);
            _ownedImages.Clear();
            _ownedDependencies.Clear();
            return images;
        }

        /// <summary>
        /// 在同步锁外释放Mat，避免耗时的原生资源回收阻塞租约计数锁。
        /// </summary>
        /// <param name="images">待释放的去重Mat集合。</param>
        /// <param name="dependencies">待释放的父级图像租约集合。</param>
        private static void DisposeResources(
            IEnumerable<Mat> images,
            IEnumerable<IImageResourceLease> dependencies)
        {
            if (images != null)
            {
                foreach (Mat image in images)
                {
                    try
                    {
                        image?.Dispose();
                    }
                    catch
                    {
                        // 单张图释放异常不能阻断其余Mat和父级租约回收。
                    }
                }
            }

            if (dependencies == null)
                return;

            foreach (IImageResourceLease dependency in dependencies)
            {
                try
                {
                    dependency?.Dispose();
                }
                catch
                {
                    // 单个父级租约异常不能阻断其余借用链释放。
                }
            }
        }

        /// <summary>
        /// 判断图像对象是否可读。
        /// </summary>
        /// <param name="image">待检查的图像。</param>
        /// <returns>图像非空且有有效像素时返回 true。</returns>
        public static bool HasValidImage(Mat image)
        {
            return image != null && !image.Empty();
        }

        /// <summary>
        /// 构建灰度图缓存；单通道图像直接复用原图引用，彩色图像只转换一次。
        /// </summary>
        /// <param name="source">源图像。</param>
        /// <returns>灰度图缓存。</returns>
        public static Mat BuildGrayImage(Mat source)
        {
            if (!HasValidImage(source))
                return new Mat();

            int channels = source.Channels();
            if (channels == 1)
                return source;

            Mat gray = new Mat();
            try
            {
                if (channels == 4)
                    Cv2.CvtColor(source, gray, ColorConversionCodes.BGRA2GRAY);
                else
                    Cv2.CvtColor(source, gray, ColorConversionCodes.BGR2GRAY);

                return gray;
            }
            catch
            {
                gray.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 输出图像租约实现，通过原子交换保证重复释放不会重复减少计数。
        /// </summary>
        private sealed class OutputImageLease : IImageResourceLease
        {
            /// <summary>
            /// 当前租约保护的输出对象，释放后被原子清空。
            /// </summary>
            private OutputImage _image;

            /// <summary>
            /// 创建一份输出图像租约。
            /// </summary>
            /// <param name="image">需要保护的输出图像。</param>
            public OutputImageLease(OutputImage image)
            {
                _image = image;
            }

            /// <summary>
            /// 当前租约保护的输出图像；租约释放后为空。
            /// </summary>
            public OutputImage Image => Volatile.Read(ref _image);

            /// <summary>
            /// 释放当前租约；重复调用不会重复减少计数。
            /// </summary>
            public void Dispose()
            {
                OutputImage image = Interlocked.Exchange(ref _image, null);
                image?.ReleaseLease();
            }
        }

        /// <summary>
        /// Mat引用比较器，避免同一Mat同时出现在多个输出字段时重复释放。
        /// </summary>
        private sealed class MatReferenceComparer : IEqualityComparer<Mat>
        {
            /// <summary>
            /// Mat引用比较器单例。
            /// </summary>
            public static readonly MatReferenceComparer Instance = new MatReferenceComparer();

            /// <summary>
            /// 判断两个Mat是否为同一个托管包装对象。
            /// </summary>
            public bool Equals(Mat x, Mat y)
            {
                return ReferenceEquals(x, y);
            }

            /// <summary>
            /// 获取Mat包装对象的引用哈希值。
            /// </summary>
            public int GetHashCode(Mat obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }

        /// <summary>
        /// 输出图像引用比较器，避免父级依赖链出现共享节点时重复统计。
        /// </summary>
        private sealed class OutputImageReferenceComparer : IEqualityComparer<OutputImage>
        {
            /// <summary>输出图像引用比较器单例。</summary>
            public static readonly OutputImageReferenceComparer Instance = new OutputImageReferenceComparer();

            /// <summary>
            /// 判断两个输出对象是否为同一个托管实例。
            /// </summary>
            /// <param name="x">第一个输出对象。</param>
            /// <param name="y">第二个输出对象。</param>
            /// <returns>两个参数引用相同时返回true。</returns>
            public bool Equals(OutputImage x, OutputImage y)
            {
                return ReferenceEquals(x, y);
            }

            /// <summary>
            /// 获取输出对象的引用哈希值。
            /// </summary>
            /// <param name="obj">输出对象。</param>
            /// <returns>引用哈希值。</returns>
            public int GetHashCode(OutputImage obj)
            {
                return obj == null ? 0 : RuntimeHelpers.GetHashCode(obj);
            }
        }
    }

}
