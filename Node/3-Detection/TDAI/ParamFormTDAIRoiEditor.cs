using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>
    /// AI检测 ROI 绘制窗口，负责把 ShowImageControl 上的动态矩形转换为节点参数。
    /// </summary>
    public partial class ParamFormTDAIRoiEditor : FormBase
    {
        /// <summary>
        /// 最小有效 ROI 宽高，低于该值视为误操作。
        /// </summary>
        private const float MinimumRoiSize = 5F;

        /// <summary>
        /// 获取当前订阅图像的异步委托，由参数窗体负责刷新上游并返回最新图像。
        /// </summary>
        private readonly Func<Task<Bitmap>> _currentImageProvider;

        /// <summary>
        /// 初始化 AI检测 ROI 绘制窗口。
        /// </summary>
        /// <param name="sourceImage">用于绘制 ROI 的源图像。</param>
        /// <param name="roiRegions">已经保存的 ROI 区域。</param>
        /// <param name="currentImageProvider">手动获取当前图像的异步委托。</param>
        public ParamFormTDAIRoiEditor(Bitmap sourceImage, IEnumerable<TDAIRoiRegion> roiRegions, Func<Task<Bitmap>> currentImageProvider)
        {
            InitializeComponent();
            _currentImageProvider = currentImageProvider;
            showImageControl1.EnableRectangleRoiDrawing = true;
            showImageControl1.RoiColor = Color.Lime;
            showImageControl1.SetBuiltInMenuVisible(true);

            if (sourceImage != null)
                showImageControl1.SetImage(new Bitmap(sourceImage));

            LoadRoiRegions(roiRegions);
        }

        /// <summary>
        /// 获取用户确认后的 ROI 区域集合。
        /// </summary>
        public List<TDAIRoiRegion> RoiRegions { get; private set; } = new List<TDAIRoiRegion>();

        /// <summary>
        /// 窗口显示后把焦点交给图像控件，便于 Del 删除选中 ROI。
        /// </summary>
        /// <param name="e">事件参数。</param>
        protected override void OnShown(EventArgs e)
        {
            base.OnShown(e);
            showImageControl1.Focus();
        }

        /// <summary>
        /// 将已保存的 ROI 参数恢复到图像控件。
        /// </summary>
        /// <param name="roiRegions">已保存 ROI 集合。</param>
        private void LoadRoiRegions(IEnumerable<TDAIRoiRegion> roiRegions)
        {
            if (roiRegions == null)
                return;

            int index = 1;
            foreach (TDAIRoiRegion region in roiRegions)
            {
                if (!IsValidRegion(region))
                    continue;

                float phiRad = (float)(region.Angle * Math.PI / 180.0);
                string label = string.IsNullOrWhiteSpace(region.Name) ? "ROI" + index : region.Name;
                showImageControl1.AddRoiRotatedRect(region.CenterY, region.CenterX, phiRad, region.Width / 2F, region.Height / 2F, 1F, Color.Lime, label);
                index++;
            }
        }

        /// <summary>
        /// 清空当前绘制的全部动态 ROI。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void buttonClear_Click(object sender, EventArgs e)
        {
            showImageControl1.ClearDynamicRoi();
            showImageControl1.Focus();
        }

        /// <summary>
        /// 手动刷新并获取当前订阅图像，保留已经绘制的 ROI。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private async void buttonGetCurrentImage_Click(object sender, EventArgs e)
        {
            if (_currentImageProvider == null)
            {
                MessageBoxTD.Show("当前窗口没有配置图像获取入口。");
                return;
            }

            buttonGetCurrentImage.Enabled = false;
            Bitmap bitmap = null;
            try
            {
                bitmap = await _currentImageProvider();
                if (bitmap == null)
                    throw new Exception("当前图像为空。");

                showImageControl1.SetImage(bitmap);
                bitmap = null;
                showImageControl1.ShowFit();
                showImageControl1.Focus();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("获取当前图像失败，原因：" + ex.Message);
            }
            finally
            {
                bitmap?.Dispose();
                buttonGetCurrentImage.Enabled = true;
            }
        }

        /// <summary>
        /// 确认 ROI 绘制结果并关闭窗口。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void buttonConfirm_Click(object sender, EventArgs e)
        {
            RoiRegions = ReadRoiRegions();
            DialogResult = DialogResult.OK;
            Close();
        }

        /// <summary>
        /// 取消本次 ROI 编辑。
        /// </summary>
        /// <param name="sender">事件源。</param>
        /// <param name="e">事件参数。</param>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// 从 ShowImageControl 读取全部动态矩形 ROI。
        /// </summary>
        /// <returns>ROI 参数列表。</returns>
        private List<TDAIRoiRegion> ReadRoiRegions()
        {
            var infos = showImageControl1.GetAllRotatedRectInfos();
            var regions = new List<TDAIRoiRegion>();
            for (int i = 0; i < infos.Count; i++)
            {
                var info = infos[i];
                var region = new TDAIRoiRegion
                {
                    Name = "ROI" + (i + 1),
                    CenterX = info.Column,
                    CenterY = info.Row,
                    Width = info.Length1 * 2F,
                    Height = info.Length2 * 2F,
                    Angle = (float)(info.Phi * 180.0 / Math.PI)
                };

                if (IsValidRegion(region))
                    regions.Add(region);
            }

            if (infos.Count > 0 && regions.Count == 0)
                MessageBoxTD.Show("绘制的 ROI 区域过小，已自动忽略。");

            return regions;
        }

        /// <summary>
        /// 判断 ROI 区域是否达到可保存的最小尺寸。
        /// </summary>
        /// <param name="region">待判断 ROI。</param>
        /// <returns>有效返回 true。</returns>
        private static bool IsValidRegion(TDAIRoiRegion region)
        {
            return region != null &&
                region.Width >= MinimumRoiSize &&
                region.Height >= MinimumRoiSize;
        }
    }
}
