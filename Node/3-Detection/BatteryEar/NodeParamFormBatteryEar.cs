using OpenCvSharp.Extensions;
using OpenCvSharp;
using System;
using Point = OpenCvSharp.Point;
using TDJS_Vision.Forms.YTMessageBox;
using System.Collections.Generic;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Forms.ShapeDraw;
using Logger;
using System.Linq;
using TDJS_Vision.Forms.ImageViewer;
using HslCommunication.Profinet.Delta;
using System.IO;
using System.Drawing;
namespace TDJS_Vision.Node._3_Detection.BatteryEar
{
    public partial class NodeParamFormBatteryEar : FormBase, INodeParamForm
    {
        /// <summary>
        /// 所属流程。
        /// </summary>
        private readonly Process process;

        /// <summary>
        /// 所属节点。
        /// </summary>
        private readonly NodeBase node;

        /// <summary>
        /// 当前借用的上游原图，不由参数窗体释放。
        /// </summary>
        private Mat src;

        /// <summary>
        /// 当前节点独占的模板管理窗体，随参数窗体一起释放。
        /// </summary>
        private readonly FormNewTemplate _formNewTemplate;

        /// <summary>
        /// 参数窗体自有资源是否已经释放。
        /// </summary>
        private int _resourcesReleased;


        public NodeParamFormBatteryEar(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            this.node = nodeBase;
            _formNewTemplate = new FormNewTemplate();
            imageROIEditControl1.SetROIType2Draw(ROIType.Rectangle);
            Shown += NodeParamFormBatteryEar_Shown;
        }

        private void NodeParamFormBatteryEar_Shown(object sender, EventArgs e)
        {
            InitDeviceComboBox();
        }
        /// <summary>
        /// 初始化Modbus下拉框
        /// </summary>
        private void InitDeviceComboBox()
        {
            string text1 = comboBoxDevice.Text;

            comboBoxDevice.Items.Clear();
            comboBoxDevice.Items.Add("[未设置]");
            // 初始化Modbus列表,只显示添加的Modbus用户自定义名称
            foreach (var device in Solution.Instance.AllDevices)
            {
                switch (device.DevType)
                {
                    case Device.DevType.PLC:
                    case Device.DevType.ModbusRTUPoll:
                    case Device.DevType.ModbusTcpPoll:
                        comboBoxDevice.Items.Add(device.UserDefinedName);
                        break;
                    default:
                        continue;
                }
            }
            int index1 = comboBoxDevice.Items.IndexOf(text1);
            comboBoxDevice.SelectedIndex = index1 == -1 ? 0 : index1;
        }

        public INodeParam Params { get; set; }

        /// <summary>
        /// 反序列化还原界面参数设置
        /// </summary>
        public void SetParam2Form()
        {
            if (Params is NodeParamBatteryEar param)
            {
                try
                {
                    nodeSubscription1.SetText(param.Text1, param.Text2);
                    imageROIEditControl1.SetROIs(param.ROIs);
                    textBoxFixtureWidthMM.Text = param.FixtureWidthMM.ToString();
                    labelPixNum.Text = param.FixtureWidthPix.ToString();
                    textBoxScale.Text = param.Scale.ToString();
                    textBoxScore.Text = param.Score.ToString();
                    textBoxDeltaMM1.Text = param.DeltaMM1.ToString();
                    textBoxDeltaMM2.Text = param.DeltaMM2.ToString();
                    textBoxDeltaMM3.Text = param.DeltaMM3.ToString();
                    textBoxDeltaMM4.Text = param.DeltaMM4.ToString();
                    textBoxImgSavePath.Text = param.ImageSavePath;

                    // 通信
                    InitDeviceComboBox();
                    comboBoxDevice.SelectedItem = param.DeviceName;
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
        public void UpdataImage()
        {
            OutputImage currentOutput = nodeSubscription1.GetValue<OutputImage>();
            if (currentOutput == null ||
                currentOutput.Bitmaps == null ||
                currentOutput.Bitmaps.Count == 0 ||
                currentOutput.Bitmaps[0] == null ||
                currentOutput.Bitmaps[0].Empty())
            {
                throw new Exception("订阅的图像为null！");
            }

            src = currentOutput.Bitmaps[0];
            Bitmap roiPreview = null;
            try
            {
                roiPreview = src.ToBitmap();
                imageROIEditControl1.SetImage(roiPreview);
                roiPreview = null;
            }
            finally
            {
                roiPreview?.Dispose();
            }
        }

        /// <summary>
        /// 点击执行锂电池极耳检测
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            Mat resultImage = null;
            try
            {
                var (vals, rects, lines, img) = CalculateJierToMarkVerticalDistancesOnOriginal();
                resultImage = img;
                // 绘制图像
                for (int i = 0; i < rects.Count; ++i)
                    Cv2.Rectangle(img, rects[i], Scalar.Blue, 5);
                foreach (var line in lines)
                    Cv2.Line(img, line.P1, line.P2, Scalar.Red, 5);
                // 单位转换、补偿
                vals = vals.Select(x => x * double.Parse(textBoxScale.Text)).ToList();
                Cv2.PutText(img, $"D1:{vals[0] + double.Parse(textBoxDeltaMM1.Text):0.000}mm  " +
                    $"D2:{vals[1] + double.Parse(textBoxDeltaMM2.Text):0.000}mm  " +
                    $"D3:{vals[2] + double.Parse(textBoxDeltaMM3.Text):0.000}mm  " +
                    $"D4:{vals[3] + double.Parse(textBoxDeltaMM4.Text):0.000}mm", new Point(100, 100), HersheyFonts.Italic, 3, Scalar.Green, 5);
                imageROIEditControl1.SetImage(img.ToBitmap());
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"锂电池极耳异常：原因：{ex.Message}");
            }
            finally
            {
                resultImage?.Dispose();
            }
        }

        /// <summary>
        /// 解析ROI图像和位置，区分左右Mark点和极耳
        /// </summary>
        /// <param name="roiImages"></param>
        /// <param name="roiLocations"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        public (List<(Mat, Rect)>, List<(Mat, Rect)>) ParseROI(List<Mat> roiImages, List<Rect> roiLocations)
        {
            if (roiImages.Count != roiLocations.Count || roiImages.Count != 6)
                throw new ArgumentException("确保Mark点和极耳总共6个ROI图像及其位置！");

            // 分别存储左、右mark点和极耳的列表
            var leftMarkEars = new List<(Mat, Rect)>();
            var rightMarkEars = new List<(Mat, Rect)>();

            // 按X坐标对ROI进行排序，以区分左右侧
            var sortedROIs = new List<(Mat, Rect, int)>(); // 添加索引以保留原始对应关系
            for (int i = 0; i < roiImages.Count; i++)
            {
                sortedROIs.Add((roiImages[i], roiLocations[i], i));
            }
            sortedROIs.Sort((a, b) => a.Item2.X.CompareTo(b.Item2.X)); // 根据X坐标升序排序

            foreach (var item in sortedROIs)
            {
                if (item.Item2.X < 1500) // 这里假设X=1500为左右侧的分界线，请根据实际情况调整
                {
                    // 左侧处理
                    leftMarkEars.Add((item.Item1, item.Item2));
                }
                else
                {
                    // 右侧处理
                    rightMarkEars.Add((item.Item1, item.Item2));
                }
            }

            // 对两侧分别按Y坐标排序，以确保从上到下的正确顺序
            leftMarkEars.Sort((a, b) => a.Item2.Y.CompareTo(b.Item2.Y));
            rightMarkEars.Sort((a, b) => a.Item2.Y.CompareTo(b.Item2.Y));

            // 返回结果
            return (leftMarkEars, rightMarkEars);
        }
        /// <summary>
        /// 将 Mat 图像保存到 D:\存图 路径下，文件名为当前时间戳
        /// </summary>
        /// <param name="mat">要保存的图像</param>
        /// <returns>保存的文件完整路径，失败返回 null</returns>
        public string SaveMatToD(string folderPath = @"D:\Images")
        {
            if (src == null || src.Empty())
                return null;

            try
            {
                // 获取当前日期，用于创建子文件夹
                string dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                string subFolderPath = Path.Combine(folderPath, dateFolder);

                // 确保日期子目录存在
                if (!Directory.Exists(subFolderPath))
                    Directory.CreateDirectory(subFolderPath);

                // 生成文件名：时-分-秒-毫秒.jpg（避免文件名过长）
                string fileName = DateTime.Now.ToString("HH-mm-ss-fff") + ".jpg";
                string fullPath = Path.Combine(subFolderPath, fileName);

                // 保存图像
                Cv2.ImWrite(fullPath, src);
                return fullPath;
            }
            catch (Exception ex)
            {
                // 可选：记录日志或处理异常
                return null;
            }
        }
        public (List<double> distances, List<Rect> rects, List<LineSegmentPoint> lines, Mat resultImage) CalculateJierToMarkVerticalDistancesOnOriginal()
        {
            List<double> distances = new List<double>(new double[4] { 0, 0, 0, 0 }); // 初始化为0
            List<Rect> rects = new List<Rect>(); // 矩形框
            List<LineSegmentPoint> lines = new List<LineSegmentPoint>();
            Mat resultImage = src.Clone(); // 在原图副本上绘制
            List<Mat> roiImagesToDispose = null;
            List<Mat> jiErTemplateSnapshots = null;
            List<Mat> markTemplateSnapshots = null;
            try
            {
                SaveMatToD(textBoxImgSavePath.Text); // 存图
                var matchThreshold = double.Parse(textBoxScore.Text);
                jiErTemplateSnapshots = _formNewTemplate.GetJiErTemplate();
                markTemplateSnapshots = _formNewTemplate.GetMarkTemplate();
                var roiImages = imageROIEditControl1.GetROIImages();
                roiImagesToDispose = roiImages == null ? new List<Mat>() : roiImages.ToList();
                var roiLocations = imageROIEditControl1.GetImageROIRects();

                // 去掉最高的那个治具框
                roiImages.RemoveAll(m => m.Height > 1000);
                roiLocations.RemoveAll(r => r.Height > 1000);

                // 解析出左右的极耳和Mark点
                var (leftRois, rightRois) = ParseROI(roiImages, roiLocations);

                OpenCvSharp.Size templateJierSize;
                OpenCvSharp.Size templateMarkSize;

                foreach (var (side, isLeft) in new[] { (leftRois, true), (rightRois, false) })
                {
                    if (side.Count != 3) continue; // 每边应有3个元素：mark点、上极耳、下极耳

                    // 获取mark点的位置
                    Point markCenter = GetTemplateCenter(markTemplateSnapshots, resultImage, side[0].Item2, matchThreshold, out templateMarkSize);
                    if (markCenter.X == -1)
                        continue;

                    rects.Add(new Rect(
                        markCenter.X - templateMarkSize.Width / 2,
                        markCenter.Y - templateMarkSize.Height / 2,
                        templateMarkSize.Width,
                        templateMarkSize.Height));

                    for (int i = 1; i < side.Count; i++)
                    {
                        Point jierCenter = GetTemplateCenter(jiErTemplateSnapshots, resultImage, side[i].Item2, matchThreshold, out templateJierSize);

                        if (jierCenter.X == -1) continue; // 如果未能成功找到中心点

                        // 计算极耳中心于Mark点中心的垂直距离并保存
                        distances[(isLeft ? 0 : 2) + (i - 1)] = Math.Abs(jierCenter.Y - markCenter.Y);

                        rects.Add(new Rect(
                            jierCenter.X - templateJierSize.Width / 2,
                            jierCenter.Y - templateJierSize.Height / 2,
                            templateJierSize.Width,
                            templateJierSize.Height));
                        lines.Add(new LineSegmentPoint(jierCenter, markCenter));

                    }
                }
            }
            catch
            {
                resultImage.Dispose();
                throw;
            }
            finally
            {
                if (roiImagesToDispose != null)
                {
                    foreach (Mat roiImage in roiImagesToDispose)
                        roiImage?.Dispose();
                }
                if (jiErTemplateSnapshots != null)
                {
                    foreach (Mat template in jiErTemplateSnapshots)
                        template?.Dispose();
                }
                if (markTemplateSnapshots != null)
                {
                    foreach (Mat template in markTemplateSnapshots)
                        template?.Dispose();
                }
            }

            return (distances, rects, lines, resultImage);
        }

        /// <summary>
        /// 在指定区域执行模板匹配并返回模板中心。
        /// </summary>
        /// <param name="templates">本轮检测持有的模板快照。</param>
        /// <param name="originalImage">只读源图。</param>
        /// <param name="roiRect">匹配区域。</param>
        /// <param name="matchThreshold">最低匹配阈值。</param>
        /// <param name="templateSize">命中模板的宽高，未命中时为空尺寸。</param>
        /// <returns>模板中心；未达到阈值时返回(-1,-1)。</returns>
        private Point GetTemplateCenter(List<Mat> templates, Mat originalImage, Rect roiRect, double matchThreshold, out OpenCvSharp.Size templateSize)
        {
            templateSize = new OpenCvSharp.Size();
            using (Mat roiImage = new Mat(originalImage, roiRect))
            using (Mat result = new Mat())
            {
                double minVal, maxVal, targetV = double.MinValue;
                Point minLoc, maxLoc, targetPoint = new Point();
                Mat matchedTemplate = null;
                foreach (Mat candidateTemplate in templates)
                {
                    Cv2.MatchTemplate(roiImage, candidateTemplate, result, TemplateMatchModes.CCoeffNormed);
                    Cv2.MinMaxLoc(result, out minVal, out maxVal, out minLoc, out maxLoc);
                    if (targetV < maxVal)
                    {
                        targetV = maxVal;
                        targetPoint = maxLoc;
                        matchedTemplate = candidateTemplate;
                    }
                }

                if (targetV >= matchThreshold && matchedTemplate != null)
                {
                    templateSize = new OpenCvSharp.Size(matchedTemplate.Width, matchedTemplate.Height);
                    return new Point(
                        targetPoint.X + matchedTemplate.Width / 2 + roiRect.X,
                        targetPoint.Y + matchedTemplate.Height / 2 + roiRect.Y);
                }

                return new Point(-1, -1); // 表示未找到
            }
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
                NodeParamBatteryEar param = new NodeParamBatteryEar();
                param.Text1 = nodeSubscription1.GetText1();
                param.Text2 = nodeSubscription1.GetText2();
                param.Score = double.Parse(textBoxScore.Text);
                param.ROIs = imageROIEditControl1.GetROIs();
                if (param.Score < 0 || param.Score > 1)
                    throw new Exception("分数设置范围[0,1]！");
                param.FixtureWidthMM = double.Parse(textBoxFixtureWidthMM.Text);
                param.FixtureWidthPix = double.Parse(labelPixNum.Text);
                param.Scale = double.Parse(textBoxScale.Text);
                param.DeltaMM1 = double.Parse(textBoxDeltaMM1.Text);
                param.DeltaMM2 = double.Parse(textBoxDeltaMM2.Text);
                param.DeltaMM3 = double.Parse(textBoxDeltaMM3.Text);
                param.DeltaMM4 = double.Parse(textBoxDeltaMM4.Text);
                param.ImageSavePath = textBoxImgSavePath.Text;
                // 通信
                param.DeviceName = comboBoxDevice.SelectedItem.ToString();
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
        /// 获取治具宽度像素数量
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonGetPixNum_Click(object sender, EventArgs e)
        {
            Mat srcCoppy = src.Clone();
            List<Mat> roiImgs = null;
            try
            {
                roiImgs = imageROIEditControl1.GetROIImages();
                var rects = imageROIEditControl1.GetImageROIRects();

                if (rects.Count < 1 || roiImgs == null || roiImgs.Count == 0)
                {
                    LogHelper.AddLog(MsgLevel.Exception, "没有绘制任何ROI！", true);
                    return;
                }

                // 找到最高的ROI图像和矩形，就是治具的宽度
                var tallestImage = roiImgs.Where(img => img != null).OrderByDescending(img => img.Height).FirstOrDefault();
                var tallestRect = rects.Where(rect => rect != null).OrderByDescending(rect => rect.Height).FirstOrDefault();

                using (Mat gray = new Mat())
                using (Mat binary = new Mat())
                using (Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(11, 11)))
                using (Mat cleaned = new Mat())
                {
                    Cv2.CvtColor(tallestImage, gray, ColorConversionCodes.BGR2GRAY);
                    Cv2.Threshold(gray, binary, 0, 255, ThresholdTypes.Binary | ThresholdTypes.Otsu);
                    Cv2.MorphologyEx(binary, cleaned, MorphTypes.Open, kernel);

                    // 求得治具上下宽的点
                    var (p1, p2) = MarkTransitionPoints(cleaned);
                    p1 = new Point(p1.X + tallestRect.X, p1.Y + tallestRect.Y);
                    p2 = new Point(p2.X + tallestRect.X, p2.Y + tallestRect.Y);

                    if (p1.Y != -1)
                        Cv2.Circle(srcCoppy, p1, 9, Scalar.Red, -1);
                    if (p2.Y != -1)
                        Cv2.Circle(srcCoppy, p2, 9, Scalar.Red, -1);
                    if (p1.Y != -1 && p2.Y != -1)
                        Cv2.Line(srcCoppy, p1, p2, Scalar.Green, 5, LineTypes.AntiAlias);

                    var length = Math.Abs(p1.Y - p2.Y);
                    imageROIEditControl1.SetImage(srcCoppy.ToBitmap());
                    labelPixNum.Text = length.ToString();
                    textBoxScale.Text = (double.Parse(textBoxFixtureWidthMM.Text) / length).ToString("F5");
                }
            }
            finally
            {
                srcCoppy.Dispose();
                if (roiImgs != null)
                {
                    foreach (Mat roiImage in roiImgs)
                        roiImage?.Dispose();
                }
            }
        }
        /// <summary>
        /// 找到第一次“暗到亮”和最后一次“亮到暗”的索引
        /// </summary>
        /// <param name="grays">一列像素的灰度值</param>
        /// <param name="threshold">变化阈值，用于判断显著跳变</param>
        /// <returns>(firstDarkToLightIndex, lastLightToDarkIndex)</returns>
        public static (int firstDarkToLight, int lastLightToDark) FindTransitions(List<byte> grays, int threshold = 5)
        {
            if (grays == null || grays.Count < 2)
                return (-1, -1);

            int firstDarkToLight = -1;  // 第一次暗到亮
            int lastLightToDark = -1;   // 最后一次亮到暗

            for (int i = 1; i < grays.Count; i++)
            {
                int delta = grays[i] - grays[i - 1];

                // 检测“暗到亮”：上升沿
                if (delta >= threshold && firstDarkToLight == -1)
                {
                    firstDarkToLight = i; // 变化发生在 i 位置
                }

                // 检测“亮到暗”：下降沿，不断更新
                if (delta <= -threshold)
                {
                    lastLightToDark = i;
                }
            }

            return (firstDarkToLight, lastLightToDark);
        }
        ///// <summary>
        ///// 在二值化图像的竖直中线上找到第一次白→黑和最后一次黑→白的跳变点，并绘制标记和连线
        ///// </summary>
        ///// <param name="binaryImage">输入的二值化图像 (单通道, 0 或 255)</param>
        ///// <returns>绘制后的图像</returns>
        public (Point, Point) MarkTransitionPoints(Mat binaryImage)
        {
            if (binaryImage.Channels() != 1)
                LogHelper.AddLog(MsgLevel.Exception, "输入图像必须是单通道二值图像。", true);

            int height = binaryImage.Rows;
            int width = binaryImage.Cols;
            int centerX = width / 2; // 竖直中线

            int firstWhiteToBlackY = -1;  // 第一次 白 → 黑
            int lastBlackToWhiteY = -1;   // 最后一次 黑 → 白
            int threshold = 5; // 跳变阈值

            // 从上到下扫描中线
            for (int y = 1; y < height; y++)
            {
                // 以黑色底座为参考
                //byte currentPixel = binaryImage.At<byte>(y, centerX);
                //// 检测 白 → 黑 跳变（下降沿）
                //if (previousPixel == 255 && currentPixel == 0 && firstWhiteToBlackY == -1)
                //{
                //    firstWhiteToBlackY = y;
                //}

                //// 检测 黑 → 白 跳变（上升沿），不断更新，保留最后一次
                //if (previousPixel == 0 && currentPixel == 255)
                //{
                //    lastBlackToWhiteY = y;
                //}

                //previousPixel = currentPixel;

                // 以青色夹子为参考
                int delta = binaryImage.At<byte>(y, centerX) - binaryImage.At<byte>(y - 1, centerX);
                // 检测“暗到亮”：上升沿
                if (delta >= threshold && firstWhiteToBlackY == -1)
                {
                    firstWhiteToBlackY = y;
                }

                // 检测“亮到暗”：下降沿，不断更新
                if (delta <= -threshold)
                {
                    lastBlackToWhiteY = y; // 变化发生在 i 位置
                }
            }
            Point p1 = new Point(centerX, firstWhiteToBlackY);
            Point p2 = new Point(centerX, lastBlackToWhiteY);

            return (p1, p2);
        }

        /// <summary>
        /// 在二值化图像的竖直中线上找到第一次黑→白和最后一次白→黑的跳变点，并绘制标记和连线
        /// </summary>
        /// <param name="binaryImage">输入的二值化图像 (单通道, 0 或 255)</param>
        /// <returns>包含两个跳变点的元组：(第一次黑→白, 最后一次白→黑)</returns>
        //public (Point firstBlackToWhite, Point lastWhiteToBlack) MarkTransitionPoints(Mat binaryImage)
        //{
        //    if (binaryImage.Channels() != 1)
        //    {
        //        LogHelper.AddLog(MsgLevel.Exception, "输入图像必须是单通道二值图像。", true);
        //        return (new Point(-1, -1), new Point(-1, -1));
        //    }

        //    // 确保输出是彩色图像以便绘制颜色标记
        //    Mat result = new Mat();
        //    if (binaryImage.Type() == MatType.CV_8UC1)
        //    {
        //        Cv2.CvtColor(binaryImage, result, ColorConversionCodes.GRAY2BGR);
        //    }
        //    else
        //    {
        //        result = binaryImage.Clone();
        //    }

        //    int height = binaryImage.Rows;
        //    int width = binaryImage.Cols;
        //    int centerX = width / 2; // 竖直中线

        //    int firstBlackToWhiteY = -1;  // 第一次 黑 → 白（上升沿）
        //    int lastWhiteToBlackY = -1;   // 最后一次 白 → 黑（下降沿）

        //    byte previousPixel = 0; // 初始化为黑（0），确保第一行也能检测上升沿

        //    // 从上到下扫描中线
        //    for (int y = 0; y < height; y++)
        //    {
        //        byte currentPixel = binaryImage.At<byte>(y, centerX);

        //        // 检测 黑 → 白 跳变（上升沿）：只记录第一次
        //        if (previousPixel == 0 && currentPixel == 255)
        //        {
        //            if (firstBlackToWhiteY == -1)
        //            {
        //                firstBlackToWhiteY = y;
        //            }
        //        }

        //        // 检测 白 → 黑 跳变（下降沿）：不断更新，保留最后一次
        //        if (previousPixel == 255 && currentPixel == 0)
        //        {
        //            lastWhiteToBlackY = y;
        //        }

        //        previousPixel = currentPixel;
        //    }

        //    // 创建返回点
        //    Point p1 = firstBlackToWhiteY != -1 ? new Point(centerX, firstBlackToWhiteY) : new Point(-1, -1);
        //    Point p2 = lastWhiteToBlackY != -1 ? new Point(centerX, lastWhiteToBlackY) : new Point(-1, -1);

        //    // ✅ 绘制标记和连线
        //    if (firstBlackToWhiteY != -1)
        //    {
        //        // 绘制第一个跳变点（绿色圆圈，表示 黑→白）
        //        Cv2.Circle(result, p1, radius: 5, color: Scalar.Lime, thickness: 2);
        //    }

        //    if (lastWhiteToBlackY != -1)
        //    {
        //        // 绘制第二个跳变点（红色圆圈，表示 白→黑）
        //        Cv2.Circle(result, p2, radius: 5, color: Scalar.Red, thickness: 2);
        //    }

        //    // 如果两个点都有效，绘制连接线（黄色虚线）
        //    if (firstBlackToWhiteY != -1 && lastWhiteToBlackY != -1)
        //    {
        //        Cv2.Line(result, p1, p2, color: Scalar.Yellow, thickness: 1, lineType: LineTypes.Link8);
        //    }

        //    // 可选：将结果图像复制回原引用（如果需要可视化调试）
        //    // binaryImage = result; // 视需求而定

        //    // 返回结果
        //    return (p1, p2);
        //}

        /// <summary>
        /// 创建Mark点/极耳模版
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonNewTemplate_Click(object sender, EventArgs e)
        {
            Bitmap preview = null;
            try
            {
                if (src != null && !src.Empty())
                {
                    preview = src.ToBitmap();
                    _formNewTemplate.LoadImages(preview);
                    preview = null;
                }

                _formNewTemplate.ShowDialog();
            }
            finally
            {
                preview?.Dispose();
            }
        }

        /// <summary>
        /// 释放当前节点独占的模板窗体和预览资源。
        /// </summary>
        private void ReleaseImageResources()
        {
            if (System.Threading.Interlocked.Exchange(ref _resourcesReleased, 1) != 0)
                return;

            Shown -= NodeParamFormBatteryEar_Shown;
            imageROIEditControl1.SetImage(null);
            _formNewTemplate.Dispose();
            src = null;
        }
        /// <summary>
        /// 选择图像保存路径
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if(folderBrowserDialog1.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                textBoxImgSavePath.Text = folderBrowserDialog1.SelectedPath;
            }
        }
    }
}
