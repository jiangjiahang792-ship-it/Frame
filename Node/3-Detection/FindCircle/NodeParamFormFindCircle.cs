using OpenCvSharp.Extensions;
using OpenCvSharp;
using System;
using System.Drawing;
using System.Windows.Forms;
using System.Linq;
using Logger;
using Point = OpenCvSharp.Point;
using System.Collections.Generic;
using Sunny.UI;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.FindCircle
{
    public partial class NodeParamFormFindCircle : FormBase, INodeParamForm
    {
        private Process process;//所属流程
        private NodeBase node;//所属节点
        private CircleSelection curLineSelection; // 当前选择的圆
        private int curLineSelectionID;// 当前选择的圆的ID

        public NodeParamFormFindCircle(Process process, NodeBase nodeBase)
        {
            InitializeComponent();
            this.process = process;
            this.node = nodeBase;
            comboBox1.SelectedIndex = 0;
            imageROIEditControl1.SetROIType2Draw(Forms.ShapeDraw.ROIType.Circle);
            Shown += NodeParamFormFindLine_Shown;
            SetToolTips();
        }

        private void SetToolTips()
        {
            toolTip1.SetToolTip(label3, "减少噪点,仅限奇数,影响边缘检测图像");
            toolTip1.SetToolTip(label1, "越小边缘噪点越多,影响边缘检测图像");
            toolTip1.SetToolTip(label2, "越大边缘噪点越少,影响边缘检测图像");
            toolTip1.SetToolTip(label6, "越大则边缘检测变得更加严(检测到的圆更少)");
            toolTip1.SetToolTip(label5, "检测到的圆更少更加精准(检测到的圆更少)");
            toolTip1.SetToolTip(label4, "启用时边缘检测更精准但会增加耗时");
            toolTip1.SetToolTip(label7, "如果设置得太小,多个邻近的圆可能会被错误地检测为一个圆");
        }

        private void NodeParamFormFindLine_Shown(object sender, EventArgs e)
        {
            UpdataImage();
        }

        public INodeParam Params { get; set; }

        public void SetParam2Form()
        {
            if (Params is NodeParamFindCircle param)
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                checkBoxMoreParams.Checked = param.MoreParamsEnable;
                checkBoxOKEnable.Checked = param.OKEnable;
                textBoxOKMinR.Text = param.OKMinR.ToString();
                textBoxOKMaxR.Text = param.OKMaxR.ToString();
                textBoxBlurSize.Text = param.GaussianBlur.ToString();
                textBoxThreshold1.Text = param.Threshold1.ToString();
                textBoxThreshold2.Text = param.Threshold2.ToString();
                checkBoxUseL2.Checked = param.IsOpenL2;
                textBox1.Text = param.param1.ToString();
                textBox2.Text = param.param2.ToString();
                textBoxCount.Text = param.Count.ToString();
                textBoxMinR.Text = param.MinLength.ToString();
                textBoxMaxR.Text = param.MaxDistance.ToString();
                switch (param.CircleSelection)
                {
                    case CircleSelection.Largest:
                        comboBox1.SelectedIndex = 0;
                        break;
                    case CircleSelection.Smallest:
                        comboBox1.SelectedIndex = 1;
                        break;
                    case CircleSelection.Topmost:
                        comboBox1.SelectedIndex = 2;
                        break;
                    case CircleSelection.Bottommost:
                        comboBox1.SelectedIndex = 3;
                        break;
                    case CircleSelection.Leftmost:
                        comboBox1.SelectedIndex = 4;
                        break;
                    case CircleSelection.Rightmost:
                        comboBox1.SelectedIndex = 5;
                        break;
                    default:
                        break;
                }
                //还原ROI
                imageROIEditControl1.SetROIs(param.ROIs);
            }
        }

        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        /// <summary>
        /// 点击更多参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            tableLayoutPanel2.Visible = checkBoxMoreParams.Checked;
        }

        /// <summary>
        /// 点击刷新图像
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private async void button3_Click(object sender, EventArgs e)
        {
            await process.RunForUpdateImages(node);
            UpdataImage();
        }

        /// <summary>
        /// 获取订阅的图像设置到显示控件中
        /// </summary>
        public void UpdataImage()
        {
            Bitmap bitmap = null;
            try
            {
                bitmap = nodeSubscription1.GetValue<OutputImage>().Bitmaps[0].ToBitmap();
            }
            catch (Exception)
            {
                bitmap = null;
            }
            imageROIEditControl1.SetImage(bitmap);
        }

        public OutputImage GetOutputImage()
        {
            return nodeSubscription1.GetValue<OutputImage>();
        }

        /// <summary>
        /// 点击执行
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRun_Click(object sender, EventArgs e)
        {
            try
            {
                DetectCircle();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"检测异常：{ex.Message}");
                LogHelper.AddLog(MsgLevel.Exception, $"节点({node.ID}.{node.Name})检测异常，原因：{ex.Message}", true);
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
                NodeParamFindCircle nodeParamFindCircle = new NodeParamFindCircle();
                nodeParamFindCircle.Text1 = nodeSubscription1.GetText1();
                nodeParamFindCircle.Text2 = nodeSubscription1.GetText2();
                nodeParamFindCircle.MoreParamsEnable = checkBoxMoreParams.Checked;
                nodeParamFindCircle.OKEnable = checkBoxOKEnable.Checked;
                nodeParamFindCircle.OKMinR = double.Parse(textBoxOKMinR.Text);
                nodeParamFindCircle.OKMaxR = double.Parse(textBoxOKMaxR.Text);
                nodeParamFindCircle.GaussianBlur = int.Parse(textBoxBlurSize.Text);
                nodeParamFindCircle.Threshold1 = double.Parse(textBoxThreshold1.Text);
                nodeParamFindCircle.Threshold2 = double.Parse(textBoxThreshold2.Text);
                nodeParamFindCircle.param1 = int.Parse(textBox1.Text);
                nodeParamFindCircle.param2 = int.Parse(textBox2.Text);
                nodeParamFindCircle.IsOpenL2 = checkBoxUseL2.Checked;
                nodeParamFindCircle.Count = int.Parse(textBoxCount.Text);
                nodeParamFindCircle.MinLength = int.Parse(textBoxMinR.Text);
                nodeParamFindCircle.MaxDistance = int.Parse(textBoxMaxR.Text);
                nodeParamFindCircle.CircleSelection = curLineSelection;
                nodeParamFindCircle.ROIs = imageROIEditControl1.GetROIs();
                Params = nodeParamFindCircle;
            }
            catch (Exception)
            {
                MessageBoxTD.Show("参数设置异常，请检查参数设置是否合理！");
                return false;
            }
            return true;
        }

        /// <summary>
        /// 检测圆
        /// </summary>
        public (CircleSegment circle, bool succeeded) DetectCircle()
        {
            CircleSegment Circle = new CircleSegment();
            List<Mat> roiImages = null;
            try
            {
                // 更新输入图像和获取ROI图像
                ReplacePictureBoxImage(pictureBoxCanny, null);
                ReplacePictureBoxImage(pictureBoxResult1, null);
                ReplacePictureBoxImage(pictureBoxResult2, null);
                UpdataImage();
                roiImages = imageROIEditControl1.GetROIImages();
                if (roiImages == null || roiImages.Count == 0 || !OutputImage.HasValidImage(roiImages[0]))
                    throw new Exception("圆查找没有取得有效ROI图像！");

                Mat bitmap = roiImages[0];
                if (bitmap.Channels() != 1)
                    Cv2.CvtColor(bitmap, bitmap, ColorConversionCodes.BGR2GRAY);

                using (Mat blurred = new Mat())
                using (Mat edges = new Mat())
                {
                    Cv2.GaussianBlur(bitmap, blurred, new OpenCvSharp.Size(int.Parse(textBoxBlurSize.Text), int.Parse(textBoxBlurSize.Text)), 0);
                    Cv2.Canny(blurred, edges, double.Parse(textBoxThreshold1.Text), double.Parse(textBoxThreshold2.Text), 3, checkBoxUseL2.Checked);
                    ReplacePictureBoxImage(pictureBoxCanny, BitmapConverter.ToBitmap(edges));

                /*
                 * 使用霍夫圆变换检测图像中的圆。参数解释：
                    image：输入图像，通常是灰度图像。
                    method：检测方法，通常使用 HoughModes.Gradient。
                    dp：累加器分辨率与输入图像分辨率的反比。例如，如果 dp=1，累加器和输入图像具有相同的分辨率；如果 dp=2，累加器的宽度和高度是输入图像的一半。
                    minDist：检测到的圆心之间的最小距离。如果设置得太小，多个邻近的圆可能会被错误地检测为一个圆。
                    param1：传递给 Canny 边缘检测器的高阈值，低阈值是高阈值的一半。
                    param2：累加器阈值，用于检测阶段。阈值越小，检测到的圆越多（但可能包括错误的圆）；阈值越大，检测到的圆越少，但更准确。
                    minRadius：检测圆的最小半径。
                    maxRadius：检测圆的最大半径。
                 */
                    var circles = Cv2.HoughCircles(edges, HoughModes.Gradient, 1, int.Parse(textBoxCount.Text), param1: int.Parse(textBox1.Text), param2: int.Parse(textBox2.Text),
                                                minRadius: int.Parse(textBoxMinR.Text), maxRadius: int.Parse(textBoxMaxR.Text));

                    Random random = new Random();
                    int index = 1;
                    HashSet<Scalar> usedColors = new HashSet<Scalar>();
                    double minColorDistance = 5;
                    Dictionary<CircleSegment, Scalar> keyValuePairs1 = new Dictionary<CircleSegment, Scalar>();
                    Dictionary<int, CircleSegment> keyValuePairs2 = new Dictionary<int, CircleSegment>();

                    for (int i = comboBox1.Items.Count - 1; i >= 6; i--)
                        comboBox1.Items.RemoveAt(i);

                    using (Mat result = bitmap.Clone())
                    {
                        Cv2.CvtColor(result, result, ColorConversionCodes.BayerBG2BGR);
                        foreach (var circle in circles)
                        {
                            var center = new Point((int)circle.Center.X, (int)circle.Center.Y);
                            var radius = (int)circle.Radius;
                            Scalar randomColor;
                            do
                            {
                                randomColor = new Scalar(GetDarkColorValue(random), GetDarkColorValue(random), GetDarkColorValue(random));
                            } while (randomColor == Scalar.Red || usedColors.Any(usedColor => GetColorDistance(randomColor, usedColor) < minColorDistance));

                            usedColors.Add(randomColor);
                            Cv2.Circle(result, center, radius, randomColor, 1);
                            Cv2.Circle(result, center, 3, randomColor, -1);
                            Cv2.PutText(result, index.ToString(), new Point(center.X + 1, center.Y), HersheyFonts.Italic, 0.5, randomColor);
                            keyValuePairs1.Add(circle, randomColor);
                            keyValuePairs2.Add(index, circle);
                            comboBox1.Items.Add($"选择ID为：{index++}的圆");
                        }
                        Cv2.PutText(result, $"Count:{circles.Count()}", new Point(40, 40), HersheyFonts.Italic, 0.5, Scalar.Red);
                        ReplacePictureBoxImage(pictureBoxResult1, BitmapConverter.ToBitmap(result));
                    }

                    var point = imageROIEditControl1.GetImageROIRects()[0].Location;
                    using (Mat selectedResult = bitmap.Clone())
                    {
                        Cv2.CvtColor(selectedResult, selectedResult, ColorConversionCodes.BayerBG2BGR);
                        var singleCircle = new CircleSegment();
                        if (curLineSelectionID > 6)
                        {
                            comboBox1.SelectedIndexChanged -= comboBox1_SelectedIndexChanged;
                            try
                            {
                                if (curLineSelectionID > comboBox1.Items.Count)
                                {
                                    comboBox1.SelectedIndex = 0;
                                    singleCircle = CircleMerger.MergeCircles(circles.ToList(), curLineSelection);
                                }
                                else
                                {
                                    comboBox1.SelectedIndex = curLineSelectionID - 1;
                                    singleCircle = keyValuePairs2[curLineSelectionID - 6];
                                }
                            }
                            finally
                            {
                                comboBox1.SelectedIndexChanged += comboBox1_SelectedIndexChanged;
                            }
                        }
                        else
                        {
                            singleCircle = CircleMerger.MergeCircles(circles.ToList(), curLineSelection);
                        }

                        Cv2.Circle(selectedResult, (Point)singleCircle.Center, (int)singleCircle.Radius, keyValuePairs1[singleCircle], 2);
                        Cv2.Circle(selectedResult, (Point)singleCircle.Center, 3, keyValuePairs1[singleCircle], -1);
                        Cv2.PutText(selectedResult, keyValuePairs2.FirstOrDefault(x => x.Value == singleCircle).Key.ToString(), new Point((int)singleCircle.Center.X + 1, singleCircle.Center.Y), HersheyFonts.Italic, 0.5, keyValuePairs1[singleCircle]);
                        Cv2.PutText(selectedResult, $"Radius: {singleCircle.Radius.ToString("F2")} px", new Point(40, 40), HersheyFonts.Italic, 0.5, Scalar.Red);
                        ReplacePictureBoxImage(pictureBoxResult2, BitmapConverter.ToBitmap(selectedResult));
                        Circle = singleCircle;
                    }
                }
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Exception, $"检测圆失败，原因：{ex.Message}", true);
                return (Circle, false);
            }
            finally
            {
                if (roiImages != null)
                {
                    foreach (Mat roiImage in roiImages)
                        roiImage?.Dispose();
                }
            }

            return (Circle, true);
        }

        /// <summary>
        /// 替换预览框图像并释放上一张位图，避免重复执行时累积GDI资源。
        /// </summary>
        /// <param name="pictureBox">目标预览框。</param>
        /// <param name="image">由预览框接管的新图像。</param>
        private static void ReplacePictureBoxImage(PictureBox pictureBox, Image image)
        {
            Image oldImage = pictureBox.Image;
            pictureBox.Image = image;
            if (!ReferenceEquals(oldImage, image))
                oldImage?.Dispose();
        }
        private static byte GetDarkColorValue(Random rand)
        {
            // 随机选择一个区间
            return rand.Next(2) == 0 ? (byte)rand.Next(0, 11) : (byte)rand.Next(245, 256);
        }

        private void comboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (!(comboBox1.SelectedIndex > 6))
            {
                curLineSelection = comboBox1.SelectedIndex == 0 ? CircleSelection.Largest :
                   comboBox1.SelectedIndex == 1 ? CircleSelection.Smallest :
                   comboBox1.SelectedIndex == 2 ? CircleSelection.Topmost :
                   comboBox1.SelectedIndex == 3 ? CircleSelection.Bottommost :
                   comboBox1.SelectedIndex == 4 ? CircleSelection.Leftmost : CircleSelection.Rightmost;
            }
            curLineSelectionID = comboBox1.SelectedIndex + 1;
        }

        private void checkBoxOKEnable_CheckedChanged(object sender, EventArgs e)
        {
            textBoxOKMinR.Enabled = checkBoxOKEnable.Checked;
            textBoxOKMaxR.Enabled = checkBoxOKEnable.Checked;
        }

        /// <summary>
        /// 计算两种颜色之间的差异
        /// </summary>
        /// <param name="color1"></param>
        /// <param name="color2"></param>
        /// <returns></returns>
        private double GetColorDistance(Scalar color1, Scalar color2)
        {
            // 计算RGB颜色之间的欧几里得距离
            return Math.Sqrt(Math.Pow(color1.Val0 - color2.Val0, 2) + Math.Pow(color1.Val1 - color2.Val1, 2) + Math.Pow(color1.Val2 - color2.Val2, 2));
        }
    }
}
