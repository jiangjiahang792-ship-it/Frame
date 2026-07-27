using OpenCvSharp;
using OpenCvSharp.Extensions;
using Sunny.UI;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Device;
using TDJS_Vision.Forms.ShapeDraw;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.ColorDiscern
{

    public partial class NodeParamFormBinaryAnalysis : FormBase, INodeParamForm
    {
        private Process process; // 所属流程
        private NodeBase node;   // 所属节点
        private Bitmap src = null; // 原图

        private bool _eventsEnabled = true;

        /// <summary>
        /// 运行参数列表
        /// </summary>
        public List<BinaryAnlysisParams> BinaryAnlysisParams = new List<BinaryAnlysisParams>();

        // 用于临时存储要绘制的文字信息
        private class TextOverlay
        {
            public string Text { get; set; }
            public System.Drawing.Point Location { get; set; }
            public Color Color { get; set; }
            public bool IsHeader { get; set; } = false; // 是否是左上角标题
        }

        public NodeParamFormBinaryAnalysis(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            this.node = nodeBase;
            Shown += NodeParamForm_Shown;
            flowDirectionPanel1.SelectedControlChanged += FlowDirectionPanel1_SelectedControlChanged;
        }

        private void FlowDirectionPanel1_SelectedControlChanged(Control arg1, int arg2)
        {
            _eventsEnabled = false;

            InitDate();

            // 识别当前选中的区域
            RunCureentDetection();

            _eventsEnabled = true;
        }

        private void NodeParamForm_Shown(object sender, EventArgs e)
        {
            UpdateImage();
        }

        public INodeParam Params { get; set; }

        /// <summary>
        /// 反序列化还原界面参数设置
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamBinaryAnalysis param)
            {
                try
                {
                    nodeSubscription1.SetText(param.Text1, param.Text2);
                    // 还原参数列表
                    if (param.BinaryAnlysisParams != null)
                    {
                        this.BinaryAnlysisParams = param.BinaryAnlysisParams.ToList();
                        // 还原UI列表显示
                        RefreshListUI();
                    }
                    Params = param;
                }
                catch (Exception)
                {
                    throw;
                }
            }
        }

        private void RefreshListUI()
        {
            flowDirectionPanel1.Controls.Clear();
            foreach (var p in BinaryAnlysisParams)
            {
                AddButtonToPanel(p);
            }
        }

        /// <summary>
        /// 初始化订阅节点
        /// </summary>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 刷新图像
        /// </summary>
        private async void button3_Click(object sender, EventArgs e)
        {
            await process.RunForUpdateImages(node);
            UpdateImage();
        }

        /// <summary>
        /// 更新函数
        /// </summary>
        public void UpdateImage(bool isShow = true)
        {
            try
            {
                OutputImage outputImage = nodeSubscription1.GetValue<OutputImage>();
                if (outputImage != null && outputImage.Bitmaps.Count > 0)
                    src = outputImage.Bitmaps[0].ToBitmap();
            }
            catch (Exception)
            {
                src = null;
            }
            if (isShow && src != null) { showImageControl1.ImageBitmap = src; showImageControl1.ResetView(); }
        }

        /// <summary>
        /// 点击执行测试 (运行分析)
        /// </summary>
        private async void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                await RunAnalysisAsync();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"分析异常：{ex.Message}");
            }
        }

        /// <summary>
        /// 核心分析逻辑
        /// </summary>
        public async Task<(Bitmap, List<RegionResult>, bool)> RunAnalysisAsync(bool showImage = true)
        {
            if (src == null)
            {
                MessageBoxTD.Show("原图为空，请先获取图像");
                return (null, new List<RegionResult>(), false);
            }

            // 如果列表为空，则临时创建一个基于当前UI参数的配置进行测试
            List<BinaryAnlysisParams> runList = new List<BinaryAnlysisParams>();

            // 兼容逻辑
            if (BinaryAnlysisParams.Count == 0)
            {
                var tempParam = GetParamsFromUI();
                if (tempParam.Region.Length1 == 0)
                {
                    tempParam.Region = new BinaryRegion
                    {
                        Column = src.Width / 2.0,
                        Row = src.Height / 2.0,
                        Length1 = src.Width / 2.0,
                        Length2 = src.Height / 2.0,
                        Phi = 0
                    };
                }
                runList.Add(tempParam);
            }
            else
            {
                if (showImage)
                {
                    runList = this.BinaryAnlysisParams;
                }
                else runList = ((NodeParamBinaryAnalysis)Params).BinaryAnlysisParams;
            }

            if (showImage)
            {
                showImageControl1.ClearAllRoi();
            }

            Bitmap retBitmap = null;
            List<RegionResult> retResults = new List<RegionResult>();
            bool retPass = false;

            await Task.Run(() =>
            {
                try
                {
                    List<TextOverlay> textOverlays = new List<TextOverlay>();

                    using (Mat sourceMat = BitmapConverter.ToMat(src))
                    using (Mat displayMat = sourceMat.Clone()) // 用于绘制图形（框、轮廓）
                    {
                        int okCount = 0;
                        int ngCount = 0;

                        foreach (var param in runList)
                        {
                            RegionResult result = AnalyzeSingleRegion(sourceMat, displayMat, param, textOverlays);
                            if (result.IsPass) okCount++; else ngCount++;
                            retResults.Add(result);
                        }

                        // 整体结果判定：有结果且全部通过
                        retPass = (retResults.Count > 0 && ngCount == 0);

                        // 节点运行时输出原图，结果框和文字交给 ShowImageControl 叠加层绘制。
                        retBitmap = showImage ? BitmapConverter.ToBitmap(displayMat) : (Bitmap)src.Clone();

                        // 2. 如果需要显示，生成带文字的图片并更新 UI
                        if (showImage)
                        {
                            // 克隆一份用于绘制文字，保证 retBitmap 是“纯净”的
                            Bitmap uiBitmap = (Bitmap)retBitmap.Clone();

                            using (Graphics g = Graphics.FromImage(uiBitmap))
                            {
                                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                                g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

                                // 计算自适应字体大小 (图像高度的 1/60，最小 12px)
                                float baseFontSize = Math.Max(12f, src.Height / 60f);

                                // 字体
                                using (Font font = new Font("Microsoft YaHei UI", baseFontSize, FontStyle.Bold))
                                using (Font headerFont = new Font("Microsoft YaHei UI", baseFontSize * 1.5f, FontStyle.Bold))
                                using (Brush bgBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0))) // 半透明背景
                                {
                                    // A. 绘制左上角汇总信息
                                    string summary = $"检测总数: {runList.Count}  OK: {okCount}  NG: {ngCount}";
                                    float pad = baseFontSize / 2;
                                    SizeF sumSize = g.MeasureString(summary, headerFont);
                                    // 绘制背景框
                                    g.FillRectangle(bgBrush, 10, 10, sumSize.Width + pad * 2, sumSize.Height + pad * 2);
                                    // 绘制文字
                                    g.DrawString(summary, headerFont, Brushes.Lime, 10 + pad, 10 + pad);

                                    // B. 绘制各个区域的详细文字
                                    foreach (var txt in textOverlays)
                                    {
                                        using (Brush brush = new SolidBrush(txt.Color))
                                        {
                                            SizeF sz = g.MeasureString(txt.Text, font);
                                            // 绘制文字背景，防止和图像混在一起看不清
                                            g.FillRectangle(bgBrush, txt.Location.X, txt.Location.Y - sz.Height, sz.Width + 4, sz.Height + 4);
                                            g.DrawString(txt.Text, font, brush, txt.Location.X + 2, txt.Location.Y - sz.Height + 2);
                                        }
                                    }
                                }
                            }

                            if (showImage)
                            {
                                // 显示结果
                                this.Invoke(new Action(() =>
                                {
                                    showImageControl1.ImageBitmap = uiBitmap;
                                }));
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    this.Invoke(new Action(() => MessageBoxTD.Show($"算法执行错误: {ex.Message}")));
                }
            });

            return (retBitmap, retResults, retPass);
        }

        /// <summary>
        /// 分析单个区域
        /// 返回: 该区域详细分析结果
        /// </summary>
        private RegionResult AnalyzeSingleRegion(Mat srcMat, Mat displayMat, BinaryAnlysisParams param, List<TextOverlay> texts)
        {
            RegionResult result = new RegionResult
            {
                Name = param.Name,
                IsPass = false,
                Area = 0,
                Width = 0,
                Height = 0,
                DetectedRect = new Rect(0, 0, 0, 0)
            };

            string resultText = "";
            System.Drawing.Point textLoc = new System.Drawing.Point((int)param.Region.Column, (int)param.Region.Row);

            // 1. 生成 ROI Mask
            using (Mat mask = new Mat(srcMat.Size(), MatType.CV_8UC1, Scalar.Black))
            using (Mat grayMat = new Mat())
            using (Mat binary = new Mat())
            {
                RotatedRect rr = new RotatedRect(
                    new Point2f((float)param.Region.Column, (float)param.Region.Row),
                    new Size2f((float)param.Region.Length1 * 2, (float)param.Region.Length2 * 2),
                    (float)(param.Region.Phi * 180.0 / Math.PI)
                );

                // 填充搜索区域
                result.SearchRegion = rr;

                Point2f[] vertices = rr.Points();
                OpenCvSharp.Point[] points = vertices.Select(v => new OpenCvSharp.Point((int)v.X, (int)v.Y)).ToArray();
                Cv2.FillConvexPoly(mask, points, Scalar.White);

                // 更新文字位置到 ROI 左上角，方便查看
                Rect bounding = rr.BoundingRect();
                textLoc = new System.Drawing.Point(Math.Max(0, bounding.X), Math.Max(0, bounding.Y - 20));

                if (srcMat.Channels() > 1)
                    Cv2.CvtColor(srcMat, grayMat, ColorConversionCodes.BGR2GRAY);
                else
                    srcMat.CopyTo(grayMat);

                // 3. 区间二值化
                using (Mat tempBin = new Mat())
                using (Mat maskMax = new Mat())
                {
                    Cv2.Threshold(grayMat, tempBin, param.ThreshMin, 255, ThresholdTypes.Binary);
                    Cv2.Threshold(grayMat, maskMax, param.ThreshMax, 255, ThresholdTypes.BinaryInv);
                    Cv2.BitwiseAnd(tempBin, maskMax, tempBin);
                    Cv2.BitwiseAnd(tempBin, mask, binary);
                }

                // 4. 形态学
                MorphTypes op = GetMorphType(param.MorphStr);
                if (op != (MorphTypes)(-1) && param.KernelSize > 0)
                {
                    using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(param.KernelSize, param.KernelSize)))
                    {
                        Cv2.MorphologyEx(binary, binary, op, kernel);
                    }
                }

                // 5. 轮廓查找
                OpenCvSharp.Point[][] contours;
                HierarchyIndex[] hierarchy;
                Cv2.FindContours(binary, out contours, out hierarchy, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                // 6. 筛选与判定

                Rect rect = new Rect();

                if (contours.Length > 0)
                {
                    // 找最大轮廓
                    var maxContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
                    double area = Cv2.ContourArea(maxContour);
                    rect = Cv2.BoundingRect(maxContour);

                    // 填充识别到的信息
                    result.Area = area;
                    result.Width = rect.Width;
                    result.Height = rect.Height;
                    result.DetectedRect = rect;

                    bool passed = true;
                    if (param.EnableArea && (area < param.MinArea || area > param.MaxArea)) passed = false;
                    if (param.EnableWidth && (rect.Width < param.MinWidth || rect.Width > param.MaxWidth)) passed = false;
                    if (param.EnableHeight && (rect.Height < param.MinHeight || rect.Height > param.MaxHeight)) passed = false;

                    result.IsPass = passed;

                    Scalar color = passed ? Scalar.Lime : Scalar.Red;
                    // 画轮廓
                    Cv2.DrawContours(displayMat, new OpenCvSharp.Point[][] { maxContour }, -1, color, 2);
                    // 画外接矩形
                    Cv2.Rectangle(displayMat, rect, color, 1);

                    // 准备文字信息 (仅显示已启用项)
                    resultText = $"{param.Name}";
                    if (param.EnableArea)
                    {
                        resultText += $"\n面积:{area:F0}";
                    }

                    string whStr = "";
                    if (param.EnableWidth) whStr += $"宽:{rect.Width} ";
                    if (param.EnableHeight) whStr += $"高:{rect.Height}";

                    if (!string.IsNullOrEmpty(whStr))
                    {
                        resultText += $"\n{whStr.Trim()}";
                    }

                    if (!passed) resultText += "\n(NG)";
                }
                else
                {
                    resultText = $"{param.Name}: 未找到目标";
                    result.IsPass = false;
                }

                // 绘制 ROI 框 (OpenCV画线效率高，保留在这里)
                Scalar roiColor = result.IsPass ? Scalar.Green : Scalar.Orange;
                for (int j = 0; j < 4; j++)
                    Cv2.Line(displayMat, points[j], points[(j + 1) % 4], roiColor, 2);

                // 将文字信息添加到列表，待会儿统一用 Graphics 画
                texts.Add(new TextOverlay
                {
                    Text = resultText,
                    Location = textLoc,
                    Color = result.IsPass ? Color.Lime : Color.Red
                });
            }

            return result;
        }

        private MorphTypes GetMorphType(string typeStr)
        {
            if (string.IsNullOrEmpty(typeStr) || typeStr == "None") return (MorphTypes)(-1);
            if (typeStr.Contains("腐蚀") || typeStr.Contains("Erode")) return MorphTypes.Erode;
            if (typeStr.Contains("膨胀") || typeStr.Contains("Dilate")) return MorphTypes.Dilate;
            if (typeStr.Contains("开运算") || typeStr.Contains("Open")) return MorphTypes.Open;
            if (typeStr.Contains("闭运算") || typeStr.Contains("Close")) return MorphTypes.Close;
            return (MorphTypes)(-1);
        }

        private void buttonSave_Click(object sender, EventArgs e)
        {
            if (SaveParams())
                Hide();
        }

        private bool SaveParams()
        {
            try
            {
                NodeParamBinaryAnalysis param = new NodeParamBinaryAnalysis();
                param.Text1 = nodeSubscription1.GetText1();
                param.Text2 = nodeSubscription1.GetText2();
                param.BinaryAnlysisParams = this.BinaryAnlysisParams.ToList();
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
        /// 点击绘制区域 (UI 交互 - 假设控件支持此方法)
        /// </summary>
        private void toolStripButton1_Click(object sender, EventArgs e)
        {
            showImageControl1.ClearAllRoi();
            // 提示用户绘制，添加一个默认的可交互矩形
            showImageControl1.AddRoiRotatedRect(200, 200, 0, 100, 100, 1, Color.Green, "ROI");
        }

        /// <summary>
        /// 点击 "添加区域" 按钮
        /// 逻辑：自动获取区域 -> 自动计算阈值(Otsu) -> 测量 -> 设置公差(+/-50) -> 形态学默认关闭
        /// </summary>
        private void toolStripButton2_Click(object sender, EventArgs e)
        {
            // 1. 获取 UI 上的 ROI 数据
            var rois = showImageControl1.GetAllRotatedRectInfos();

            if (rois == null || rois.Count == 0)
            {
                MessageBoxTD.Show("请先在图像上绘制至少一个区域框！");
                return;
            }

            if (src == null)
            {
                MessageBoxTD.Show("请先加载图像！");
                return;
            }

            int addedCount = 0;

            try
            {
                // 准备 OpenCV Mat
                using (Mat sourceMat = BitmapConverter.ToMat(src))
                using (Mat grayMat = new Mat())
                {
                    // 转灰度
                    if (sourceMat.Channels() > 1)
                        Cv2.CvtColor(sourceMat, grayMat, ColorConversionCodes.BGR2GRAY);
                    else
                        sourceMat.CopyTo(grayMat);

                    foreach (var roi in rois)
                    {
                        // --- 1. 创建参数对象 ---
                        BinaryAnlysisParams newParam = new BinaryAnlysisParams();
                        newParam.Name = $"ROI_{BinaryAnlysisParams.Count + 1}";

                        // 保存几何参数
                        newParam.Region = new BinaryRegion
                        {
                            Column = roi.Column,
                            Row = roi.Row,
                            Phi = roi.Phi,
                            Length1 = roi.Length1,
                            Length2 = roi.Length2
                        };

                        // --- 2. 自动阈值 (Otsu) ---
                        RotatedRect rr = new RotatedRect(
                            new Point2f((float)roi.Column, (float)roi.Row),
                            new Size2f((float)roi.Length1 * 2, (float)roi.Length2 * 2),
                            (float)(roi.Phi * 180.0 / Math.PI)
                        );
                        Rect boundingRect = rr.BoundingRect();
                        // 限制在图像范围内
                        boundingRect = boundingRect.Intersect(new Rect(0, 0, grayMat.Width, grayMat.Height));

                        double autoThresh = 128; // 默认值
                        if (boundingRect.Width > 0 && boundingRect.Height > 0)
                        {
                            using (Mat roiMat = new Mat(grayMat, boundingRect))
                            using (Mat trash = new Mat())
                            {
                                // 使用 Otsu 寻找高亮区域 (THRESH_BINARY | THRESH_OTSU)
                                autoThresh = Cv2.Threshold(roiMat, trash, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                            }
                        }

                        newParam.ThreshMin = (int)autoThresh;
                        newParam.ThreshMax = 255;

                        // --- 3. 形态学默认关闭 ---
                        newParam.MorphStr = "无";
                        newParam.KernelSize = 3;

                        // --- 4. 自动测量并设置判定参数 ---
                        using (Mat mask = new Mat(sourceMat.Size(), MatType.CV_8UC1, Scalar.Black))
                        using (Mat binary = new Mat())
                        {
                            // A. 绘制 Mask
                            Point2f[] vertices = rr.Points();
                            OpenCvSharp.Point[] pts = vertices.Select(v => new OpenCvSharp.Point((int)v.X, (int)v.Y)).ToArray();
                            Cv2.FillConvexPoly(mask, pts, Scalar.White);

                            // B. 二值化 (使用刚计算的自动阈值)
                            using (Mat tempBin = new Mat())
                            using (Mat maskMax = new Mat())
                            {
                                Cv2.Threshold(grayMat, tempBin, newParam.ThreshMin, 255, ThresholdTypes.Binary);
                                Cv2.Threshold(grayMat, maskMax, newParam.ThreshMax, 255, ThresholdTypes.BinaryInv);
                                Cv2.BitwiseAnd(tempBin, maskMax, tempBin);
                                Cv2.BitwiseAnd(tempBin, mask, binary);
                            }

                            // C. 测量
                            OpenCvSharp.Point[][] contours;
                            HierarchyIndex[] h;
                            Cv2.FindContours(binary, out contours, out h, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                            if (contours.Length > 0)
                            {
                                var maxContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
                                double measuredArea = Cv2.ContourArea(maxContour);
                                Rect measuredRect = Cv2.BoundingRect(maxContour);
                                int tolerance = 50;

                                newParam.EnableArea = true;
                                newParam.MinArea = Math.Max(0, measuredArea - tolerance);
                                newParam.MaxArea = measuredArea + tolerance;

                                newParam.EnableWidth = true;
                                newParam.MinWidth = Math.Max(0, measuredRect.Width - tolerance);
                                newParam.MaxWidth = measuredRect.Width + tolerance;

                                newParam.EnableHeight = true;
                                newParam.MinHeight = Math.Max(0, measuredRect.Height - tolerance);
                                newParam.MaxHeight = measuredRect.Height + tolerance;
                            }
                            else
                            {
                                newParam.EnableArea = false;
                                newParam.EnableWidth = false;
                                newParam.EnableHeight = false;
                            }
                        }

                        this.BinaryAnlysisParams.Add(newParam);
                        AddButtonToPanel(newParam);
                        addedCount++;
                    }

                    if (addedCount > 0)
                    {
                        //MessageBoxTD.Show($"成功添加 {addedCount} 个区域！\r\n已自动计算阈值并设置判定参数(±50)。");
                        showImageControl1.ClearAllRoi();
                        RunAnalysisAsync(false);
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"添加区域时发生错误: {ex.Message}");
            }
        }

        private void AddButtonToPanel(BinaryAnlysisParams param)
        {
            Button button = new Button() { Text = param.Name, Height = 40, Width = 100, Margin = new Padding(5) };
            button.Click += (s, e) =>
            {
                int index = BinaryAnlysisParams.IndexOf(param);
                if (index >= 0) flowDirectionPanel1.SelectedIndex = index;
            };

            ContextMenuStrip contextMenu = new ContextMenuStrip();
            ToolStripMenuItem deleteMenuItem = new ToolStripMenuItem("删除");
            deleteMenuItem.Click += (_, __) =>
            {
                int index = BinaryAnlysisParams.IndexOf(param);
                if (index >= 0)
                {
                    flowDirectionPanel1.RemoveControl(button);
                    BinaryAnlysisParams.RemoveAt(index);
                    if (flowDirectionPanel1.Controls.Count > 0)
                        flowDirectionPanel1.SelectedIndex = 0;
                }
                contextMenu.Dispose();
            };
            contextMenu.Items.Add(deleteMenuItem);
            button.ContextMenuStrip = contextMenu;
            flowDirectionPanel1.AddControl(button);
        }

        /// <summary>
        /// 辅助方法：从界面控件读取当前设置的参数
        /// </summary>
        private BinaryAnlysisParams GetParamsFromUI()
        {
            return new BinaryAnlysisParams
            {
                Name = textBox2.Text,
                ThreshMin = uiTrackBar1.Value,
                ThreshMax = uiTrackBar2.Value,
                MorphStr = comboBox2.SelectedItem?.ToString() ?? "None",
                KernelSize = (int)uiIntegerUpDown4.Value,

                EnableArea = uiCheckBox1.Checked,
                MinArea = uiDoubleUpDown2.Value,
                MaxArea = uiDoubleUpDown3.Value,

                EnableHeight = uiCheckBox2.Checked,
                MinHeight = uiDoubleUpDown5.Value,
                MaxHeight = uiDoubleUpDown6.Value,

                EnableWidth = uiCheckBox3.Checked,
                MinWidth = uiDoubleUpDown7.Value,
                MaxWidth = uiDoubleUpDown8.Value,

                // 默认空Region
                Region = new BinaryRegion()
            };
        }


        /// <summary>
        /// 初始化界面数据 (从 Profile 加载到 UI)
        /// </summary>
        void InitDate()
        {
            int index = flowDirectionPanel1.SelectedIndex;
            if (index == -1 || index >= BinaryAnlysisParams.Count) return;

            BinaryAnlysisParams anlysisParams = BinaryAnlysisParams[index];

            //名称
            textBox2.Text = anlysisParams.Name;

            //二值化调整
            uiTrackBar1.Value = anlysisParams.ThreshMin;
            uiTrackBar2.Value = anlysisParams.ThreshMax;

            //结果优化 形态学
            comboBox2.SelectedItem = anlysisParams.MorphStr;
            uiIntegerUpDown4.Value = anlysisParams.KernelSize;

            //结果判定
            uiCheckBox1.Checked = anlysisParams.EnableArea;
            uiDoubleUpDown2.Value = anlysisParams.MinArea;
            uiDoubleUpDown3.Value = anlysisParams.MaxArea;

            uiCheckBox2.Checked = anlysisParams.EnableHeight;
            uiDoubleUpDown5.Value = anlysisParams.MinHeight;
            uiDoubleUpDown6.Value = anlysisParams.MaxHeight;

            uiCheckBox3.Checked = anlysisParams.EnableWidth;
            uiDoubleUpDown7.Value = anlysisParams.MinWidth;
            uiDoubleUpDown8.Value = anlysisParams.MaxWidth;
        }

        /// <summary>
        /// 更新数据 (从 UI 保存到 Profile)
        /// </summary>
        void UpdataDate()
        {
            if (!_eventsEnabled) return;
            int index = flowDirectionPanel1.SelectedIndex;
            if (index == -1 || index >= BinaryAnlysisParams.Count) return;

            if (flowDirectionPanel1.Controls.Count > index)
            {
                ((Button)flowDirectionPanel1.Controls[index]).Text = textBox2.Text;
            }

            BinaryAnlysisParams anlysisParams = BinaryAnlysisParams[index];

            //名称
            anlysisParams.Name = textBox2.Text;

            //二值化调整
            anlysisParams.ThreshMin = uiTrackBar1.Value;
            anlysisParams.ThreshMax = uiTrackBar2.Value;

            //结果优化 形态学
            anlysisParams.MorphStr = comboBox2.SelectedItem?.ToString() ?? "None";
            anlysisParams.KernelSize = (int)uiIntegerUpDown4.Value;

            //结果判定
            anlysisParams.EnableArea = uiCheckBox1.Checked;
            anlysisParams.MinArea = uiDoubleUpDown2.Value;
            anlysisParams.MaxArea = uiDoubleUpDown3.Value;

            anlysisParams.EnableHeight = uiCheckBox2.Checked;
            anlysisParams.MinHeight = uiDoubleUpDown5.Value;
            anlysisParams.MaxHeight = uiDoubleUpDown6.Value;

            anlysisParams.EnableWidth = uiCheckBox3.Checked;
            anlysisParams.MinWidth = uiDoubleUpDown7.Value;
            anlysisParams.MaxWidth = uiDoubleUpDown8.Value;

            RunCureentDetection();
        }

        /// <summary>
        /// 识别当前区域
        /// </summary>
        private void RunCureentDetection()
        {
            int index = flowDirectionPanel1.SelectedIndex;
            if (index == -1 || index >= BinaryAnlysisParams.Count) return;

            if (src == null) return;

            BinaryAnlysisParams anlysisParams = BinaryAnlysisParams[index];

            try
            {
                List<TextOverlay> overlays = new List<TextOverlay>();
                using (Mat sourceMat = BitmapConverter.ToMat(src))
                using (Mat displayMat = sourceMat.Clone())
                {
                    AnalyzeSingleRegion(sourceMat, displayMat, anlysisParams, overlays);

                    // 单区域测试时也要绘制文字
                    Bitmap resultBmp = BitmapConverter.ToBitmap(displayMat);
                    using (Graphics g = Graphics.FromImage(resultBmp))
                    {
                        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
                        float baseFontSize = Math.Max(12f, src.Height / 60f);

                        using (Font font = new Font("Microsoft YaHei UI", baseFontSize, FontStyle.Bold))
                        using (Brush bgBrush = new SolidBrush(Color.FromArgb(120, 0, 0, 0)))
                        {
                            foreach (var txt in overlays)
                            {
                                using (Brush brush = new SolidBrush(txt.Color))
                                {
                                    SizeF sz = g.MeasureString(txt.Text, font);
                                    g.FillRectangle(bgBrush, txt.Location.X, txt.Location.Y - sz.Height, sz.Width + 4, sz.Height + 4);
                                    g.DrawString(txt.Text, font, brush, txt.Location.X + 2, txt.Location.Y - sz.Height + 2);
                                }
                            }
                        }
                    }
                    showImageControl1.ImageBitmap = resultBmp;
                }
            }
            catch (Exception ex)
            {
                // 忽略或记录日志，避免UI操作频繁报错
            }
        }

        /// <summary>
        /// 拖动uiTrackBar控件,使二值化阈值调整
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void uiTrackBar1_ValueChanged(object sender, EventArgs e) { UpdataDate(); }

        /// <summary>
        /// 开始结果判定 面积-高度-宽度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void uiCheckBox1_CheckedChanged(object sender, EventArgs e) { UpdataDate(); }

        /// <summary>
        /// 结果判定上下限值 面积-高度-宽度
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="value"></param>
        private void uiDoubleUpDown2_ValueChanged(object sender, double value) { UpdataDate(); }

        /// <summary>
        /// 名称更改
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void textBox2_TextChanged(object sender, EventArgs e) { UpdataDate(); }

        /// <summary>
        /// 自动获取当前区域的值 (魔棒功能: Auto Otsu + Measure +/- 50)
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void toolStripButton3_Click(object sender, EventArgs e)
        {
            int index = flowDirectionPanel1.SelectedIndex;
            if (index == -1 || index >= BinaryAnlysisParams.Count)
            {
                MessageBoxTD.Show("请先选择一个区域参数！");
                return;
            }

            if (src == null) return;

            BinaryAnlysisParams param = BinaryAnlysisParams[index];

            try
            {
                using (Mat sourceMat = BitmapConverter.ToMat(src))
                using (Mat grayMat = new Mat())
                {
                    if (sourceMat.Channels() > 1) Cv2.CvtColor(sourceMat, grayMat, ColorConversionCodes.BGR2GRAY);
                    else sourceMat.CopyTo(grayMat);

                    // 1. 获取ROI图像，计算Otsu阈值
                    RotatedRect rr = new RotatedRect(
                        new Point2f((float)param.Region.Column, (float)param.Region.Row),
                        new Size2f((float)param.Region.Length1 * 2, (float)param.Region.Length2 * 2),
                        (float)(param.Region.Phi * 180.0 / Math.PI)
                    );
                    Rect boundingRect = rr.BoundingRect();
                    boundingRect = boundingRect.Intersect(new Rect(0, 0, grayMat.Width, grayMat.Height));

                    if (boundingRect.Width > 0 && boundingRect.Height > 0)
                    {
                        using (Mat roiMat = new Mat(grayMat, boundingRect))
                        using (Mat trash = new Mat())
                        {
                            // 使用 Otsu 寻找最佳阈值
                            double thresh = Cv2.Threshold(roiMat, trash, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                            param.ThreshMin = (int)thresh;
                            param.ThreshMax = 255;
                        }
                    }

                    // 2. 不再重置形态学，保持当前参数
                    // param.MorphStr = "None"; 

                    // 3. 测量并设置公差
                    using (Mat mask = new Mat(sourceMat.Size(), MatType.CV_8UC1, Scalar.Black))
                    using (Mat binary = new Mat())
                    {
                        // 绘制 Mask
                        Point2f[] vertices = rr.Points();
                        OpenCvSharp.Point[] points = vertices.Select(v => new OpenCvSharp.Point((int)v.X, (int)v.Y)).ToArray();
                        Cv2.FillConvexPoly(mask, points, Scalar.White);

                        // 二值化
                        using (Mat tempBin = new Mat())
                        using (Mat maskMax = new Mat())
                        {
                            Cv2.Threshold(grayMat, tempBin, param.ThreshMin, 255, ThresholdTypes.Binary);
                            Cv2.Threshold(grayMat, maskMax, param.ThreshMax, 255, ThresholdTypes.BinaryInv);
                            Cv2.BitwiseAnd(tempBin, maskMax, tempBin);
                            Cv2.BitwiseAnd(tempBin, mask, binary);
                        }

                        // 新增：应用当前的形态学参数，确保自动计算的数值是基于处理后的图像
                        MorphTypes op = GetMorphType(param.MorphStr);
                        if (op != (MorphTypes)(-1) && param.KernelSize > 0)
                        {
                            using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(param.KernelSize, param.KernelSize)))
                            {
                                Cv2.MorphologyEx(binary, binary, op, kernel);
                            }
                        }

                        // 轮廓查找
                        OpenCvSharp.Point[][] contours;
                        HierarchyIndex[] h;
                        Cv2.FindContours(binary, out contours, out h, RetrievalModes.External, ContourApproximationModes.ApproxSimple);

                        if (contours.Length > 0)
                        {
                            var maxContour = contours.OrderByDescending(c => Cv2.ContourArea(c)).First();
                            double area = Cv2.ContourArea(maxContour);
                            Rect rect = Cv2.BoundingRect(maxContour);
                            int tolerance = 50;

                            param.EnableArea = true;
                            param.MinArea = Math.Max(0, area - tolerance);
                            param.MaxArea = area + tolerance;

                            param.EnableWidth = true;
                            param.MinWidth = Math.Max(0, rect.Width - tolerance);
                            param.MaxWidth = rect.Width + tolerance;

                            param.EnableHeight = true;
                            param.MinHeight = Math.Max(0, rect.Height - tolerance);
                            param.MaxHeight = rect.Height + tolerance;
                        }
                    }
                }

                // 4. 更新界面并显示结果
                InitDate();
                RunCureentDetection();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"自动计算出错: {ex.Message}");
            }
        }

        private void comboBox2_SelectedIndexChanged(object sender, EventArgs e)
        {
            UpdataDate();
        }

        private void uiIntegerUpDown4_ValueChanged(object sender, int value)
        {
            UpdataDate();
        }
    }
}
