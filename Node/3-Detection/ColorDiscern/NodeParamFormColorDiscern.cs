using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Device.Modbus;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using Point = OpenCvSharp.Point;

namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{
    public partial class NodeParamFormColorDiscern : FormBase, INodeParamForm
    {
        private Process process;//所属流程
        private NodeBase node;//所属节点
        private static ColorCreate colorCreate = null; // 模版创建对象

        // 图像资源缓存
        private Bitmap src = null;  // 原图 Bitmap (用于 UI 显示)
        private Mat srcMat = null;  // 原图 Mat (用于算法处理，避免重复转换)

        private bool isOpenGeiGround;  //是否开启通信检测项
        private IDevice device;   //通信设备
        private string deviceName;  // 设备名称
        public string address; // 通信监听的地址

        private bool isOpenSort;  //是否开启排序
        private int sortModel;  //排序方式

        // 颜色模板
        private ColorParam colorParam = new ColorParam();

        public NodeParamFormColorDiscern(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            this.node = nodeBase;
            Shown += NodeParamFormQRCodeIdentification_Shown;
            Shown += NodeParamFormColorDiscern_Shown;
        }

        private void NodeParamFormColorDiscern_Shown(object sender, EventArgs e)
        {
            InitDevice();
        }

        private void NodeParamFormQRCodeIdentification_Shown(object sender, EventArgs e)
        {
            UpdataImage();
        }

        public INodeParam Params { get; set; }

        // --- 参数加载与保存 ---
        public void SetParam2Form()
        {
            if (Params is NodeParamColorDiscern param)
            {
                try
                {
                    textBoxModelPath.Text = param.TemplateFileName;
                    nodeSubscription1.SetText(param.Text1, param.Text2);

                    radioButton2.Checked = param.IsOpenGeiGround;
                    radioButton1.Checked = !param.IsOpenGeiGround;

                    param.Device = Solution.Instance.AllDevices.Find(dev => dev.UserDefinedName == param.DeviceName);
                    comboBoxDevice.Text = param.DeviceName;
                    device = param.Device;
                    ListeningAddressTextbox.Text = param.Address;
                    address = param.Address;

                    checkBox1.Checked = param.IsOpenSort;
                    comboBox1.SelectedIndex = param.SrotModel;

                    if (File.Exists(param.TemplateFileName))
                    {
                        colorParam = JsonProjectSerializer.LoadProject<ColorParam>(param.TemplateFileName);
                    }
                }
                catch (Exception) { throw; }
            }
        }

        public void SetNodeBelong(NodeBase node) { nodeSubscription1.Init(node); }

        private async void button3_Click(object sender, EventArgs e)
        {
            await process.RunForUpdateImages(node);
            UpdataImage();
        }

        /// <summary>
        /// 更新图像资源 (优化的核心：同时维护 Bitmap 和 Mat)
        /// </summary>
        public void UpdataImage(bool isShow = true)
        {
            try
            {
                OutputImage outputImage = nodeSubscription1.GetValue<OutputImage>();
                Bitmap newBmp = outputImage.Bitmaps[0].ToBitmap();

                // 释放旧资源
                if (src != null) src.Dispose();
                src = newBmp;

                // 更新 Mat 缓存 (避免 MatchTemplateAsync 每次运行时重复转换)
                if (srcMat != null && !srcMat.IsDisposed) srcMat.Dispose();
                srcMat = BitmapConverter.ToMat(src);

                if (isShow) showImageControl1.ImageBitmap = src;
            }
            catch (Exception)
            {
                src = null;
                // Mat 也要置空，防止使用已释放的对象
                if (srcMat != null) { srcMat.Dispose(); srcMat = null; }
            }
        }

        private async void buttonRun_Click(object sender, EventArgs e)
        {
            try { await MatchTemplateAsync(); }
            catch (Exception ex) { MessageBoxTD.Show($"模版匹配异常：原因：{ex.Message}"); }
        }

        private static Mat BitmapToColorMat(Bitmap bitmap) { return BitmapConverter.ToMat(bitmap); }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams()) Hide();
        }

        private bool SaveParams()
        {
            try
            {
                NodeParamColorDiscern param = new NodeParamColorDiscern();
                param.TemplateFileName = textBoxModelPath.Text;
                param.Text1 = nodeSubscription1.GetText1();
                param.Text2 = nodeSubscription1.GetText2();

                colorParam = JsonProjectSerializer.LoadProject<ColorParam>(textBoxModelPath.Text);

                param.IsOpenGeiGround = isOpenGeiGround;
                param.Device = device;
                param.DeviceName = deviceName;
                param.Address = address;

                param.IsOpenSort = isOpenSort;
                param.SrotModel = sortModel;

                Params = param;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"参数设置异常，原因：{ex.Message}");
                return false;
            }
            return true;
        }

        private void button2_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBoxModelPath.Text = openFileDialog1.FileName;
                colorParam = JsonProjectSerializer.LoadProject<ColorParam>(openFileDialog1.FileName);
            }
        }

        private void label1_Click(object sender, EventArgs e)
        {
            colorCreate = new ColorCreate();
            colorCreate.SetNodeBelong(node);
            colorCreate.ShowDialog();
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            bool isComm = radioButton2.Checked;
            comboBoxDevice.Enabled = isComm;
            ListeningAddressTextbox.Enabled = isComm;
            checkBox1.Enabled = !isComm;
            comboBox1.Enabled = !isComm;
            isOpenGeiGround = isComm;
        }

        private void InitDevice()
        {
            string text1 = comboBoxDevice.Text;
            comboBoxDevice.Items.Clear();
            comboBoxDevice.Items.Add("[未设置]");
            foreach (var plc in Solution.Instance.PlcDevices) comboBoxDevice.Items.Add(plc.UserDefinedName);
            foreach (var mod in Solution.Instance.ModbusDevices) comboBoxDevice.Items.Add(mod.UserDefinedName);
            int index1 = comboBoxDevice.Items.IndexOf(text1);
            comboBoxDevice.SelectedIndex = (index1 == -1) ? 0 : index1;
        }

        private void comboBoxDevice_SelectedIndexChanged(object sender, EventArgs e)
        {
            device = Solution.Instance.AllDevices.Find(dev => dev.UserDefinedName == comboBoxDevice.Text);
            deviceName = device != null ? device.DevName : null;
        }

        private void ListeningAddressTextbox_TextChanged(object sender, EventArgs e)
        {
            address = ListeningAddressTextbox.Text;
        }


        #region 运行核心逻辑 (High Performance Optimized)

        /// <summary>
        /// 模版匹配 (优化的主入口)
        /// </summary>
        public async Task<(Bitmap, List<DetectionResult>, bool)> MatchTemplateAsync(bool show = true)
        {
            try
            {
                // 1. 校验资源 (优先使用缓存的 srcMat)
                if (srcMat == null || srcMat.IsDisposed)
                {
                    if (src != null) srcMat = BitmapConverter.ToMat(src); // 补救措施
                    else throw new ArgumentException("源图像或模板图像不能为空");
                }

                if (colorParam.Profiles.Count == 0) throw new ArgumentException("请先添加色彩文件再次执行匹配!");

                bool allOk = true;
                List<DetectionResult> allMatchResults = null;

                // 2. 计算搜索区域 (Clip to image bounds)
                Rect searchRect = (colorParam.DetectedRoi.Size.Width > 0)
                   ? colorParam.DetectedRoi.BoundingRect().Intersect(new Rect(0, 0, srcMat.Width, srcMat.Height))
                   : new Rect(0, 0, srcMat.Width, srcMat.Height);

                // 3. 执行检测 (只读访问 srcMat，无需 Clone)
                if (isOpenGeiGround)
                {
                    ColorProfile currentColor = null;
                    if (device is IModbus mod)
                    {
                        // Modbus IO 是异步的，保持 await
                        ushort value = await GetModbusCode(mod, address);
                        string strVal = value.ToString();
                        currentColor = colorParam.Profiles.FirstOrDefault(m =>
                            !string.IsNullOrEmpty(m.TriggerVal) &&
                            m.TriggerVal.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                        .Select(val => val.Trim())
                                        .Contains(strVal)
                        );
                    }
                    allMatchResults = RunCurrentDetection(srcMat, currentColor, searchRect);
                }
                else
                {
                    allMatchResults = RunDetection(srcMat, searchRect);

                    // 排序逻辑
                    if (isOpenSort)
                    {
                        allOk = SortAndCheckByProfile(allMatchResults, colorParam.Profiles, sortModel);
                    }
                }

                if (allMatchResults.Count == 0) allOk = false;
                foreach (var r in allMatchResults)
                {
                    if (!r.IsOk) allOk = false;
                }

                if (!show)
                    return ((Bitmap)src.Clone(), allMatchResults, allOk);

                // 4. 结果绘制 (仅在这一步创建新的 Bitmap 用于显示)
                // 使用 srcMat.Clone() 避免修改原始缓存数据
                using (Mat displayMat = srcMat.Clone())
                {
                    foreach (var r in allMatchResults)
                    {
                        Scalar rectColor = r.IsOk ? Scalar.Lime : Scalar.Red;
                        Cv2.Rectangle(displayMat, r.Region, rectColor, 3);
                    }

                    // 转换为 Bitmap 以进行文字绘制 (GDI+ 效果更好)
                    Bitmap resultBitmap = BitmapConverter.ToBitmap(displayMat);

                    using (Graphics g = Graphics.FromImage(resultBitmap))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        Font font = new Font("Microsoft YaHei", 9, FontStyle.Bold);

                        // 绘制左侧半透明背景列表
                        int lineHeight = 20;
                        int boxWidth = 260;
                        int boxHeight = (allMatchResults.Count * lineHeight) + 30;

                        g.FillRectangle(new SolidBrush(Color.FromArgb(160, 0, 0, 0)), 0, 0, boxWidth, boxHeight);
                        g.DrawString("检测结果 (左侧列表):", font, Brushes.White, 5, 5);

                        for (int i = 0; i < allMatchResults.Count; i++)
                        {
                            var r = allMatchResults[i];
                            string status = r.IsOk ? "OK" : "NG";
                            Brush brush = r.IsOk ? Brushes.Lime : Brushes.Red;

                            string text = $"{i + 1}. {r.Name} [{status}]";
                            if (r.ShowArea) text += $" Area:{r.Area}";
                            if (r.ShowRatio) text += $" Ratio:{r.AreaB:F1}%";

                            g.DrawString(text, font, brush, 5, 25 + (i * lineHeight));
                        }
                    }

                    if (show)
                    {
                        showImageControl1.ClearAllRoi();
                        showImageControl1.ImageBitmap = resultBitmap;
                    }

                    return (resultBitmap, allMatchResults, allOk);
                }
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("模板匹配失败", ex);
            }
        }

        // 内部结构
        private struct BlobCandidate
        {
            public double Area;
            public Rect BoundingBox;
            public ColorProfile Profile;
        }

        /// <summary>
        /// 识别所有颜色 (优化：复用Lab通道，减少内存分配)
        /// </summary>
        public List<DetectionResult> RunDetection(Mat inputImage, Rect searchRect)
        {
            List<DetectionResult> results = new List<DetectionResult>();
            if (inputImage == null || inputImage.Empty()) return results;
            if (searchRect.Width <= 0 || searchRect.Height <= 0) return results;

            // 裁剪 ROI (使用 Header，不拷贝数据)
            searchRect = searchRect.Intersect(new Rect(0, 0, inputImage.Width, inputImage.Height));
            if (searchRect.Width <= 0 || searchRect.Height <= 0) return results;

            using (Mat roiSrc = new Mat(inputImage, searchRect))
            using (Mat baseProcessed = new Mat())
            {
                // 全局降噪 (只做一次)
                Cv2.GaussianBlur(roiSrc, baseProcessed, new OpenCvSharp.Size(3, 3), 0);

                // --- 优化：预计算标准 Lab 通道 ---
                // 如果存在多个 Profile 使用默认 Gamma=1.0 且无结构提取，可复用这些通道
                Mat[] sharedLabChannels = null;
                bool hasStandardProfile = colorParam.Profiles.Any(p => Math.Abs(p.Gamma - 1.0) < 0.05 && !p.UseStructure);

                if (hasStandardProfile)
                {
                    using (Mat sharedLab = new Mat())
                    {
                        Cv2.CvtColor(baseProcessed, sharedLab, ColorConversionCodes.BGR2Lab);
                        sharedLabChannels = Cv2.Split(sharedLab);
                    }
                }

                try
                {
                    List<BlobCandidate> candidates = new List<BlobCandidate>();
                    double totalDetectedArea = 0;

                    for (int i = 0; i < colorParam.Profiles.Count; i++)
                    {
                        var profile = colorParam.Profiles[i];
                        List<BlobCandidate> profileCandidates = new List<BlobCandidate>();

                        using (Mat mask = new Mat())
                        {
                            // 区分 快速通道 vs 独立通道
                            bool isStandard = (Math.Abs(profile.Gamma - 1.0) < 0.05 && !profile.UseStructure);

                            if (isStandard && sharedLabChannels != null)
                            {
                                // 快速：复用 Lab 通道
                                CalculateColorMask(sharedLabChannels, mask, profile);
                                ExtractContours(mask, sharedLabChannels, profile, searchRect, profileCandidates, isLabSplit: true);
                            }
                            else
                            {
                                // 慢速：独立处理 Gamma 或 Structure
                                using (Mat customProcessed = baseProcessed.Clone())
                                using (Mat customLab = new Mat())
                                {
                                    if (Math.Abs(profile.Gamma - 1.0) > 0.05)
                                    {
                                        byte[] lut = new byte[256];
                                        for (int j = 0; j < 256; j++) lut[j] = (byte)Math.Min(255, Math.Pow(j / 255.0, 1.0 / profile.Gamma) * 255.0);
                                        using (Mat lutMat = new Mat(1, 256, MatType.CV_8U))
                                        {
                                            for (int k = 0; k < 256; k++) lutMat.Set<byte>(0, k, lut[k]);
                                            Cv2.LUT(customProcessed, lutMat, customProcessed);
                                        }
                                    }
                                    ProcessStructureAndLab(customProcessed, customLab, mask, profile);
                                    ExtractContours(mask, customLab, profile, searchRect, profileCandidates, isLabSplit: false);
                                }
                            }
                        }

                        // 结果筛选 (MaxArea / MinArea)
                        if (profileCandidates.Count > 0)
                        {
                            if (profile.ScreeningModel == "MaxArea")
                            {
                                var best = profileCandidates.OrderByDescending(x => x.Area).First();
                                profileCandidates.Clear(); profileCandidates.Add(best);
                            }
                            else if (profile.ScreeningModel == "MinArea")
                            {
                                var best = profileCandidates.OrderBy(x => x.Area).First();
                                profileCandidates.Clear(); profileCandidates.Add(best);
                            }
                        }

                        foreach (var c in profileCandidates)
                        {
                            candidates.Add(c);
                            totalDetectedArea += c.Area;
                        }
                    }

                    // 最终判定 (Area & Ratio)
                    foreach (var blob in candidates)
                    {
                        double ratio = (totalDetectedArea > 0) ? Math.Round(blob.Area / totalDetectedArea * 100.0, 2) : 0;
                        bool isOk = true; // 默认合格

                        // 先判断单个条件是否满足
                        bool widthCheckPass = true; // 宽度检查是否通过
                        bool ratioCheckPass = true; // 百分比检查是否通过

                        // 1. 宽度检查（Area）逻辑
                        if (blob.Profile.IsOpenWidthCheck)
                        {
                            widthCheckPass = (blob.Area >= blob.Profile.MinWidth && blob.Area <= blob.Profile.MaxWidth);
                        }

                        // 2. 百分比检查（Ratio）逻辑
                        if (blob.Profile.IsOpenWidthBCheck)
                        {
                            ratioCheckPass = (ratio >= blob.Profile.MinWidthB && ratio <= blob.Profile.MaxWidthB);
                        }

                        // 3. 整合所有检查，确定最终isOk
                        if (blob.Profile.IsOpenWidthCheck && blob.Profile.IsOpenWidthBCheck)
                        {
                            // 两个检查都开启：只要一个满足就为true
                            isOk = widthCheckPass || ratioCheckPass;
                        }
                        else if (blob.Profile.IsOpenWidthCheck)
                        {
                            // 仅开启宽度检查：判断宽度是否通过
                            isOk = widthCheckPass;
                        }
                        else if (blob.Profile.IsOpenWidthBCheck)
                        {
                            // 仅开启百分比检查：判断百分比是否通过
                            isOk = ratioCheckPass;
                        }
                        // 都不开启的情况：isOk保持初始值true

                        results.Add(new DetectionResult
                        {
                            Name = blob.Profile.Name,
                            Region = blob.BoundingBox,
                            Area = blob.Area,
                            AreaB = ratio,
                            IsOk = isOk,
                            ShowArea = blob.Profile.IsOpenWidthCheck,
                            ShowRatio = blob.Profile.IsOpenWidthBCheck
                        });
                    }

                }
                finally
                {
                    if (sharedLabChannels != null)
                    {
                        foreach (var m in sharedLabChannels) m.Dispose();
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// 识别指定颜色配置 (单 Profile)
        /// </summary>
        public List<DetectionResult> RunCurrentDetection(Mat inputImage, ColorProfile profile, Rect searchRect)
        {
            List<DetectionResult> results = new List<DetectionResult>();
            if (inputImage == null || profile == null) return results;
            if (searchRect.Width <= 0 || searchRect.Height <= 0) return results;

            searchRect = searchRect.Intersect(new Rect(0, 0, inputImage.Width, inputImage.Height));
            if (searchRect.Width <= 0 || searchRect.Height <= 0) return results;

            using (Mat roiSrc = new Mat(inputImage, searchRect))
            using (Mat baseProcessed = new Mat())
            {
                Cv2.GaussianBlur(roiSrc, baseProcessed, new OpenCvSharp.Size(3, 3), 0);
                List<BlobCandidate> candidates = new List<BlobCandidate>();
                double totalDetectedArea = 0;

                using (Mat mask = new Mat())
                using (Mat lab = new Mat())
                {
                    if (Math.Abs(profile.Gamma - 1.0) < 0.05 && !profile.UseStructure)
                    {
                        Cv2.CvtColor(baseProcessed, lab, ColorConversionCodes.BGR2Lab);
                        Mat[] channels = Cv2.Split(lab);
                        CalculateColorMask(channels, mask, profile);
                        ExtractContours(mask, lab, profile, searchRect, candidates, isLabSplit: false);
                        foreach (var c in channels) c.Dispose();
                    }
                    else
                    {
                        using (Mat customProcessed = baseProcessed.Clone())
                        {
                            if (Math.Abs(profile.Gamma - 1.0) > 0.05)
                            {
                                byte[] lut = new byte[256];
                                for (int j = 0; j < 256; j++) lut[j] = (byte)Math.Min(255, Math.Pow(j / 255.0, 1.0 / profile.Gamma) * 255.0);
                                using (Mat lutMat = new Mat(1, 256, MatType.CV_8U))
                                {
                                    for (int k = 0; k < 256; k++) lutMat.Set<byte>(0, k, lut[k]);
                                    Cv2.LUT(customProcessed, lutMat, customProcessed);
                                }
                            }
                            ProcessStructureAndLab(customProcessed, lab, mask, profile);
                            ExtractContours(mask, lab, profile, searchRect, candidates, isLabSplit: false);
                        }
                    }
                }

                if (candidates.Count > 0)
                {
                    if (profile.ScreeningModel == "MaxArea")
                    {
                        var best = candidates.OrderByDescending(x => x.Area).First(); candidates.Clear(); candidates.Add(best);
                    }
                    else if (profile.ScreeningModel == "MinArea")
                    {
                        var best = candidates.OrderBy(x => x.Area).First(); candidates.Clear(); candidates.Add(best);
                    }
                }

                foreach (var c in candidates) totalDetectedArea += c.Area;
                foreach (var blob in candidates)
                {
                    double ratio = (totalDetectedArea > 0) ? (blob.Area / totalDetectedArea * 100.0) : 0;
                    bool isOk = true;
                    if (blob.Profile.IsOpenWidthCheck && (blob.Area < blob.Profile.MinWidth || blob.Area > blob.Profile.MaxWidth)) isOk = false;
                    if (blob.Profile.IsOpenWidthBCheck && (ratio < blob.Profile.MinWidthB || ratio > blob.Profile.MaxWidthB)) isOk = false;
                    if (!blob.Profile.IsOpenWidthCheck && !blob.Profile.IsOpenWidthBCheck) isOk = true;

                    results.Add(new DetectionResult { Name = blob.Profile.Name, Region = blob.BoundingBox, Area = blob.Area, AreaB = ratio, IsOk = isOk, ShowArea = blob.Profile.IsOpenWidthCheck, ShowRatio = blob.Profile.IsOpenWidthBCheck });
                }
            }
            return results;
        }

        // --- 核心辅助算法 (复用) ---

        private void CalculateColorMask(Mat[] labChannels, Mat dstMask, ColorProfile profile)
        {
            using (Mat dL = new Mat()) using (Mat maskL = new Mat()) using (Mat maskC = new Mat())
            {
                Cv2.Absdiff(labChannels[0], new Scalar(profile.TargetLab.Val0), dL);
                Cv2.Threshold(dL, maskL, profile.ToleranceL, 255, ThresholdTypes.BinaryInv);
                using (Mat dA = new Mat()) using (Mat dB = new Mat())
                using (Mat dA2 = new Mat()) using (Mat dB2 = new Mat()) using (Mat dist = new Mat())
                {
                    Cv2.Absdiff(labChannels[1], new Scalar(profile.TargetLab.Val1), dA);
                    Cv2.Absdiff(labChannels[2], new Scalar(profile.TargetLab.Val2), dB);
                    dA.ConvertTo(dA2, MatType.CV_32F); dB.ConvertTo(dB2, MatType.CV_32F);
                    Cv2.Magnitude(dA2, dB2, dist);
                    Cv2.Threshold(dist, maskC, profile.ToleranceC, 255, ThresholdTypes.BinaryInv);
                    maskC.ConvertTo(maskC, MatType.CV_8U);
                }
                Cv2.BitwiseAnd(maskL, maskC, dstMask);
            }
            if (profile.UseMorphology)
            {
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(profile.MorphKernelSize, profile.MorphKernelSize)))
                    Cv2.MorphologyEx(dstMask, dstMask, profile.MorphOp, kernel, iterations: profile.MorphIterations);
            }
        }

        private void ProcessStructureAndLab(Mat processed, Mat lab, Mat dstMask, ColorProfile profile)
        {
            using (Mat structureMask = new Mat())
            {
                if (profile.UseStructure)
                {
                    using (Mat gray = new Mat())
                    using (Mat kernelStr = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(profile.StructureKernelSize, profile.StructureKernelSize)))
                    {
                        Cv2.CvtColor(processed, gray, ColorConversionCodes.BGR2GRAY);
                        Cv2.MorphologyEx(gray, structureMask, profile.StructureOp, kernelStr);
                        Cv2.Threshold(structureMask, structureMask, profile.StructureThreshold, 255, ThresholdTypes.Binary);
                    }
                }
                Cv2.CvtColor(processed, lab, ColorConversionCodes.BGR2Lab);
                Mat[] channels = Cv2.Split(lab);
                CalculateColorMask(channels, dstMask, profile);
                foreach (var c in channels) c.Dispose();
                if (profile.UseStructure && !structureMask.Empty()) Cv2.BitwiseAnd(dstMask, structureMask, dstMask);
            }
        }

        private void ExtractContours(Mat mask, object labSource, ColorProfile profile, Rect searchOffset, List<BlobCandidate> candidates, bool isLabSplit)
        {
            Point[][] contours; HierarchyIndex[] hierarchy;
            Cv2.FindContours(mask, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);
            using (Mat singleBlobMask = profile.EnableMeanCheck ? new Mat(mask.Size(), MatType.CV_8U) : null)
            {
                foreach (var contour in contours)
                {
                    double area = Cv2.ContourArea(contour);
                    if (area < profile.MinArea) continue;
                    bool isColorMatch = true;
                    if (profile.EnableMeanCheck)
                    {
                        singleBlobMask.SetTo(Scalar.All(0));
                        Cv2.DrawContours(singleBlobMask, new[] { contour }, -1, Scalar.All(255), -1);
                        Scalar meanVal;
                        if (isLabSplit)
                        {
                            Mat[] channels = (Mat[])labSource;
                            double mL = Cv2.Mean(channels[0], singleBlobMask).Val0;
                            double mA = Cv2.Mean(channels[1], singleBlobMask).Val0;
                            double mB = Cv2.Mean(channels[2], singleBlobMask).Val0;
                            meanVal = new Scalar(mL, mA, mB);
                        }
                        else
                        {
                            meanVal = Cv2.Mean((Mat)labSource, singleBlobMask);
                        }
                        double diffL = Math.Abs(meanVal.Val0 - profile.TargetLab.Val0);
                        double distC = Math.Sqrt(Math.Pow(meanVal.Val1 - profile.TargetLab.Val1, 2) + Math.Pow(meanVal.Val2 - profile.TargetLab.Val2, 2));
                        if (diffL > profile.ToleranceL || distC > profile.ToleranceC) isColorMatch = false;
                    }
                    if (isColorMatch)
                    {
                        Rect r = Cv2.BoundingRect(contour);
                        r.X += searchOffset.X; r.Y += searchOffset.Y;
                        candidates.Add(new BlobCandidate { Area = area, BoundingBox = r, Profile = profile });
                    }
                }
            }
        }

        private async Task<ushort> GetModbusCode(IModbus modbus, string address)
        {
            ushort[] value = modbus.ReadUInt16(address, 1);
            return value[0];
        }

        public bool SortAndCheckByProfile(List<DetectionResult> results, List<ColorProfile> profiles, int sortModel)
        {
            if (results == null || profiles == null || results.Count != profiles.Count) return false;
            if (sortModel == 0) results.Sort((a, b) => a.Region.X.CompareTo(b.Region.X));
            else results.Sort((a, b) => a.Region.Y.CompareTo(b.Region.Y));
            for (int i = 0; i < profiles.Count; i++)
            {
                if (!string.Equals(results[i].Name, profiles[i].Name, StringComparison.OrdinalIgnoreCase)) return false;
            }
            return true;
        }

        #endregion

        private void checkBox1_CheckedChanged(object sender, EventArgs e) { isOpenSort = checkBox1.Checked; }
        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e) { sortModel = comboBox1.SelectedIndex; }
    }
}
