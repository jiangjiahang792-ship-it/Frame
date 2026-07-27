using Logger;
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
using TDJS_Vision.Device.PLC;
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
        private Mat srcMat = null;  // 原图

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
            //imageROIEditControl1.SetROIType2Draw(Forms.ShapeDraw.ROIType.Rectangle);
            comboBox1.SelectedIndex = 0;
            Shown += NodeParamFormQRCodeIdentification_Shown;
            Shown += NodeParamFormColorDiscern_Shown;
        }

        private void NodeParamFormColorDiscern_Shown(object sender, EventArgs e)
        {
            InitDevice();
        }

        /// <summary>
        /// 窗口显示时更新订阅的图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void NodeParamFormQRCodeIdentification_Shown(object sender, EventArgs e)
        {
            UpdataImage();
        }

        public INodeParam Params { get; set; }

        /// <summary>
        /// 反序列化还原界面参数设置
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamColorDiscern param)
            {
                try
                {
                    textBoxModelPath.Text = param.TemplateFileName;
                    nodeSubscription1.SetText(param.Text1, param.Text2);

                    radioButton2.Checked = param.IsOpenGeiGround ? true : false;
                    radioButton1.Checked = param.IsOpenGeiGround ? false : true;
                    param.Device = Solution.Instance.AllDevices.Find(dev => dev.UserDefinedName == param.DeviceName);
                    comboBoxDevice.Text = param.DeviceName;
                    device = param.Device;
                    ListeningAddressTextbox.Text = param.Address;
                    address = param.Address;

                    isOpenSort = param.IsOpenSort;

                    checkBox1.Checked = param.IsOpenSort;
                    comboBox1.SelectedIndex = param.SrotModel;

                    if (File.Exists(param.TemplateFileName))
                    {
                        colorParam = JsonProjectSerializer.LoadProject<ColorParam>(param.TemplateFileName);
                    }
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        /// <summary>
        /// 初始化订阅节点
        /// </summary>
        /// <param name="node"></param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 刷新图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button3_Click(object sender, EventArgs e)
        {
            await process.RunForUpdateImages(node);
            UpdataImage();
        }

        /// <summary>
        /// 更新函数
        /// </summary>
        public void UpdataImage(bool isShow = true)
        {
            try
            {
                OutputImage outputImage = nodeSubscription1.GetValue<OutputImage>();
                srcMat?.Dispose();
                srcMat = outputImage.Bitmaps[0]?.Clone();
            }
            catch (Exception)
            {
                srcMat = null;
            }
            //imageROIEditControl1.SetImage(src);
            if (isShow) showImageControl1.ImageBitmap = (srcMat != null && !srcMat.Empty()) ? BitmapConverter.ToBitmap(srcMat) : null;
        }

        /// <summary>
        /// 获取订阅的输入图像对象，运行节点使用原图和叠加结果分离的输出模式。
        /// </summary>
        /// <returns>上游输出图像。</returns>
        public OutputImage GetInputOutputImage()
        {
            return nodeSubscription1.GetValue<OutputImage>();
        }


        /// <summary>
        /// 点击执行模板匹配
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                await MatchTemplateAsync();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"模版匹配异常：原因：{ex.Message}");
            }
        }


        /// <summary>
        /// Bitmap转灰度Mat
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private static Mat BitmapToGrayMat(Bitmap bitmap)
        {
            using (Mat mat = BitmapConverter.ToMat(bitmap))
            {
                if (mat.Channels() == 1)
                    return mat.Clone();
                Mat grayMat = new Mat();
                Cv2.CvtColor(mat, grayMat, ColorConversionCodes.BGR2GRAY);
                return grayMat;
            }
        }

        /// <summary>
        /// Bitmap转彩色Mat
        /// </summary>
        /// <param name="bitmap"></param>
        /// <returns></returns>
        private static Mat BitmapToColorMat(Bitmap bitmap)
        {
            return BitmapConverter.ToMat(bitmap);
        }

        /// <summary>
        /// 点击保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        /// <returns></returns>
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

        /// <summary>
        /// 点击选择模版图像文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                textBoxModelPath.Text = openFileDialog1.FileName;
                colorParam = JsonProjectSerializer.LoadProject<ColorParam>(openFileDialog1.FileName);
            }
        }

        /// <summary>
        /// 点击创建模版
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void label1_Click(object sender, EventArgs e)
        {
            colorCreate = new ColorCreate();
            colorCreate.SetNodeBelong(node);
            colorCreate.ShowDialog();
        }

        private void radioButton2_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButton1.Checked)
            {
                comboBoxDevice.Enabled = false;
                ListeningAddressTextbox.Enabled = false;
                checkBox1.Enabled = true;
                comboBox1.Enabled = true;
            }
            else
            {
                comboBoxDevice.Enabled = true;
                ListeningAddressTextbox.Enabled = true;
                checkBox1.Enabled = false;
                comboBox1.Enabled = false;
            }
            isOpenGeiGround = radioButton2.Checked;
        }


        /// <summary>
        /// 初始化设备列表
        /// </summary>
        private void InitDevice()
        {
            string text1 = comboBoxDevice.Text;

            comboBoxDevice.Items.Clear();
            comboBoxDevice.Items.Add("[未设置]");
            foreach (var plc in Solution.Instance.PlcDevices)
            {
                comboBoxDevice.Items.Add(plc.UserDefinedName);
            }
            foreach (var mod in Solution.Instance.ModbusDevices)
            {
                comboBoxDevice.Items.Add(mod.UserDefinedName);
            }
            int index1 = comboBoxDevice.Items.IndexOf(text1);
            if (index1 == -1)
                comboBoxDevice.SelectedIndex = 0;
            else
                comboBoxDevice.SelectedIndex = index1;
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


        #region  运行核心逻辑

        /// <summary>
        /// 模版匹配
        /// </summary>
        /// <param name="sourceImage"></param>
        /// <param name="templateImage"></param>
        /// <param name="scale"></param>
        /// <param name="show">是否更新显示，节点运行时设为false降低耗时</param>
        /// <returns></returns>
        public async Task<(Bitmap, List<DetectionResult>, bool)> MatchTemplateAsync(bool show = true)
        {
            try
            {
                Mat sourceImage = srcMat;

                if (sourceImage == null || sourceImage.Empty())
                    throw new ArgumentException("源图像或模板图像不能为空");

                var (allMatchResults, allOk) = await MatchColorResultsAsync(sourceImage);
                if (!show)
                    return (null, allMatchResults, allOk);

                using (Mat sourceMat = sourceImage.Clone())
                {
                    // 显示识别结果
                    foreach (var r in allMatchResults)
                    {
                        Scalar rectColor = r.IsOk ? Scalar.Lime : Scalar.Red;
                        // 参数界面预览保留烧图显示，运行节点不走此分支。
                        Cv2.Rectangle(sourceMat, r.Region, rectColor, 3);
                    }

                    // 转换为 Bitmap 进行文字绘制 (解决中文乱码和堆叠问题)
                    Bitmap resultBitmap = BitmapConverter.ToBitmap(sourceMat);

                    using (Graphics g = Graphics.FromImage(resultBitmap))
                    using (Font font = new Font("Microsoft YaHei", 9, FontStyle.Bold))
                    using (Brush backgroundBrush = new SolidBrush(Color.FromArgb(160, 0, 0, 0)))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                        // 在左侧绘制半透明背景列
                        int lineHeight = 20;
                        int boxWidth = 260;
                        int boxHeight = (allMatchResults.Count * lineHeight) + 30;

                        // 绘制半透明背景
                        g.FillRectangle(backgroundBrush, 0, 0, boxWidth, boxHeight);
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

        /// <summary>
        /// 只执行颜色识别并返回结构化结果，不生成预览图像。
        /// </summary>
        /// <param name="sourceImage">待识别的图像。</param>
        /// <returns>识别结果列表和整体 OK/NG。</returns>
        public async Task<(List<DetectionResult> Results, bool IsOk)> MatchColorResultsAsync(Mat sourceImage)
        {
            try
            {
                LogRuntimeStep("颜色匹配入口");
                if (sourceImage == null || sourceImage.Empty())
                    throw new ArgumentException("源图像或模板图像不能为空");

                if (colorParam.Profiles.Count == 0)
                    throw new ArgumentException("请先添加色彩文件再次执行匹配!");

                bool allOk = true;
                Rect searchRect = (colorParam.DetectedRoi.Size.Width > 0)
                    ? colorParam.DetectedRoi.BoundingRect().Intersect(new Rect(0, 0, sourceImage.Width, sourceImage.Height))
                    : new Rect(0, 0, sourceImage.Width, sourceImage.Height);

                List<DetectionResult> allMatchResults = null;
                if (isOpenGeiGround)
                {
                    ColorProfile currentColor = null;
                    if (device is IPlc)
                    {
                        throw new Exception("未实现该通信方式<IPlc>");
                    }
                    else if (device is IModbus mod)
                    {
                        LogRuntimeStep($"开始读取Modbus颜色地址:{address} 触发值");
                        ushort value = await GetModbusCode(mod, address);
                        LogRuntimeStep($"读取Modbus颜色触发值完毕，值:{value}");
                        string strVal = value.ToString();
                        currentColor = colorParam.Profiles.FirstOrDefault(m =>
                            !string.IsNullOrEmpty(m.TriggerVal) &&
                            m.TriggerVal.Split(new char[] { ',' }, StringSplitOptions.RemoveEmptyEntries)
                                .Select(val => val.Trim())
                                .Contains(strVal));
                        if (currentColor == null) currentColor = colorParam.Profiles[0];
                    }

                    LogRuntimeStep("开始执行当前颜色检测");
                    allMatchResults = RunCurrentDetection(sourceImage, currentColor, searchRect);
                    LogRuntimeStep($"当前颜色检测完毕，结果数量:{(allMatchResults == null ? 0 : allMatchResults.Count)}");
                }
                else
                {
                    LogRuntimeStep($"开始执行全部颜色检测，模板数量:{colorParam.Profiles.Count}");
                    allMatchResults = RunDetection(sourceImage, searchRect);
                    LogRuntimeStep($"全部颜色检测完毕，结果数量:{(allMatchResults == null ? 0 : allMatchResults.Count)}");

                    if (isOpenSort)
                    {
                        LogRuntimeStep("开始执行颜色排序校验");
                        allOk = SortAndCheckByProfile(allMatchResults, colorParam.Profiles, sortModel);
                        LogRuntimeStep($"颜色排序校验完毕，结果:{allOk}");
                    }
                }

                allMatchResults = allMatchResults ?? new List<DetectionResult>();
                if (allMatchResults.Count == 0)
                    allOk = false;

                foreach (DetectionResult result in allMatchResults)
                {
                    if (!result.IsOk)
                    {
                        allOk = false;
                        break;
                    }
                }

                return (allMatchResults, allOk);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("颜色识别失败", ex);
            }
        }

        /// <summary>
        /// 输出颜色识别参数窗体运行阶段诊断日志，辅助定位通信读取或OpenCV检测耗时。
        /// </summary>
        /// <param name="step">诊断步骤名称。</param>
        private void LogRuntimeStep(string step)
        {
            if (process != null && !process.ShowLog)
                return;

            string nodeName = node == null ? "颜色识别" : $"{node.ID}.{node.NodeName}";
            LogHelper.AddLog(MsgLevel.Debug, $"节点({nodeName}){step}，{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff")}", true);
        }

        // 内部结构，用于暂存扫描的结果
        private struct BlobCandidate
        {
            public double Area;
            public Rect BoundingBox;
            public ColorProfile Profile;
        }

        /// <summary>
        /// 识别所有颜色
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="searchRect">搜索区域 (相对图像坐标)</param>
        /// <returns>检测结果列表</returns>
        public List<DetectionResult> RunDetection(Mat inputImage, Rect searchRect)
        {
            List<DetectionResult> results = new List<DetectionResult>();
            if (inputImage == null || inputImage.Empty()) return results;

            // 裁剪搜索区域 ROI
            searchRect = searchRect.Intersect(new Rect(0, 0, inputImage.Width, inputImage.Height));
            if (searchRect.Width <= 0 || searchRect.Height <= 0) return results;

            using (Mat roiSrc = new Mat(inputImage, searchRect))
            using (Mat baseProcessed = new Mat())
            {
                // 全局降噪
                Cv2.GaussianBlur(roiSrc, baseProcessed, new OpenCvSharp.Size(3, 3), 0);

                double totalDetectedArea = 0;
                List<BlobCandidate> allCandidates = new List<BlobCandidate>();

                for (int i = 0; i < colorParam.Profiles.Count; i++)
                {
                    var profile = colorParam.Profiles[i];
                    List<BlobCandidate> profileCandidates = new List<BlobCandidate>();

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
                                        // 独立通道二次校验
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
                                    profileCandidates.Add(new BlobCandidate { Area = area, BoundingBox = r, Profile = profile });
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
                    }

                    // 将筛选后的结果加入总列表并累计面积
                    foreach (var c in profileCandidates)
                    {
                        allCandidates.Add(c);
                        totalDetectedArea += c.Area;
                    }
                }

                // 2. 第二遍：根据总面积计算占比，并进行最终判定
                foreach (var blob in allCandidates)
                {
                    double ratio = (totalDetectedArea > 0) ? (blob.Area / totalDetectedArea * 100.0) : 0;
                    bool isOk = true;

                    if (blob.Profile.IsOpenWidthCheck)
                    {
                        if (blob.Area < blob.Profile.MinWidth || blob.Area > blob.Profile.MaxWidth)
                            isOk = false;
                    }
                    if (blob.Profile.IsOpenWidthBCheck)
                    {
                        if (isOk == false) isOk = true;
                        if (ratio < blob.Profile.MinWidthB || ratio > blob.Profile.MaxWidthB) isOk = false;
                    }

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
            return results;
        }

        /// <summary>
        /// 识别指定颜色配置 (逻辑同步更新)
        /// </summary>
        /// <param name="inputImage">输入图像</param>
        /// <param name="profile">颜色参数</param>
        /// <param name="searchRect">搜索区域</param>
        /// <returns>检测结果列表</returns>
        public List<DetectionResult> RunCurrentDetection(Mat inputImage, ColorProfile profile, Rect searchRect)
        {
            List<DetectionResult> results = new List<DetectionResult>();
            if (inputImage == null || profile == null) return results;

            // 裁剪搜索区域 ROI
            searchRect = searchRect.Intersect(new Rect(0, 0, inputImage.Width, inputImage.Height));
            if (searchRect.Width <= 0 || searchRect.Height <= 0) return results;

            using (Mat roiSrc = new Mat(inputImage, searchRect))
            using (Mat baseProcessed = new Mat())
            {
                Cv2.GaussianBlur(roiSrc, baseProcessed, new OpenCvSharp.Size(3, 3), 0);

                List<BlobCandidate> candidates = new List<BlobCandidate>();
                double totalDetectedArea = 0;

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
                            // 计算轮廓的最小外接矩形
                            RotatedRect minRect = Cv2.MinAreaRect(contour);
                            // 计算最小外接矩形的面积（宽 × 高）
                            double area = minRect.Size.Width * minRect.Size.Height;
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
                                    double diffL = Math.Abs(meanVal.Val0 - profile.TargetLab.Val0);
                                    double diffA = Math.Abs(meanVal.Val1 - profile.TargetLab.Val1);
                                    double diffB = Math.Abs(meanVal.Val2 - profile.TargetLab.Val2);
                                    if (diffL > profile.ToleranceL || diffA > profile.ToleranceA || diffB > profile.ToleranceB) isColorMatch = false;
                                }
                            }

                            if (isColorMatch)
                            {
                                Rect r = Cv2.BoundingRect(contour);
                                r.X += searchRect.X;
                                r.Y += searchRect.Y;
                                candidates.Add(new BlobCandidate { Area = area, BoundingBox = r, Profile = profile });
                            }
                        }
                    }
                }

                // --- Screening Logic ---
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
                }

                foreach (var c in candidates) totalDetectedArea += c.Area;

                // 判定
                foreach (var blob in candidates)
                {
                    double ratio = (totalDetectedArea > 0) ? (blob.Area / totalDetectedArea * 100.0) : 0;
                    bool isOk = true;

                    if (blob.Profile.IsOpenWidthCheck)
                    {
                        if (blob.Area < blob.Profile.MinWidth || blob.Area > blob.Profile.MaxWidth) isOk = false;
                    }
                    if (blob.Profile.IsOpenWidthBCheck)
                    {
                        if (ratio < blob.Profile.MinWidthB || ratio > blob.Profile.MaxWidthB) isOk = false;
                    }

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
            return results;
        }

        // 提取的公共图像处理管线 (快速路径辅助)
        // 专门处理快速通道 (无 Gamma/Structure) 的场景，直接基于 Lab 通道计算
        private void CalculateColorMask(Mat[] labChannels, Mat dstMask, ColorProfile profile)
        {
            using (Mat dL = new Mat()) using (Mat maskL = new Mat())
            {
                Cv2.Absdiff(labChannels[0], new Scalar(profile.TargetLab.Val0), dL);
                Cv2.Threshold(dL, maskL, profile.ToleranceL, 255, ThresholdTypes.BinaryInv);

                using (Mat dA = new Mat()) using (Mat dB = new Mat())
                using (Mat maskA = new Mat()) using (Mat maskB = new Mat())
                {
                    Cv2.Absdiff(labChannels[1], new Scalar(profile.TargetLab.Val1), dA);
                    Cv2.Threshold(dA, maskA, profile.ToleranceA, 255, ThresholdTypes.BinaryInv);

                    Cv2.Absdiff(labChannels[2], new Scalar(profile.TargetLab.Val2), dB);
                    Cv2.Threshold(dB, maskB, profile.ToleranceB, 255, ThresholdTypes.BinaryInv);

                    Cv2.BitwiseAnd(maskL, maskA, dstMask);
                    Cv2.BitwiseAnd(dstMask, maskB, dstMask);
                }
            }
            if (profile.UseMorphology)
            {
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(profile.MorphKernelSize, profile.MorphKernelSize)))
                    Cv2.MorphologyEx(dstMask, dstMask, profile.MorphOp, kernel, iterations: profile.MorphIterations);
            }
        }

        // 提取的公共图像处理管线

        private void ProcessImagePipeline(Mat processed, Mat lab, Mat mask, ColorProfile profile, out Mat rawMask)
        {
            rawMask = null;
            // 2. Gamma
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

            // 3. Structure
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

                // 4. Lab
                Cv2.CvtColor(processed, lab, ColorConversionCodes.BGR2Lab);
                using (Mat diff = new Mat())
                using (Mat targetMat = new Mat(lab.Size(), lab.Type()))
                {
                    targetMat.SetTo(profile.TargetLab);
                    using (Mat d1 = new Mat())
                    using (Mat d2 = new Mat())
                    {
                        Cv2.Subtract(lab, targetMat, d1);
                        Cv2.Subtract(targetMat, lab, d2);
                        Cv2.Add(d1, d2, diff);
                    }

                    Mat[] channels = Cv2.Split(diff);
                    using (Mat dL = channels[0])
                    using (Mat dA = channels[1])
                    using (Mat dB = channels[2])
                    using (Mat maskL = new Mat())
                    using (Mat maskA = new Mat())
                    using (Mat maskB = new Mat())
                    {
                        // 独立通道阈值判定 (Box Model)
                        Cv2.Threshold(dL, maskL, profile.ToleranceL, 255, ThresholdTypes.BinaryInv);
                        Cv2.Threshold(dA, maskA, profile.ToleranceA, 255, ThresholdTypes.BinaryInv);
                        Cv2.Threshold(dB, maskB, profile.ToleranceB, 255, ThresholdTypes.BinaryInv);

                        Cv2.BitwiseAnd(maskL, maskA, mask);
                        Cv2.BitwiseAnd(mask, maskB, mask);
                    }
                    foreach (var c in channels) c.Dispose();
                }

                if (profile.UseStructure && !structureMask.Empty())
                    Cv2.BitwiseAnd(mask, structureMask, mask);
            }

            // 5. Morph
            if (profile.UseMorphology)
            {
                rawMask = mask.Clone();
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(profile.MorphKernelSize, profile.MorphKernelSize)))
                    Cv2.MorphologyEx(mask, mask, profile.MorphOp, kernel, iterations: profile.MorphIterations);
            }
        }

        private async Task<ushort> GetModbusCode(IModbus modbus, string address)
        {
            ushort[] value = modbus.ReadUInt16(address, 1);
            return value[0];
        }

        /// <summary>
        /// 根据 ColorProfile 集合中 Name 的顺序，
        /// 校验并重排检测结果顺序
        /// </summary>
        /// <param name="results">算法识别结果</param>
        /// <param name="profiles">配置的颜色模板集合</param>
        /// <param name="sortModel">0=X方向排序，1=Y方向排序</param>
        /// <returns>是否与模板 Name 顺序一致</returns>
        public bool SortAndCheckByProfile(
            List<DetectionResult> results,
            List<ColorProfile> profiles,
            int sortModel)
        {
            if (results == null || profiles == null)
                return false;

            if (results.Count != profiles.Count)
                return false;

            // ① 先按空间位置排序（X 或 Y）
            if (sortModel == 0)
            {
                // X 方向排序（从左到右）
                results.Sort((a, b) => a.Region.X.CompareTo(b.Region.X));
            }
            else
            {
                // Y 方向排序（从上到下）
                results.Sort((a, b) => a.Region.Y.CompareTo(b.Region.Y));
            }

            // ② 按模板 Name 顺序进行一致性校验
            for (int i = 0; i < profiles.Count; i++)
            {
                if (!string.Equals(
                    results[i].Name,
                    profiles[i].Name,
                    StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }


        #endregion

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            isOpenSort = checkBox1.Checked;
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            sortModel = comboBox1.SelectedIndex;
        }
    }
}
