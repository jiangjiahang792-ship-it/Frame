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
        /// 串行保护依赖边的检查与登记，确保并发构建输出时不会形成循环依赖。
        /// </summary>
        private static readonly object DependencyGraphLock = new object();

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

        /// <summary>必须与当前输出图像最终生命周期一起释放的外部资源。</summary>
        private readonly List<IDisposable> _ownedExternalLifetimes = new List<IDisposable>();

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

        /// <summary>原图像字段，写入时同步刷新保存队列使用的资源估算。</summary>
        private Mat _srcImg;

        /// <summary>灰度图缓存字段，写入时同步刷新保存队列使用的资源估算。</summary>
        private Mat _grayImg;

        /// <summary>多图输出字段，写入时同步刷新保存队列使用的资源估算。</summary>
        private List<Mat> _bitmaps = new List<Mat>();

        /// <summary>输出构建期间缓存的保守资源字节估算，保存队列预判时不再访问Mat。</summary>
        private long _retainedImageBytesEstimate = 1L;

        /// <summary>
        /// 原图像
        /// </summary>
        public Mat SrcImg
        {
            get
            {
                lock (_resourceLock)
                    return _srcImg;
            }
            set
            {
                lock (_resourceLock)
                {
                    ThrowIfDisposeRequestedNoLock();
                    _srcImg = value;
                    RefreshRetainedImageBytesEstimateNoLock();
                }
            }
        }

        /// <summary>
        /// 灰度图缓存，供卡尺、测量等只读灰度算法直接订阅，避免每个节点重复整图转灰度。
        /// </summary>
        public Mat GrayImg
        {
            get
            {
                lock (_resourceLock)
                    return _grayImg;
            }
            set
            {
                lock (_resourceLock)
                {
                    ThrowIfDisposeRequestedNoLock();
                    _grayImg = value;
                    RefreshRetainedImageBytesEstimateNoLock();
                }
            }
        }

        /// <summary>
        /// 节点输出多张图像就存在图像列表；兼容旧节点时第一张通常也是原图。
        /// </summary>
        public List<Mat> Bitmaps
        {
            get
            {
                lock (_resourceLock)
                    return _bitmaps;
            }
            set
            {
                lock (_resourceLock)
                {
                    ThrowIfDisposeRequestedNoLock();
                    _bitmaps = value ?? new List<Mat>();
                    RefreshRetainedImageBytesEstimateNoLock();
                }
            }
        }
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

        /// <summary>当前随图像最终生命周期持有的外部资源数量。</summary>
        public int OwnedExternalLifetimeCount
        {
            get
            {
                lock (_resourceLock)
                    return _ownedExternalLifetimes.Count;
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

        /// <summary>使用自产图像构建输出，并接管必须随该图像最终释放的外部生命周期。</summary>
        /// <param name="source">当前节点创建并转交所有权的源图。</param>
        /// <param name="externalLifetime">例如相机实际图像字节预算租约。</param>
        /// <param name="grayImage">可复用的自产灰度图；为空时按需创建。</param>
        /// <returns>同时拥有Mat和外部生命周期的标准输出对象。</returns>
        public static OutputImage FromOwnedSingleImageWithLifetime(
            Mat source,
            IDisposable externalLifetime,
            Mat grayImage = null)
        {
            if (externalLifetime == null)
                throw new ArgumentNullException(nameof(externalLifetime));

            OutputImage outputImage = null;
            IDisposable pendingLifetime = externalLifetime;
            try
            {
                outputImage = FromOwnedSingleImage(source, grayImage);
                outputImage.TakeExternalLifetime(pendingLifetime);
                pendingLifetime = null;
                return outputImage;
            }
            catch
            {
                outputImage?.Dispose();
                try { pendingLifetime?.Dispose(); } catch { }
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
                RefreshRetainedImageBytesEstimateNoLock();
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
            try
            {
                lock (DependencyGraphLock)
                {
                    if (HasDependencyPath(owner, this))
                        throw new InvalidOperationException("图像输出依赖不能形成循环。");

                    lock (_resourceLock)
                    {
                        ThrowIfDisposeRequestedNoLock();
                        _ownedDependencies.Add(dependency);
                        try
                        {
                            RefreshRetainedImageBytesEstimateNoLock();
                        }
                        catch
                        {
                            _ownedDependencies.RemoveAt(_ownedDependencies.Count - 1);
                            throw;
                        }

                        dependency = null;
                    }
                }
            }
            finally
            {
                dependency?.Dispose();
            }

            return this;
        }

        /// <summary>接管一个必须等当前输出最后一份图像租约释放后才能回收的外部资源。</summary>
        /// <param name="externalLifetime">待接管的幂等外部资源。</param>
        /// <returns>当前输出对象。</returns>
        public OutputImage TakeExternalLifetime(IDisposable externalLifetime)
        {
            if (externalLifetime == null)
                return this;

            lock (_resourceLock)
            {
                ThrowIfDisposeRequestedNoLock();
                if (!_ownedExternalLifetimes.Any(item => ReferenceEquals(item, externalLifetime)))
                    _ownedExternalLifetimes.Add(externalLifetime);
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
        /// 读取保存队列预接纳使用的保守字节估算；该路径只读缓存，不访问可能并发释放的Mat。
        /// </summary>
        /// <returns>不小于1的资源字节估算。</returns>
        public long GetRetainedImageBytesEstimateForAdmission()
        {
            lock (_resourceLock)
            {
                ThrowIfDisposeRequestedNoLock();
                return Math.Max(1L, _retainedImageBytesEstimate);
            }
        }

        /// <summary>
        /// 在同一资源锁内取得后台保存租约和源图引用，再在租约保护下重算实际字节，避免释放与预算失真竞态。
        /// </summary>
        /// <returns>必须由调用方转移租约或释放的原子保存快照。</returns>
        public OutputImageSaveSnapshot AcquireSaveSnapshot()
        {
            Mat sourceImage;
            IImageResourceLease lease;
            lock (_resourceLock)
            {
                ThrowIfDisposeRequestedNoLock();
                sourceImage = _bitmaps == null || _bitmaps.Count == 0 ? null : _bitmaps[0];
                if (!HasValidImage(sourceImage))
                    throw new InvalidOperationException("待保存的输出图像为空。");

                _activeLeaseCount++;
                lease = new OutputImageLease(this);
            }

            try
            {
                HashSet<OutputImage> visitedOutputs = new HashSet<OutputImage>(OutputImageReferenceComparer.Instance);
                HashSet<Mat> visitedImages = new HashSet<Mat>(MatReferenceComparer.Instance);
                long exactEstimatedBytes = Math.Max(
                    1L,
                    CollectRetainedImageBytes(this, visitedOutputs, visitedImages));
                return new OutputImageSaveSnapshot(sourceImage, exactEstimatedBytes, lease);
            }
            catch
            {
                lease.Dispose();
                throw;
            }
        }

        /// <summary>
        /// 估算当前输出租约会固定在内存中的全部Mat字节数，包括灰度图、多图输出和父级依赖链。
        /// </summary>
        /// <returns>按Mat实际行跨度去重累计的估算字节数。</returns>
        public long EstimateRetainedImageBytes()
        {
            using (IImageResourceLease lease = AcquireLease())
            {
                HashSet<OutputImage> visitedOutputs = new HashSet<OutputImage>(OutputImageReferenceComparer.Instance);
                HashSet<Mat> visitedImages = new HashSet<Mat>(MatReferenceComparer.Instance);
                return CollectRetainedImageBytes(this, visitedOutputs, visitedImages);
            }
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
                images.Add(output._srcImg);
                images.Add(output._grayImg);
                if (output._bitmaps != null)
                    images.AddRange(output._bitmaps);

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
            List<IDisposable> externalLifetimesToDispose;
            lock (_resourceLock)
            {
                if (_disposeRequested)
                    return;

                _disposeRequested = true;
                imagesToDispose = CollectResourcesForFinalRelease(
                    out dependenciesToDispose,
                    out externalLifetimesToDispose);
            }

            DisposeResources(imagesToDispose, dependenciesToDispose, externalLifetimesToDispose);
        }

        /// <summary>
        /// 释放一个租约，并在最后一个使用者退出后执行延迟的最终回收。
        /// </summary>
        private void ReleaseLease()
        {
            List<Mat> imagesToDispose;
            List<IImageResourceLease> dependenciesToDispose;
            List<IDisposable> externalLifetimesToDispose;
            lock (_resourceLock)
            {
                if (_activeLeaseCount <= 0)
                    return;

                _activeLeaseCount--;
                imagesToDispose = CollectResourcesForFinalRelease(
                    out dependenciesToDispose,
                    out externalLifetimesToDispose);
            }

            DisposeResources(imagesToDispose, dependenciesToDispose, externalLifetimesToDispose);
        }

        /// <summary>
        /// 在同步锁内判断是否满足最终释放条件并取出待释放Mat。
        /// </summary>
        /// <param name="dependencies">需要在锁外释放的父级租约集合。</param>
        /// <returns>需要在锁外释放的Mat集合；条件未满足时返回空。</returns>
        private List<Mat> CollectResourcesForFinalRelease(
            out List<IImageResourceLease> dependencies,
            out List<IDisposable> externalLifetimes)
        {
            dependencies = null;
            externalLifetimes = null;
            if (!_disposeRequested || _activeLeaseCount != 0 || _ownedResourcesReleased)
                return null;

            _ownedResourcesReleased = true;
            List<Mat> images = _ownedImages.ToList();
            dependencies = new List<IImageResourceLease>(_ownedDependencies);
            externalLifetimes = new List<IDisposable>(_ownedExternalLifetimes);
            _ownedImages.Clear();
            _ownedDependencies.Clear();
            _ownedExternalLifetimes.Clear();
            _srcImg = null;
            _grayImg = null;
            _bitmaps = new List<Mat>();
            Interlocked.Exchange(ref _retainedImageBytesEstimate, 1L);
            return images;
        }

        /// <summary>在资源锁内检查输出尚未进入释放阶段。</summary>
        private void ThrowIfDisposeRequestedNoLock()
        {
            if (_disposeRequested)
                throw new ObjectDisposedException(nameof(OutputImage), "图像输出已经进入释放阶段。");
        }

        /// <summary>根据当前自产图和父级租约刷新保守字节估算；调用方必须持有资源锁。</summary>
        private void RefreshRetainedImageBytesEstimateNoLock()
        {
            HashSet<Mat> directImages = new HashSet<Mat>(MatReferenceComparer.Instance);
            foreach (Mat ownedImage in _ownedImages)
                directImages.Add(ownedImage);

            if (_ownedDependencies.Count == 0)
            {
                directImages.Add(_srcImg);
                directImages.Add(_grayImg);
                if (_bitmaps != null)
                {
                    foreach (Mat image in _bitmaps)
                        directImages.Add(image);
                }
            }

            long totalBytes = 0L;
            foreach (Mat image in directImages)
                totalBytes = AddEstimatedBytes(totalBytes, GetImageBytesNoThrow(image));

            foreach (IImageResourceLease dependency in _ownedDependencies)
            {
                OutputImage dependencyImage = dependency?.Image;
                if (dependencyImage != null)
                    totalBytes = AddEstimatedBytes(totalBytes, dependencyImage.GetCachedEstimateEvenIfDisposeRequested());
            }

            Interlocked.Exchange(ref _retainedImageBytesEstimate, Math.Max(1L, totalBytes));
        }

        /// <summary>原子读取父级租约保护下的缓存估算，不在子级资源锁内继续获取父级锁。</summary>
        private long GetCachedEstimateEvenIfDisposeRequested()
        {
            return Math.Max(1L, Interlocked.Read(ref _retainedImageBytesEstimate));
        }

        /// <summary>
        /// 检查指定输出能否沿现有父级依赖到达目标输出；调用方必须持有依赖图锁。
        /// </summary>
        /// <param name="start">依赖路径起点。</param>
        /// <param name="target">不能被路径到达的当前输出。</param>
        /// <returns>存在直接或传递路径时返回true。</returns>
        private static bool HasDependencyPath(OutputImage start, OutputImage target)
        {
            Stack<OutputImage> pending = new Stack<OutputImage>();
            HashSet<OutputImage> visited = new HashSet<OutputImage>(OutputImageReferenceComparer.Instance);
            pending.Push(start);

            while (pending.Count > 0)
            {
                OutputImage current = pending.Pop();
                if (current == null || !visited.Add(current))
                    continue;
                if (ReferenceEquals(current, target))
                    return true;

                List<OutputImage> dependencies = new List<OutputImage>();
                lock (current._resourceLock)
                {
                    foreach (IImageResourceLease dependency in current._ownedDependencies)
                    {
                        OutputImage dependencyImage = dependency?.Image;
                        if (dependencyImage != null)
                            dependencies.Add(dependencyImage);
                    }
                }

                foreach (OutputImage dependency in dependencies)
                    pending.Push(dependency);
            }

            return false;
        }

        /// <summary>读取单张Mat的行跨度字节；输出构建异常时返回0并由后续原子快照再次校验。</summary>
        private static long GetImageBytesNoThrow(Mat image)
        {
            try
            {
                return HasValidImage(image)
                    ? Math.Max(1L, checked((long)image.Rows * (long)image.Step()))
                    : 0L;
            }
            catch
            {
                return 0L;
            }
        }

        /// <summary>以饱和方式累加估算字节，避免大图计算溢出。</summary>
        private static long AddEstimatedBytes(long currentBytes, long additionalBytes)
        {
            if (additionalBytes <= 0L)
                return currentBytes;
            if (additionalBytes > long.MaxValue - currentBytes)
                return long.MaxValue;
            return currentBytes + additionalBytes;
        }

        /// <summary>
        /// 在同步锁外释放Mat，避免耗时的原生资源回收阻塞租约计数锁。
        /// </summary>
        /// <param name="images">待释放的去重Mat集合。</param>
        /// <param name="dependencies">待释放的父级图像租约集合。</param>
        private static void DisposeResources(
            IEnumerable<Mat> images,
            IEnumerable<IImageResourceLease> dependencies,
            IEnumerable<IDisposable> externalLifetimes)
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

            if (dependencies != null)
            {
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

            if (externalLifetimes == null)
                return;
            foreach (IDisposable externalLifetime in externalLifetimes)
            {
                try { externalLifetime?.Dispose(); }
                catch
                {
                    // 外部预算或句柄释放异常不能阻断同批其他资源回收。
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
        /// 保存队列接纳时原子取得的源图与租约快照。
        /// </summary>
        public sealed class OutputImageSaveSnapshot : IDisposable
        {
            /// <summary>快照拥有且尚未转移给后台任务的图像租约。</summary>
            private IImageResourceLease _lease;

            /// <summary>获取租约保护下的第一张待保存图像。</summary>
            public Mat SourceImage { get; private set; }

            /// <summary>获取原子快照时的保守资源字节估算。</summary>
            public long EstimatedBytes { get; private set; }

            /// <summary>创建一份原子保存快照。</summary>
            internal OutputImageSaveSnapshot(Mat sourceImage, long estimatedBytes, IImageResourceLease lease)
            {
                SourceImage = sourceImage ?? throw new ArgumentNullException(nameof(sourceImage));
                EstimatedBytes = Math.Max(1L, estimatedBytes);
                _lease = lease ?? throw new ArgumentNullException(nameof(lease));
            }

            /// <summary>把快照持有的租约原子转移给后台任务；每份快照只能成功转移一次。</summary>
            public IImageResourceLease TransferLease()
            {
                IImageResourceLease lease = Interlocked.Exchange(ref _lease, null);
                if (lease == null)
                    throw new InvalidOperationException("保存快照租约已经转移或释放。");
                return lease;
            }

            /// <summary>释放尚未转移的快照租约；重复调用安全。</summary>
            public void Dispose()
            {
                IImageResourceLease lease = Interlocked.Exchange(ref _lease, null);
                lease?.Dispose();
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
