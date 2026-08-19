using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using Point = OpenCvSharp.Point;
using Size = OpenCvSharp.Size;

namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{
    /// <summary>
    /// 模版创建窗口类
    /// </summary>
    public partial class ColorCreate : FormBase
    {
        // 当前图像
        private Mat CurrentImage;

        // 颜色模板
        private ColorParam colorParam = new ColorParam();

        // 防止频繁触发的锁
        private bool isProcessing = false;

        /// <summary>
        /// 模板窗体自有图像是否已经释放，避免重复Dispose。
        /// </summary>
        private int _imageResourcesReleased;

        private bool _eventsEnabled = true;

        private enum PickMode { None, SinglePoint, ThreePoints }
        private PickMode _pickMode = PickMode.None;
        private List<Scalar> _pickedLabs = new List<Scalar>();

        public ColorCreate()
        {
            InitializeComponent();
            Shown += Form_Shown;
            showImageControl1.MouseImageClick += ShowImageControl1_MouseImageClick;

            flowDirectionPanel1.SelectedControlChanged += FlowDirectionPanel1_SelectedControlChanged;
            flowDirectionPanel1.ControlMoved += FlowDirectionPanel1_ControlMoved;

            // 初始化默认配置
            colorParam.Profiles = new List<ColorProfile>
            {

            };
        }

        private void FlowDirectionPanel1_ControlMoved(object sender, Forms.FlowDirectionPanelContrls.ControlMovedEventArgs e)
        {
            if (e.OriginalIndex < 0 || e.TargetIndex < 0 || e.OriginalIndex == e.TargetIndex) return;
            ColorProfile movedItem = colorParam.Profiles[e.OriginalIndex];
            colorParam.Profiles.RemoveAt(e.OriginalIndex);
            colorParam.Profiles.Insert(e.TargetIndex, movedItem);
        }

        private void FlowDirectionPanel1_SelectedControlChanged(Control obj, int Index)
        {
            if (Index == -1) return;
            _eventsEnabled = false;
            InitDate();
            RunCureentDetection();
            _eventsEnabled = true;
        }

        private void Form_Shown(object sender, EventArgs e) { UpdataImage(); }
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        public void UpdataImage()
        {
            Mat nextImage = null;
            Bitmap nextDisplay = null;
            try
            {
                if (checkBoxUseSub.Checked)
                    nextImage = nodeSubscription1.GetValue<OutputImage>().Bitmaps[0].Clone();
                else
                    nextImage = Cv2.ImRead(textBox1.Text, ImreadModes.Color);

                if (nextImage == null || nextImage.Empty())
                    throw new InvalidOperationException("图像加载失败。");

                nextDisplay = nextImage.ToBitmap();
                ReplaceCurrentImage(nextImage);
                nextImage = null;
                showImageControl1.ImageBitmap = nextDisplay;
                nextDisplay = null;
                showImageControl1.ResetView();
            }
            catch (Exception)
            {
                nextImage?.Dispose();
                nextDisplay?.Dispose();
                ReplaceCurrentImage(null);
                if (!showImageControl1.IsDisposed)
                {
                    showImageControl1.ImageBitmap = null;
                }
            }
        }

        /// <summary>
        /// 替换模板窗体拥有的当前Mat，并释放上一张图。
        /// </summary>
        /// <param name="nextImage">由模板窗体接管的新图，可为空。</param>
        private void ReplaceCurrentImage(Mat nextImage)
        {
            Mat previous = CurrentImage;
            CurrentImage = nextImage;
            previous?.Dispose();
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxUseSub.Checked)
            {
                textBox1.Enabled = false; button1.Enabled = false; nodeSubscription1.Enabled = true;
            }
            else
            {
                textBox1.Enabled = true; button1.Enabled = true; nodeSubscription1.Enabled = false;
            }
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBox1.Text = openFileDialog1.FileName;
                UpdataImage();
            }
        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try { JsonProjectSerializer.SaveProject<ColorParam>(colorParam, saveFileDialog1.FileName); }
                catch (Exception) { MessageBoxTD.Show("保存失败！"); return; }
                MessageBoxTD.Show("保存成功！");
                Close();
            }
        }

        private void button2_Click(object sender, EventArgs e) { UpdataImage(); }

        // 内部结构，用于暂存第一遍扫描的结果
        private struct BlobCandidate
        {
            public double Area;
            public Rect BoundingBox;
            public ColorProfile Profile;
        }

        /// <summary>
        /// 核心检测逻辑 - 识别列表中的所有颜色
        /// </summary>
        private async void RunDetection()
        {
            if (CurrentImage == null || isProcessing) return;
            isProcessing = true;
            Mat sourceSnapshot = null;
            Bitmap resultDisplay = null;
            try
            {
                sourceSnapshot = CurrentImage.Clone();
                if (colorParam.Profiles.Count == 0)
                {
                    resultDisplay = sourceSnapshot.ToBitmap();
                    if (!IsDisposed && !showImageControl1.IsDisposed)
                    {
                        showImageControl1.ImageBitmap = resultDisplay;
                        resultDisplay = null;
                    }
                    return;
                }

                // 如果定义了搜索区域，则计算交集，否则全图
                Rect searchRect = (colorParam.DetectedRoi.Size.Width > 0)
                    ? colorParam.DetectedRoi.BoundingRect().Intersect(new Rect(0, 0, sourceSnapshot.Width, sourceSnapshot.Height))
                    : new Rect(0, 0, sourceSnapshot.Width, sourceSnapshot.Height);

                await Task.Run(() =>
                {
                    using (Mat roiSrc = new Mat(sourceSnapshot, searchRect))
                    using (Mat baseProcessed = new Mat())
                    {
                        // 全局降噪
                        Cv2.GaussianBlur(roiSrc, baseProcessed, new Size(3, 3), 0);

                        // 1. 第一遍扫描：找出所有符合基础条件的色块，并计算总面积
                        List<BlobCandidate> candidates = new List<BlobCandidate>();
                        double totalDetectedArea = 0;

                        for (int i = 0; i < colorParam.Profiles.Count; i++)
                        {
                            var profile = colorParam.Profiles[i];
                            List<BlobCandidate> profileCandidates = new List<BlobCandidate>(); // 暂存当前Profile的候选

                            using (Mat processed = baseProcessed.Clone())
                            using (Mat lab = new Mat())
                            using (Mat mask = new Mat())
                            {
                                // --- 图像处理管线 (Gamma -> Structure -> Lab -> Morph) ---
                                ProcessImagePipeline(processed, lab, mask, profile, out Mat rawMask);
                                using (rawMask)
                                {
                                    // 轮廓查找
                                    Point[][] contours;
                                    HierarchyIndex[] hierarchy;
                                    Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                                    foreach (var contour in contours)
                                    {
                                        // 计算轮廓的最小外接矩形
                                        RotatedRect minRect = Cv2.MinAreaRect(contour);
                                        // 计算最小外接矩形的面积（宽 × 高）
                                        double area = minRect.Size.Width * minRect.Size.Height;

                                        // 基础降噪筛选 (MinArea)
                                        if (area < profile.MinArea) continue;

                                        bool isColorMatch = true;
                                        if (profile.EnableMeanCheck)
                                        {
                                            using (Mat singleBlobMask = new Mat(mask.Size(), MatType.CV_8U, Scalar.All(0)))
                                            {
                                                Cv2.DrawContours(singleBlobMask, new[] { contour }, -1, Scalar.All(255), -1);
                                                if (rawMask != null && !rawMask.Empty())
                                                {
                                                    Cv2.BitwiseAnd(singleBlobMask, rawMask, singleBlobMask);
                                                }
                                                Scalar meanVal = Cv2.Mean(lab, singleBlobMask);

                                                // 独立通道判定 (Box Model)
                                                double diffL = Math.Abs(meanVal.Val0 - profile.TargetLab.Val0);
                                                double diffA = Math.Abs(meanVal.Val1 - profile.TargetLab.Val1);
                                                double diffB = Math.Abs(meanVal.Val2 - profile.TargetLab.Val2);

                                                if (diffL > profile.ToleranceL || diffA > profile.ToleranceA || diffB > profile.ToleranceB) isColorMatch = false;
                                            }
                                        }

                                        if (isColorMatch)
                                        {
                                            Rect r = Cv2.BoundingRect(contour);
                                            // 坐标还原到原图
                                            r.X += searchRect.X;
                                            r.Y += searchRect.Y;

                                            profileCandidates.Add(new BlobCandidate
                                            {
                                                Area = area,
                                                BoundingBox = r,
                                                Profile = profile
                                            });
                                        }
                                    }
                                }
                            }

                            // --- Screening Logic (筛选模式) ---
                            if (profileCandidates.Count > 0)
                            {
                                if (profile.ScreeningModel == "MaxArea")
                                {
                                    var best = profileCandidates.OrderByDescending(x => x.Area).First();
                                    profileCandidates.Clear();
                                    profileCandidates.Add(best);
                                }
                                else if (profile.ScreeningModel == "MinArea")
                                {
                                    var best = profileCandidates.OrderBy(x => x.Area).First();
                                    profileCandidates.Clear();
                                    profileCandidates.Add(best);
                                }
                                // "All" do nothing
                            }

                            // 将筛选后的结果加入总列表并累计面积
                            foreach (var c in profileCandidates)
                            {
                                candidates.Add(c);
                                totalDetectedArea += c.Area;
                            }
                        }

                        // 2. 第二遍：根据总面积计算占比，并进行最终判定和绘制
                        // 先在 Mat 上画框 (OpenCV 效率高)
                        using (Mat drawMat = sourceSnapshot.Clone())
                        {
                            var resultsForText = new List<(string Info, Color Col)>();

                        foreach (var blob in candidates)
                        {
                            // 计算占比：当前块面积 / 所有识别块的总面积
                            double ratio = (totalDetectedArea > 0)
                                ? Math.Round(blob.Area / totalDetectedArea * 100.0, 2)
                                : 0;

                            bool isOk = true;

                            // A. 面积判定 (使用 MinWidth/MaxWidth)
                            if (blob.Profile.IsOpenWidthCheck)
                            {
                                if (blob.Area < blob.Profile.MinWidth || blob.Area > blob.Profile.MaxWidth)
                                    isOk = false;
                            }

                            // B. 占比判定 (使用 MinWidthB/MaxWidthB)
                            if (blob.Profile.IsOpenWidthBCheck)
                            {
                                if (isOk == false) isOk = true;
                                if (ratio < blob.Profile.MinWidthB || ratio > blob.Profile.MaxWidthB)
                                    isOk = false;
                            }

                            if (blob.Profile.IsOpenWidthCheck == false && blob.Profile.IsOpenWidthBCheck == false)
                            {
                                isOk = true;
                            }

                            Scalar drawColor = isOk ? Scalar.Lime : Scalar.Red;
                            Color sysColor = isOk ? Color.Lime : Color.Red;
                            string status = isOk ? "OK" : "NG";

                            // 绘制矩形
                            Cv2.Rectangle(drawMat, blob.BoundingBox, drawColor, 2);

                            // 收集文本信息以便稍后用 Graphics 绘制
                            string infoText = $"{blob.Profile.Name} [{status}]";
                            if (blob.Profile.IsOpenWidthCheck) infoText += $" Area:{blob.Area}";
                            if (blob.Profile.IsOpenWidthBCheck) infoText += $" Ratio:{ratio:F1}%";

                            resultsForText.Add((infoText, sysColor));
                        }

                        // 转为 Bitmap 进行文字绘制 (解决中文乱码和堆叠问题)
                        resultDisplay = drawMat.ToBitmap();

                            using (Graphics g = Graphics.FromImage(resultDisplay))
                            using (Font font = new Font("Microsoft YaHei", 9, FontStyle.Bold))
                            using (Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                            {
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                            // 在左侧绘制半透明背景列
                            int lineHeight = 20;
                            int boxWidth = 320; // 足够宽以显示信息
                            int boxHeight = (resultsForText.Count * lineHeight) + 30;

                            // 绘制半透明背景
                            g.FillRectangle(backgroundBrush, 0, 0, boxWidth, boxHeight);
                            g.DrawString("检测结果 (左侧列表):", font, Brushes.White, 5, 5);

                            for (int i = 0; i < resultsForText.Count; i++)
                            {
                                var item = resultsForText[i];
                                using (Brush b = new SolidBrush(item.Col))
                                {
                                    g.DrawString($"{i + 1}. {item.Info}", font, b, 5, 25 + (i * lineHeight));
                                }
                            }
                            }
                        }
                    }
                });

                if (resultDisplay != null && !IsDisposed && !showImageControl1.IsDisposed)
                {
                    showImageControl1.ImageBitmap = resultDisplay;
                    resultDisplay = null;
                }
            }
            finally
            {
                resultDisplay?.Dispose();
                sourceSnapshot?.Dispose();
                isProcessing = false;
            }
        }

        /// <summary>
        /// 识别当前颜色 (逻辑同 RunDetection，但仅处理选中的 Profile)
        /// </summary>
        private async void RunCureentDetection()
        {
            if (CurrentImage == null || isProcessing) return;
            int index = flowDirectionPanel1.SelectedIndex;
            if (index == -1) return;
            ColorProfile profile = colorParam.Profiles[index];
            isProcessing = true;
            Mat sourceSnapshot = null;
            Bitmap resultDisplay = null;
            try
            {
                sourceSnapshot = CurrentImage.Clone();
                Rect searchRect = (colorParam.DetectedRoi.Size.Width > 0)
                   ? colorParam.DetectedRoi.BoundingRect().Intersect(new Rect(0, 0, sourceSnapshot.Width, sourceSnapshot.Height))
                   : new Rect(0, 0, sourceSnapshot.Width, sourceSnapshot.Height);

                await Task.Run(() =>
                {
                    using (Mat roiSrc = new Mat(sourceSnapshot, searchRect))
                    using (Mat baseProcessed = new Mat())
                    {
                        Cv2.GaussianBlur(roiSrc, baseProcessed, new Size(3, 3), 0);

                    List<BlobCandidate> candidates = new List<BlobCandidate>();
                    double totalDetectedArea = 0;

                    // 1. 第一遍：只针对当前 Profile 查找块
                    using (Mat processed = baseProcessed.Clone())
                    using (Mat lab = new Mat())
                    using (Mat mask = new Mat())
                    {
                        ProcessImagePipeline(processed, lab, mask, profile, out Mat rawMask);
                        using (rawMask)
                        {
                            Point[][] contours;
                            HierarchyIndex[] hierarchy;
                            Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                            foreach (var contour in contours)
                            {
                                double area = Cv2.ContourArea(contour);
                                if (area < profile.MinArea) continue;

                                bool isColorMatch = true;
                                if (profile.EnableMeanCheck)
                                {
                                    using (Mat singleBlobMask = new Mat(mask.Size(), MatType.CV_8U, Scalar.All(0)))
                                    {
                                        Cv2.DrawContours(singleBlobMask, new[] { contour }, -1, Scalar.All(255), -1);
                                        if (rawMask != null && !rawMask.Empty())
                                        {
                                            Cv2.BitwiseAnd(singleBlobMask, rawMask, singleBlobMask);
                                        }
                                        Scalar meanVal = Cv2.Mean(lab, singleBlobMask);
                                        // 独立通道判定
                                        double diffL = Math.Abs(meanVal.Val0 - profile.TargetLab.Val0);
                                        double diffA = Math.Abs(meanVal.Val1 - profile.TargetLab.Val1);
                                        double diffB = Math.Abs(meanVal.Val2 - profile.TargetLab.Val2);

                                        if (diffL > profile.ToleranceL || diffA > profile.ToleranceA || diffB > profile.ToleranceB) isColorMatch = false;
                                    }
                                }

                                if (isColorMatch)
                                {
                                    Rect r = Cv2.BoundingRect(contour);
                                    r.X += searchRect.X; r.Y += searchRect.Y;
                                    candidates.Add(new BlobCandidate { Area = area, BoundingBox = r, Profile = profile });
                                }
                            }
                        }
                    }

                    // --- Screening Logic (筛选模式) ---
                    if (candidates.Count > 0)
                    {
                        if (profile.ScreeningModel == "MaxArea")
                        {
                            var best = candidates.OrderByDescending(x => x.Area).First();
                            candidates.Clear();
                            candidates.Add(best);
                        }
                        else if (profile.ScreeningModel == "MinArea")
                        {
                            var best = candidates.OrderBy(x => x.Area).First();
                            candidates.Clear();
                            candidates.Add(best);
                        }
                        // "All" do nothing
                    }

                    // 累计面积 (用于占比计算)
                    foreach (var c in candidates) totalDetectedArea += c.Area;

                    // 2. 第二遍：绘制结果
                    using (Mat drawMat = sourceSnapshot.Clone())
                    {
                        var resultsForText = new List<(string Info, Color Col)>();

                        foreach (var blob in candidates)
                        {
                            // 计算占比
                            double ratio = (totalDetectedArea > 0) ? (blob.Area / totalDetectedArea * 100.0) : 0;
                            bool isOk = true;

                            // 面积判定
                            if (blob.Profile.IsOpenWidthCheck)
                            {
                                if (blob.Area < blob.Profile.MinWidth || blob.Area > blob.Profile.MaxWidth) isOk = false;
                            }

                            // 占比判定
                            if (blob.Profile.IsOpenWidthBCheck)
                            {
                                if (ratio < blob.Profile.MinWidthB || ratio > blob.Profile.MaxWidthB) isOk = false;
                            }

                            Scalar drawColor = isOk ? Scalar.Lime : Scalar.Red;
                            Color sysColor = isOk ? Color.Lime : Color.Red;
                            string status = isOk ? "OK" : "NG";

                            Cv2.Rectangle(drawMat, blob.BoundingBox, drawColor, 2);

                            string infoText = $"{blob.Profile.Name} [{status}]";
                            if (blob.Profile.IsOpenWidthCheck) infoText += $" Area:{blob.Area}";
                            if (blob.Profile.IsOpenWidthBCheck) infoText += $" Ratio:{ratio:F1}%";

                            resultsForText.Add((infoText, sysColor));
                        }

                        // 文字绘制逻辑
                        resultDisplay = drawMat.ToBitmap();
                        using (Graphics g = Graphics.FromImage(resultDisplay))
                        using (Font font = new Font("Microsoft YaHei", 9, FontStyle.Bold))
                        using (Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                        {
                            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                            int lineHeight = 20;
                            int boxWidth = 260;
                            int boxHeight = (resultsForText.Count * lineHeight) + 30;

                            g.FillRectangle(backgroundBrush, 0, 0, boxWidth, boxHeight);
                            g.DrawString("当前颜色结果:", font, Brushes.White, 5, 5);

                            for (int i = 0; i < resultsForText.Count; i++)
                            {
                                var item = resultsForText[i];
                                using (Brush b = new SolidBrush(item.Col))
                                {
                                    g.DrawString($"{i + 1}. {item.Info}", font, b, 5, 25 + (i * lineHeight));
                                }
                            }
                        }
                    }
                    }
                });

                if (resultDisplay != null && !IsDisposed && !showImageControl1.IsDisposed)
                {
                    showImageControl1.ImageBitmap = resultDisplay;
                    resultDisplay = null;
                }
            }
            finally
            {
                resultDisplay?.Dispose();
                sourceSnapshot?.Dispose();
                isProcessing = false;
            }
        }

        // 提取的公共图像处理管线，减少代码重复
        // 核心修改：使用独立通道容差 (AbsDiff) 而非欧氏距离，有效解决黑绿混淆
        private void ProcessImagePipeline(Mat processed, Mat lab, Mat mask, ColorProfile profile, out Mat rawMask)
        {
            rawMask = null;
            // 1. Gamma
            if (Math.Abs(profile.Gamma - 1.0) > 0.05)
            {
                byte[] lut = new byte[256];
                for (int j = 0; j < 256; j++) lut[j] = (byte)Math.Min(255, Math.Pow(j / 255.0, 1.0 / profile.Gamma) * 255.0);
                using (Mat lutMat = new Mat(1, 256, MatType.CV_8U))
                {
                    for (int k = 0; k < 256; k++) lutMat.Set<byte>(0, k, lut[k]);
                    Cv2.LUT(processed, lutMat, processed);
                }
            }

            // 2. Structure
            using (Mat structureMask = new Mat())
            {
                if (profile.UseStructure)
                {
                    using (Mat gray = new Mat())
                    using (Mat kernelStr = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(profile.StructureKernelSize, profile.StructureKernelSize)))
                    {
                        Cv2.CvtColor(processed, gray, ColorConversionCodes.BGR2GRAY);
                        Cv2.MorphologyEx(gray, structureMask, profile.StructureOp, kernelStr);
                        Cv2.Threshold(structureMask, structureMask, profile.StructureThreshold, 255, ThresholdTypes.Binary);
                    }
                }

                // 3. Lab (Independent Channel Thresholding)
                Cv2.CvtColor(processed, lab, ColorConversionCodes.BGR2Lab);
                using (Mat diff = new Mat())
                using (Mat targetMat = new Mat(lab.Size(), lab.Type()))
                {
                    targetMat.SetTo(profile.TargetLab);
                    using (Mat d1 = new Mat()) using (Mat d2 = new Mat())
                    {
                        Cv2.Subtract(lab, targetMat, d1); Cv2.Subtract(targetMat, lab, d2); Cv2.Add(d1, d2, diff);
                    }

                    Mat[] channels = Cv2.Split(diff);
                    using (Mat dL = channels[0]) using (Mat dA = channels[1]) using (Mat dB = channels[2])
                    using (Mat maskL = new Mat()) using (Mat maskA = new Mat()) using (Mat maskB = new Mat())
                    {
                        // 分别对 L, A, B 通道进行阈值判定 (Box Model)
                        // A 通道容差可独立设置，严格区分黑绿
                        Cv2.Threshold(dL, maskL, profile.ToleranceL, 255, ThresholdTypes.BinaryInv);
                        Cv2.Threshold(dA, maskA, profile.ToleranceA, 255, ThresholdTypes.BinaryInv);
                        Cv2.Threshold(dB, maskB, profile.ToleranceB, 255, ThresholdTypes.BinaryInv);

                        // 合并掩膜 mask = L & A & B
                        Cv2.BitwiseAnd(maskL, maskA, mask);
                        Cv2.BitwiseAnd(mask, maskB, mask);
                    }
                }
                if (profile.UseStructure && !structureMask.Empty()) Cv2.BitwiseAnd(mask, structureMask, mask);
            }

            // 4. Morph
            if (profile.UseMorphology)
            {
                rawMask = mask.Clone();
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new Size(profile.MorphKernelSize, profile.MorphKernelSize)))
                    Cv2.MorphologyEx(mask, mask, profile.MorphOp, kernel, iterations: profile.MorphIterations);
            }
        }

        private void toolStripButton1_Click_1(object sender, EventArgs e)
        {
            showImageControl1.ClearAllRoi(); showImageControl1.AddRoiRotatedRect(50, 50, 0, 50, 50, 1, Color.Green, "取色框");
        }

        private void AddColor_Click(object sender, EventArgs e)
        {
            var rois = showImageControl1.GetAllRotatedRectInfos();
            if (rois.Count == 0 || CurrentImage == null)
            {
                MessageBox.Show("请先加载图像并绘制模板区域！"); return;
            }
            var r = rois[0];
            RotatedRect pickRect = new RotatedRect(new Point2f(r.Column, r.Row), new Size2f(r.Length1 * 2, r.Length2 * 2), (float)(r.Phi * 180.0 / Math.PI));
            Rect bounding = pickRect.BoundingRect().Intersect(new Rect(0, 0, CurrentImage.Width, CurrentImage.Height));
            using (Mat roiImg = new Mat(CurrentImage, bounding))
            using (Mat labImg = new Mat())
            using (Mat mask = new Mat(roiImg.Size(), MatType.CV_8U, Scalar.All(0)))
            {
                Point[] pts = pickRect.Points().Select(p => new Point(p.X - bounding.X, p.Y - bounding.Y)).ToArray();
                Cv2.FillConvexPoly(mask, pts, Scalar.All(255));
                Cv2.CvtColor(roiImg, labImg, ColorConversionCodes.BGR2Lab);
                using (Mat mean = new Mat())
                using (Mat stdDev = new Mat())
                {
                    Cv2.MeanStdDev(labImg, mean, stdDev, mask);
                    double mL = mean.At<double>(0), mA = mean.At<double>(1), mB = mean.At<double>(2);
                    double sL = stdDev.At<double>(0), sA = stdDev.At<double>(1), sB = stdDev.At<double>(2);
                    var newProfile = new ColorProfile
                    {
                        Name = $"Auto_{colorParam.Profiles.Count + 1}",
                        TargetLab = new Scalar(mL, mA, mB),
                        ToleranceL = Math.Max(25, sL * 3),
                        // 自动计算 A/B 独立容差
                        ToleranceA = Math.Max(15, sA * 3),
                        ToleranceB = Math.Max(15, sB * 3),
                        UseMorphology = true,
                        MorphOp = MorphTypes.Close,
                        MorphKernelSize = 5,
                        MorphIterations = 1,
                        MinArea = 100,
                        EnableMeanCheck = true,
                        MinWidth = 0,
                        MaxWidth = 9999
                    };
                    colorParam.Profiles.Add(newProfile);
                    AddProfileToUI(newProfile);
                }
            }
        }

        private void AddProfileToUI(ColorProfile profile)
        {
            Button button = new Button() { Text = profile.Name };
            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem deleteMenuItem = new ToolStripMenuItem("删除");
            deleteMenuItem.Click += (_, __) =>
            {
                int index = flowDirectionPanel1.SelectedIndex;
                if (index != -1)
                {
                    flowDirectionPanel1.RemoveControl(button);
                    colorParam.Profiles.RemoveAt(index);
                }
                contextMenu.Dispose();
            };
            contextMenu.Items.Add(deleteMenuItem);
            button.ContextMenuStrip = contextMenu;
            flowDirectionPanel1.AddControl(button);
        }

        private void StartPickMode(PickMode mode)
        {
            if (CurrentImage == null) { MessageBoxTD.Show("请先加载图像！"); return; }
            _pickMode = mode;
            _pickedLabs.Clear();
            SetUIAccess(false);
            MessageBoxTD.Show($"已进入{(_pickMode == PickMode.SinglePoint ? "单点" : "三点")}取色模式。\n请点击图像上的目标位置进行取色。", "提示", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void SetUIAccess(bool enabled)
        {
            panel4.Enabled = enabled; // 侧边栏
            panel2.Enabled = enabled; // 保存/导入按钮
            toolStrip1.Enabled = enabled; // 工具栏
                                          // 只有图像控件保持活跃
        }

        private void ShowImageControl1_MouseImageClick(PointF pt)
        {
            if (_pickMode == PickMode.None || CurrentImage == null) return;

            int x = (int)pt.X;
            int y = (int)pt.Y;
            if (x < 0 || x >= CurrentImage.Width || y < 0 || y >= CurrentImage.Height) return;

            // 提取颜色并转为 LAB
            Vec3b bgr = CurrentImage.At<Vec3b>(y, x);
            using (Mat pixelMat = new Mat(1, 1, MatType.CV_8UC3, new Scalar(bgr.Item0, bgr.Item1, bgr.Item2)))
            using (Mat labMat = new Mat())
            {
                Cv2.CvtColor(pixelMat, labMat, ColorConversionCodes.BGR2Lab);
                Vec3b labPixel = labMat.At<Vec3b>(0, 0);
                Scalar lab = new Scalar(labPixel.Item0, labPixel.Item1, labPixel.Item2);

                string msg = $"当前点击位置颜色 LAB: ({(int)lab.Val0}, {(int)lab.Val1}, {(int)lab.Val2})\n是否确认将该颜色加入采样？";
                if (MessageBoxTD.Show(msg, "取色确认", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    _pickedLabs.Add(lab);
                    int target = (_pickMode == PickMode.SinglePoint ? 1 : 3);

                    if (_pickedLabs.Count >= target)
                    {
                        // 均值计算
                        double avgL = _pickedLabs.Average(l => l.Val0);
                        double avgA = _pickedLabs.Average(l => l.Val1);
                        double avgB = _pickedLabs.Average(l => l.Val2);

                        CreateProfileFromPickedColor(new Scalar(avgL, avgA, avgB));
                        _pickMode = PickMode.None;
                        SetUIAccess(true);
                        MessageBoxTD.Show("取色成功！已根据采样点生成新的颜色配置。", "完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        RunCureentDetection();
                    }
                    else
                    {
                        MessageBoxTD.Show($"已采样 {_pickedLabs.Count} 个点，还需点击 {target - _pickedLabs.Count} 个点。", "继续取色", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }

        private void CreateProfileFromPickedColor(Scalar lab)
        {
            var newProfile = new ColorProfile
            {
                Name = $"Pick_{colorParam.Profiles.Count + 1}",
                TargetLab = lab,
                ToleranceL = 30,
                ToleranceA = 20,
                ToleranceB = 20,
                UseMorphology = true,
                MorphOp = MorphTypes.Close,
                MorphKernelSize = 5,
                MorphIterations = 1,
                MinArea = 100,
                EnableMeanCheck = true,
                MinWidth = 0,
                MaxWidth = 9999
            };
            colorParam.Profiles.Add(newProfile);
            AddProfileToUI(newProfile);
        }

        private void btnSinglePick_Click(object sender, EventArgs e) { StartPickMode(PickMode.SinglePoint); }
        private void btnTriplePick_Click(object sender, EventArgs e) { StartPickMode(PickMode.ThreePoints); }

        /// <summary>
        /// 释放模板窗体最后持有的源Mat，并解除图像控件事件。
        /// </summary>
        private void ReleaseImageResources()
        {
            if (System.Threading.Interlocked.Exchange(ref _imageResourcesReleased, 1) != 0)
                return;

            showImageControl1.MouseImageClick -= ShowImageControl1_MouseImageClick;
            ReplaceCurrentImage(null);
        }

        private void button4_Click(object sender, EventArgs e)
        {
            if (openFileDialog2.ShowDialog() == DialogResult.OK)
            {
                colorParam = JsonProjectSerializer.LoadProject<ColorParam>(openFileDialog2.FileName);
                flowDirectionPanel1.Controls.Clear();
                foreach (var item in colorParam.Profiles)
                {
                    AddProfileToUI(item);
                }
            }
        }
        private void pictureBox1_Click(object sender, EventArgs e) { flowDirectionPanel1.MoveSelectedControlUp(); }
        private void pictureBox2_Click(object sender, EventArgs e) { flowDirectionPanel1.MoveSelectedControlDown(); }
        private void toolStripTextBox1_TextChanged(object sender, EventArgs e) { }
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            showImageControl1.ClearAllRoi(); showImageControl1.AddRoiRotatedRect(200, 200, 0, 200, 200, 1, Color.Green, "搜索框");
        }
        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            var rois = showImageControl1.GetAllRotatedRectInfos();
            if (rois.Count > 0)
            {
                var r = rois[0];
                colorParam.DetectedRoi = new RotatedRect(new Point2f(r.Column, r.Row), new Size2f(r.Length1 * 2, r.Length2 * 2), (float)(r.Phi * 180.0 / Math.PI));
                showImageControl1.ClearAllRoi(); RunCureentDetection();
            }
        }

        /// <summary>
        /// 初始化界面数据 (从 Profile 加载到 UI)
        /// </summary>
        void InitDate()
        {
            int index = flowDirectionPanel1.SelectedIndex;
            if (index == -1) return;

            var profile = colorParam.Profiles[index];

            // 1. 基础信息
            textBox2.Text = profile.Name;
            textBox3.Text = profile.TriggerVal;

            // 2. 结构提取 (预处理)
            if (!profile.UseStructure)
                comboBox1.SelectedIndex = 0; // 无
            else if (profile.StructureOp == MorphTypes.TopHat)
                comboBox1.SelectedIndex = 1; // TopHat
            else
                comboBox1.SelectedIndex = 2; // BlackHat

            bool enableStructure = (comboBox1.SelectedIndex != 0);
            uiIntegerUpDown1.Enabled = enableStructure;
            uiDoubleUpDown1.Enabled = enableStructure;

            uiIntegerUpDown1.Value = profile.StructureKernelSize;
            uiDoubleUpDown1.Value = profile.StructureThreshold;

            // 3. 颜色容差
            uiDoubleUpDown2.Value = profile.ToleranceL;

            // 使用 ToleranceA 初始化 UI 滑块 (假设界面上只有这一个色差滑块)
            // 实际上 ToleranceA 和 ToleranceB 在界面调节时会同步
            uiDoubleUpDown3.Value = profile.ToleranceA;
            uiDoubleUpDown6.Value = profile.ToleranceB;

            // 4. 形态学处理
            checkBox1.Checked = profile.UseMorphology;

            bool enableMorph = profile.UseMorphology;
            comboBox2.Enabled = enableMorph;
            uiIntegerUpDown4.Enabled = enableMorph;
            uiIntegerUpDown5.Enabled = enableMorph;

            if (profile.MorphOp == MorphTypes.Close) comboBox2.SelectedIndex = 0;
            else if (profile.MorphOp == MorphTypes.Open) comboBox2.SelectedIndex = 1;
            else if (profile.MorphOp == MorphTypes.Dilate) comboBox2.SelectedIndex = 2;
            else comboBox2.SelectedIndex = 3; // Erode

            uiIntegerUpDown4.Value = profile.MorphKernelSize;
            uiIntegerUpDown5.Value = profile.MorphIterations;

            // 5. 尺寸/面积筛选
            if (profile.ScreeningModel == "All")
                comboBox3.SelectedIndex = 0;
            else if (profile.ScreeningModel == "MinArea")
                comboBox3.SelectedIndex = 1;
            else
                comboBox3.SelectedIndex = 2;

            uiIntegerUpDown6.Value = profile.MinArea;

            // 面积判定
            uiCheckBox1.Checked = profile.IsOpenWidthCheck;
            uiIntegerUpDown2.Value = profile.MinWidth;
            uiIntegerUpDown3.Value = profile.MaxWidth;

            uiIntegerUpDown2.Enabled = profile.IsOpenWidthCheck;
            uiIntegerUpDown3.Enabled = profile.IsOpenWidthCheck;

            // 占比判定
            uiCheckBox2.Checked = profile.IsOpenWidthBCheck;
            uiDoubleUpDown4.Value = profile.MinWidthB;
            uiDoubleUpDown5.Value = profile.MaxWidthB;

            uiDoubleUpDown4.Enabled = profile.IsOpenWidthBCheck;
            uiDoubleUpDown5.Enabled = profile.IsOpenWidthBCheck;
        }

        private void uiIntegerUpDown1_ValueChanged(object sender, int value) { UpdataDate(); }
        private void uiDoubleUpDown3_ValueChanged(object sender, double value) { UpdataDate(); }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) { UpdataDate(); }
        private void checkBox1_CheckedChanged_1(object sender, EventArgs e) { UpdataDate(); }
        private void textBox2_TextChanged(object sender, EventArgs e) { UpdataDate(); }
        private void textBox3_TextChanged(object sender, EventArgs e)
        {
            UpdataDate();
        }

        /// <summary>
        /// 更新数据 (从 UI 保存到 Profile)
        /// </summary>
        void UpdataDate()
        {
            if (!_eventsEnabled) return;
            int index = flowDirectionPanel1.SelectedIndex;
            if (index == -1) return;

            var profile = colorParam.Profiles[index];

            // 1. 基础信息
            profile.Name = textBox2.Text;
            ((Button)flowDirectionPanel1.Controls[index]).Text = textBox2.Text;
            profile.TriggerVal = textBox3.Text;

            // 2. 结构提取
            if (comboBox1.SelectedIndex == 0)
            {
                profile.UseStructure = false;
            }
            else if (comboBox1.SelectedIndex == 1)
            {
                profile.UseStructure = true;
                profile.StructureOp = MorphTypes.TopHat;
            }
            else if (comboBox1.SelectedIndex == 2)
            {
                profile.UseStructure = true;
                profile.StructureOp = MorphTypes.BlackHat;
            }

            bool enableStructure = (comboBox1.SelectedIndex != 0);
            uiIntegerUpDown1.Enabled = enableStructure;
            uiDoubleUpDown1.Enabled = enableStructure;

            profile.StructureKernelSize = uiIntegerUpDown1.Value;
            profile.StructureThreshold = uiDoubleUpDown1.Value;

            // 3. 颜色容差
            profile.ToleranceL = uiDoubleUpDown2.Value;

            // 关键：将 UI 的色差值同时赋给 A 和 B，实现 Box Model
            profile.ToleranceA = uiDoubleUpDown3.Value;
            profile.ToleranceB = uiDoubleUpDown6.Value;

            // 4. 形态学
            profile.UseMorphology = checkBox1.Checked;

            bool enableMorph = checkBox1.Checked;
            comboBox2.Enabled = enableMorph;
            uiIntegerUpDown4.Enabled = enableMorph;
            uiIntegerUpDown5.Enabled = enableMorph;

            if (comboBox2.SelectedIndex == 0) profile.MorphOp = MorphTypes.Close;
            else if (comboBox2.SelectedIndex == 1) profile.MorphOp = MorphTypes.Open;
            else if (comboBox2.SelectedIndex == 2) profile.MorphOp = MorphTypes.Dilate;
            else if (comboBox2.SelectedIndex == 3) profile.MorphOp = MorphTypes.Erode;

            profile.MorphKernelSize = uiIntegerUpDown4.Value;
            profile.MorphIterations = uiIntegerUpDown5.Value;

            // 5. 尺寸/面积
            profile.ScreeningModel = comboBox3.SelectedItem.ToString();
            profile.MinArea = uiIntegerUpDown6.Value;

            // 面积判定
            profile.IsOpenWidthCheck = uiCheckBox1.Checked;
            profile.MinWidth = uiIntegerUpDown2.Value;
            profile.MaxWidth = uiIntegerUpDown3.Value;

            uiIntegerUpDown2.Enabled = uiCheckBox1.Checked;
            uiIntegerUpDown3.Enabled = uiCheckBox1.Checked;

            // 占比判定
            profile.IsOpenWidthBCheck = uiCheckBox2.Checked;
            profile.MinWidthB = uiDoubleUpDown4.Value;
            profile.MaxWidthB = uiDoubleUpDown5.Value;

            uiDoubleUpDown4.Enabled = uiCheckBox2.Checked;
            uiDoubleUpDown5.Enabled = uiCheckBox2.Checked;

            RunCureentDetection();
        }

        private void toolStripButton4_Click(object sender, EventArgs e) { RunDetection(); }
        private void uiCheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            uiIntegerUpDown2.Enabled = uiCheckBox1.Checked; uiIntegerUpDown3.Enabled = uiCheckBox1.Checked;
            uiDoubleUpDown4.Enabled = uiCheckBox2.Checked; uiDoubleUpDown5.Enabled = uiCheckBox2.Checked;
            UpdataDate();
        }

        
    }
}
