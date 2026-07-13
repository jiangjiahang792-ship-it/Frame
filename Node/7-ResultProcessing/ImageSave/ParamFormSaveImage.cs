using Logger;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System.Linq;
using TDJS_Vision.Diagnostics;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageSave
{
    public partial class ParamFormImageSave : FormBase, INodeParamForm
    {
        /// <summary>
        /// 节点参数
        /// </summary>
        public INodeParam Params { get; set; }

        /// <summary>
        /// OK/NG 订阅结果解析器，用于隔离 AI 结果和多条件布尔结果的类型差异。
        /// </summary>
        private readonly IImageSaveJudgmentResolver _imageSaveJudgmentResolver;

        /// <summary>
        /// 使用默认保存判定结果适配器初始化参数窗体。
        /// </summary>
        public ParamFormImageSave()
            : this(new ImageSaveJudgmentResolver())
        {
        }

        /// <summary>
        /// 使用指定保存判定结果解析器初始化参数窗体。
        /// </summary>
        /// <param name="imageSaveJudgmentResolver">OK/NG 订阅结果解析器。</param>
        internal ParamFormImageSave(IImageSaveJudgmentResolver imageSaveJudgmentResolver)
        {
            _imageSaveJudgmentResolver = imageSaveJudgmentResolver ??
                throw new ArgumentNullException(nameof(imageSaveJudgmentResolver));
            InitializeComponent();
        }

        /// <summary>
        /// 用于节点参数界面需要订阅结果的情况调用
        /// </summary>
        /// <param name="node"></param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscriptionImg2Save.Init(node);
            nodeSubscriptionBarCode.Init(node);
            nodeSubscriptionAiRes.Init(node);
        }

        /// <summary>
        /// 存图路径选择
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            if (folderBrowserDialog1.ShowDialog() == DialogResult.OK)
            {
                this.textBox1.Text = folderBrowserDialog1.SelectedPath;
            }
        }

        /// <summary>
        /// 是否启用读码
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxBarCode_CheckedChanged(object sender, EventArgs e)
        {
            panel4.Enabled = checkBoxBarCode.Checked;
        }

        /// <summary>
        /// 是否区分NG存图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxSaveWithNG_CheckedChanged(object sender, EventArgs e)
        {
            panel6.Enabled = checkBoxSaveWithNG.Checked;
            radioButtonOKAndNG.Enabled = checkBoxSaveWithNG.Checked;
            radioButtonOK.Enabled = checkBoxSaveWithNG.Checked;
            radioButtonNG.Enabled = checkBoxSaveWithNG.Checked;
        }

        /// <summary>
        /// 是否区分早晚班存图
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBoxDayNight_CheckedChanged(object sender, EventArgs e)
        {
            panel7.Enabled = checkBoxDayNight.Checked;
        }

        /// <summary>
        /// 图片是否压缩
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            panel9.Enabled = checkBoxCompress.Checked;
        }

        /// <summary>
        /// 获取保存图片订阅控件的原始输出图像引用，供保存节点快速入队使用。
        /// </summary>
        /// <returns>订阅到的输出图像。</returns>
        public OutputImage GetOutputImageForSave()
        {
            OutputImage outputImage = nodeSubscriptionImg2Save.GetValue<OutputImage>();
            if (outputImage == null || outputImage.Bitmaps == null || outputImage.Bitmaps.Count == 0 || !OutputImage.HasValidImage(outputImage.Bitmaps[0]))
                throw new Exception("订阅的图片对象为空！");

            return outputImage;
        }

        /// <summary>
        /// 获取图片订阅控件的图像
        /// </summary>
        /// <returns></returns>
        public Bitmap GetImage()
        {
            Bitmap bitmap = null;
            bool ownershipReturned = false;
            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                MemorySnapshot beforeSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
                OutputImage outputImage = nodeSubscriptionImg2Save.GetValue<OutputImage>();
                long afterSubscription = stopwatch.ElapsedMilliseconds;
                if (outputImage == null || outputImage.Bitmaps == null || outputImage.Bitmaps.Count == 0 || !OutputImage.HasValidImage(outputImage.Bitmaps[0]))
                    throw new Exception("订阅的图片对象为空！");

                Mat sourceImage = outputImage.Bitmaps[0];
                bitmap = sourceImage.ToBitmap();
                long afterToBitmap = stopwatch.ElapsedMilliseconds;
                bool hasDrawableDisplayResult = HasDrawableDisplayResult(outputImage.DisplayResult);
                if (hasDrawableDisplayResult)
                {
                    bitmap = EnsureDrawableBitmap(bitmap);
                    long afterEnsureDrawable = stopwatch.ElapsedMilliseconds;
                    ShowImageControl.DrawDisplayResultToBitmap(bitmap, outputImage.DisplayResult);
                    long afterDrawDisplayResult = stopwatch.ElapsedMilliseconds;
                    LogImageSnapshot(outputImage, bitmap, beforeSnapshot, afterSubscription, afterToBitmap, afterEnsureDrawable, afterDrawDisplayResult, true);
                }
                else
                {
                    long afterNoDraw = stopwatch.ElapsedMilliseconds;
                    LogImageSnapshot(outputImage, bitmap, beforeSnapshot, afterSubscription, afterToBitmap, afterNoDraw, afterNoDraw, false);
                }

                ownershipReturned = true;
                return bitmap;
            }
            catch
            {
                if (!ownershipReturned)
                    bitmap?.Dispose();

                throw;
            }
        }

        /// <summary>
        /// 输出保存图像取图阶段的内存诊断，定位Mat转Bitmap或标注写入是否造成资源上涨。
        /// </summary>
        /// <param name="outputImage">订阅到的输出图像。</param>
        /// <param name="bitmap">生成的待保存位图。</param>
        /// <param name="beforeSnapshot">取图前内存快照。</param>
        /// <param name="afterSubscription">订阅读取后的耗时。</param>
        /// <param name="afterToBitmap">Mat转Bitmap后的耗时。</param>
        /// <param name="afterEnsureDrawable">确保可绘制位图后的耗时。</param>
        /// <param name="afterDrawDisplayResult">写入显示结果后的耗时。</param>
        /// <param name="hasDrawableDisplayResult">是否存在需要写入保存图的显示结果。</param>
        private static void LogImageSnapshot(OutputImage outputImage, Bitmap bitmap, MemorySnapshot beforeSnapshot, long afterSubscription, long afterToBitmap, long afterEnsureDrawable, long afterDrawDisplayResult, bool hasDrawableDisplayResult)
        {
            MemorySnapshot afterSnapshot = PerformanceSpikeDiagnostics.CaptureMemorySnapshot();
            LogHelper.AddLog(
                MsgLevel.Debug,
                $"【内存诊断-保存图像取图】输出图像={PerformanceSpikeDiagnostics.GetOutputImageText(outputImage)}；待保存Bitmap={PerformanceSpikeDiagnostics.GetBitmapText(bitmap)}；有标注={hasDrawableDisplayResult}；订阅读取={afterSubscription}ms；ToBitmap={afterToBitmap - afterSubscription}ms；确保可绘制={afterEnsureDrawable - afterToBitmap}ms；写入标注={afterDrawDisplayResult - afterEnsureDrawable}ms；总耗时={afterDrawDisplayResult}ms；私有内存变化={afterSnapshot.PrivateMemoryMb - beforeSnapshot.PrivateMemoryMb:F1}MB；GDI变化={afterSnapshot.GdiObjectCount - beforeSnapshot.GdiObjectCount}；前={beforeSnapshot.ToLogText()}；后={afterSnapshot.ToLogText()}",
                true);
        }

        /// <summary>
        /// 确保位图支持 GDI+ 写入绘制，索引色灰度图需要先转为普通 RGB 图。
        /// </summary>
        /// <param name="source">待检查的位图。</param>
        /// <returns>可安全写入线框和文本的位图。</returns>
        internal static Bitmap EnsureDrawableBitmap(Bitmap source)
        {
            if (source == null)
                return null;

            bool isIndexed = (source.PixelFormat & PixelFormat.Indexed) == PixelFormat.Indexed;
            if (!isIndexed)
                return source;

            Bitmap drawable = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(drawable))
                graphics.DrawImage(source, 0, 0, source.Width, source.Height);

            source.Dispose();
            return drawable;
        }

        /// <summary>
        /// 判断图像输出中是否带有需要写入保存图的显示叠加层。
        /// </summary>
        /// <param name="displayResult">显示叠加结果。</param>
        /// <returns>存在可绘制的线框、轮廓或文本时返回 true。</returns>
        internal static bool HasDrawableDisplayResult(AlgorithmResult displayResult)
        {
            if (displayResult == null)
                return false;

            return HasAny(displayResult.Rects)
                || (displayResult.RectsNgMap != null && displayResult.RectsNgMap.Values.Any(HasAny))
                || HasAny(displayResult.Texts)
                || HasAny(displayResult.Lines)
                || HasAny(displayResult.Circles)
                || HasAny(displayResult.Arcs)
                || HasAny(displayResult.Ellipses)
                || HasAny(displayResult.Contours);
        }

        /// <summary>
        /// 判断集合中是否至少有一个非空显示元素。
        /// </summary>
        /// <typeparam name="T">显示元素类型。</typeparam>
        /// <param name="items">显示元素集合。</param>
        /// <returns>存在非空元素时返回 true。</returns>
        private static bool HasAny<T>(IEnumerable<T> items) where T : class
        {
            return items != null && items.Any(item => item != null);
        }

        /// <summary>
        /// 获取二维码订阅控件的的值
        /// </summary>
        /// <returns></returns>
        public string GetBarCode()
        {
            try
            {
                return nodeSubscriptionBarCode.GetValue<string>();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 获取AI结果订阅控件的值
        /// </summary>
        public AlgorithmResult GetAiResult()
        {
            try
            {
                return nodeSubscriptionAiRes.GetValue<AlgorithmResult>();
            }
            catch (Exception)
            {
                throw;
            }
        }

        /// <summary>
        /// 获取保存图像节点使用的统一 OK/NG 判定结果。
        /// </summary>
        /// <returns>兼容 AI 算法结果和多条件布尔结果的统一判定对象。</returns>
        public ImageSaveJudgment GetImageSaveJudgment()
        {
            object subscriptionValue = nodeSubscriptionAiRes.GetValue<object>();
            return _imageSaveJudgmentResolver.Resolve(subscriptionValue);
        }

        /// <summary>
        /// 保存参数
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(textBox1.Text))
            {
                MessageBoxTD.Show("未选择存图路径");
                LogHelper.AddLog(MsgLevel.Fatal, "未选择存图路径", true);
                return;
            }
            if(Convert.ToUInt16(this.numericUpDown1.Value) > 100 && Convert.ToUInt16(this.numericUpDown1.Value) < 0)
            {
                MessageBoxTD.Show("压缩阈值范围不合法！");
                LogHelper.AddLog(MsgLevel.Fatal, "压缩阈值范围不合法！", true);
                return;
            }

            NodeParamSaveImage savaImageParam = new NodeParamSaveImage();
            savaImageParam.SavePath = textBox1.Text;
            savaImageParam.ImgSubText1 = nodeSubscriptionImg2Save.GetText1();
            savaImageParam.ImgSubText2 = nodeSubscriptionImg2Save.GetText2();
            savaImageParam.AiResSubText1 = nodeSubscriptionAiRes.GetText1();
            savaImageParam.AiResSubText2 = nodeSubscriptionAiRes.GetText2();
            savaImageParam.BarCodeSubText1 = nodeSubscriptionBarCode.GetText1();
            savaImageParam.BarCodeSubText2 = nodeSubscriptionBarCode.GetText2();

            // 二维码命名
            savaImageParam.IsBarCode = checkBoxBarCode.Checked;
            //是否需要区分OkNg子目录
            savaImageParam.NeedOkNg = checkBoxSaveWithNG.Checked;
            // 早晚班存图
            if (checkBoxDayNight.Checked)
            {
                savaImageParam.IsDayNight = true;
                savaImageParam.DayDataTime = dateTimePicker1.Value;
                savaImageParam.NightDataTime = dateTimePicker2.Value;
            }
            else
                savaImageParam.IsDayNight = false;
            // 图片压缩
            if (checkBoxCompress.Checked)
            {
                savaImageParam.NeedCompress = true;
                savaImageParam.CompressValue = Convert.ToUInt16(this.numericUpDown1.Value);
            }
            else
                savaImageParam.NeedCompress = false;

            // 保存什么图
            savaImageParam.ImageTypeToSave = radioButtonOKAndNG.Checked ? ImageTypeToSave.OkAndNg : 
                                             (radioButtonOK.Checked ? ImageTypeToSave.OnlyOk : ImageTypeToSave.OnlyNg);

            Params = savaImageParam;
            Hide();
        }
        /// <summary>
        /// 反序列化需要设置参数给回界面
        /// </summary>
        /// <exception cref="NotImplementedException"></exception>
        public void SetParam2Form()
        {
            if (Params is NodeParamSaveImage param)
            {
                textBox1.Text = param.SavePath;
                nodeSubscriptionImg2Save.SetText(param.ImgSubText1, param.ImgSubText2);
                checkBoxBarCode.Checked = param.IsBarCode;
                checkBoxSaveWithNG.Checked = param.NeedOkNg;
                checkBoxDayNight.Checked = param.IsDayNight;
                checkBoxCompress.Checked = param.NeedCompress;
                nodeSubscriptionAiRes.SetText(param.AiResSubText1, param.AiResSubText2);
                dateTimePicker1.Value = (param.DayDataTime < dateTimePicker1.MinDate || param.DayDataTime > dateTimePicker1.MaxDate) ? 
                                        dateTimePicker1.MinDate : param.DayDataTime;
                dateTimePicker2.Value = (param.NightDataTime < dateTimePicker2.MinDate || param.NightDataTime > dateTimePicker2.MaxDate) ?
                                        dateTimePicker2.MinDate : param.NightDataTime;
                numericUpDown1.Value = param.CompressValue;
                nodeSubscriptionBarCode.SetText(param.BarCodeSubText1, param.BarCodeSubText2);
                switch (param.ImageTypeToSave)
                {
                    case ImageTypeToSave.OkAndNg:
                        radioButtonOKAndNG.Checked = true;
                        break;
                    case ImageTypeToSave.OnlyOk:
                        radioButtonOK.Checked = true;
                        break;
                    case ImageTypeToSave.OnlyNg:
                        radioButtonNG.Checked = true;
                        break;
                    default:
                        break;
                }

            }
        }
    }
}
