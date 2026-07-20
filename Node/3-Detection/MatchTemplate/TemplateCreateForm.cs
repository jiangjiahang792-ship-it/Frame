using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    internal partial class TemplateCreateForm : Form
    {
        private Bitmap sourceImage;
        private Bitmap templateImage;
        private Bitmap originalTemplateImage;
        private Bitmap templateEraseMask;
        private float templateRegionCenterX;
        private float templateRegionCenterY;
        private float templateRegionWidth;
        private float templateRegionHeight;
        private float templateRegionAngle;
        private bool isPaintingTemplate;

        public TemplateCreateForm(Bitmap source, Bitmap initialTemplate, Bitmap initialEraseMask, string sourceName)
        {
            InitializeComponent();
            Shown += TemplateCreateForm_Shown;
            buttonConfirmRoi.Visible = false;
            TemplateSourceName = string.IsNullOrWhiteSpace(sourceName) ? "当前ROI模板" : sourceName;

            if (source != null)
            {
                sourceImage = (Bitmap)source.Clone();
                showImageControlSource.SetImage((Bitmap)sourceImage.Clone());
                showImageControlSource.ShowFit();
            }

            if (initialTemplate != null)
                SetTemplateImage((Bitmap)initialTemplate.Clone(), true, initialEraseMask);

            UpdateSourceControls();
        }

        private void TemplateCreateForm_Shown(object sender, EventArgs e)
        {
            if (sourceImage == null)
                return;

            showImageControlSource.SetImage((Bitmap)sourceImage.Clone());
            showImageControlSource.ShowFit();
            showImageControlSource.Refresh();
        }

        public string TemplateSourceName { get; private set; }

        public Bitmap GetTemplateImage()
        {
            if (templateImage == null)
                return null;

            return (Bitmap)templateImage.Clone();
        }

        public Bitmap GetTemplateEraseMask()
        {
            if (!FastTemplateMatcher.HasMaskPixels(templateEraseMask))
                return null;

            return (Bitmap)templateEraseMask.Clone();
        }

        private void UpdateSourceControls()
        {
            bool hasSource = sourceImage != null;
            buttonGenerateTemplate.Enabled = hasSource;
            buttonResetRoi.Enabled = hasSource;
            buttonConfirmRoi.Enabled = false;
            if (!hasSource)
                labelSourceStatus.Text = "当前没有源图像，只能编辑已有模板。";
            else if (showImageControlSource.GetAllDynamicRects().Count > 0)
                labelSourceStatus.Text = "模板ROI编辑中，调整完成后点击“创建模板”。";
            else
                labelSourceStatus.Text = "点击“绘制ROI”后调整模板区域。";
        }

        private void ResetTemplateRoi()
        {
            if (sourceImage == null)
                return;

            templateRegionCenterX = sourceImage.Width / 2f;
            templateRegionCenterY = sourceImage.Height / 2f;
            templateRegionWidth = Math.Max(20, sourceImage.Width / 3f);
            templateRegionHeight = Math.Max(20, sourceImage.Height / 3f);
            templateRegionAngle = 0;
            showImageControlSource.ClearDynamicRoi();
            showImageControlSource.AddDynamicRect(
                templateRegionCenterX,
                templateRegionCenterY,
                templateRegionWidth,
                templateRegionHeight,
                Color.Lime,
                "模板区域");
            UpdateSourceControls();
        }

        private void buttonResetRoi_Click(object sender, EventArgs e)
        {
            ResetTemplateRoi();
        }

        private void buttonConfirmRoi_Click(object sender, EventArgs e)
        {
            try
            {
                ConfirmTemplateRoi();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(ex.Message);
            }
        }

        private void buttonGenerateTemplate_Click(object sender, EventArgs e)
        {
            try
            {
                if (sourceImage == null)
                    throw new InvalidOperationException("源图像不能为空。");

                CaptureTemplateRoi();

                Bitmap template = FastTemplateMatcher.CreateTemplateImage(
                    sourceImage,
                    templateRegionCenterX,
                    templateRegionCenterY,
                    templateRegionWidth,
                    templateRegionHeight,
                    templateRegionAngle);
                SetTemplateImage(template, true, null);
                TemplateSourceName = "当前ROI模板";
                showImageControlSource.ClearDynamicRoi();
                UpdateSourceControls();
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"创建模板失败，原因：{ex.Message}");
            }
        }

        private void ConfirmTemplateRoi()
        {
            if (sourceImage == null)
                throw new InvalidOperationException("源图像不能为空。");

            var rects = showImageControlSource.GetAllDynamicRects();
            var roi = rects.Count > 0 ? rects[0] : null;
            if (roi == null)
                throw new InvalidOperationException("请先绘制模板区域。");

            templateRegionCenterX = roi.CX;
            templateRegionCenterY = roi.CY;
            templateRegionWidth = Math.Max(1, roi.W);
            templateRegionHeight = Math.Max(1, roi.H);
            templateRegionAngle = roi.Phi;
            showImageControlSource.ClearDynamicRoi();
            UpdateSourceControls();
        }

        private void CaptureTemplateRoi()
        {
            var rects = showImageControlSource.GetAllDynamicRects();
            var roi = rects.Count > 0 ? rects[0] : null;
            if (roi == null)
                throw new InvalidOperationException("请先点击“绘制ROI”并调整模板区域。");

            templateRegionCenterX = roi.CX;
            templateRegionCenterY = roi.CY;
            templateRegionWidth = Math.Max(1, roi.W);
            templateRegionHeight = Math.Max(1, roi.H);
            templateRegionAngle = roi.Phi;
        }

        private void SetTemplateImage(Bitmap image, bool resetOriginal, Bitmap eraseMask)
        {
            if (resetOriginal)
            {
                originalTemplateImage?.Dispose();
                originalTemplateImage = image == null ? null : FastTemplateMatcher.To24Bpp(image);
                templateEraseMask?.Dispose();
                templateEraseMask = NormalizeTemplateEraseMask(eraseMask, originalTemplateImage?.Size ?? Size.Empty);
            }

            image?.Dispose();
            RefreshEditedTemplateFromOriginal();
            UpdateTemplatePreview();
        }

        private void RefreshEditedTemplateFromOriginal()
        {
            templateImage?.Dispose();
            templateImage = originalTemplateImage == null
                ? null
                : FastTemplateMatcher.ApplyTemplateEraseMask(originalTemplateImage, templateEraseMask);
        }

        private static Bitmap NormalizeTemplateEraseMask(Bitmap eraseMask, Size templateSize)
        {
            if (templateSize.Width <= 0 || templateSize.Height <= 0)
                return null;

            if (eraseMask == null)
                return FastTemplateMatcher.CreateEmptyMask(templateSize);

            if (eraseMask.Width == templateSize.Width && eraseMask.Height == templateSize.Height)
                return FastTemplateMatcher.To24Bpp(eraseMask);

            var resized = new Bitmap(templateSize.Width, templateSize.Height);
            resized.SetResolution(96, 96);
            using (var g = Graphics.FromImage(resized))
            {
                g.InterpolationMode = InterpolationMode.NearestNeighbor;
                g.DrawImage(eraseMask, new Rectangle(0, 0, resized.Width, resized.Height), 0, 0, eraseMask.Width, eraseMask.Height, GraphicsUnit.Pixel);
            }
            return resized;
        }

        private void UpdateTemplatePreview()
        {
            Image oldImage = pictureBoxTemplate.Image;
            pictureBoxTemplate.Image = null;
            oldImage?.Dispose();

            int contourCount = 0;
            string previewError = null;
            if (templateImage != null)
            {
                try
                {
                    var contour = FastTemplateMatcher.BuildTemplateContour(templateImage, templateEraseMask);
                    contourCount = contour.Count;
                    pictureBoxTemplate.Image = FastTemplateMatcher.DrawTemplateContourPreview(templateImage, contour);
                }
                catch (Exception ex)
                {
                    previewError = ex.Message;
                    try
                    {
                        pictureBoxTemplate.Image = FastTemplateMatcher.To24Bpp(templateImage);
                    }
                    catch
                    {
                        pictureBoxTemplate.Image = null;
                    }
                }
            }

            labelTemplateStatus.Text = templateImage == null
                ? "未生成模板"
                : string.IsNullOrWhiteSpace(previewError)
                    ? $"模板 {templateImage.Width}x{templateImage.Height}，轮廓点 {contourCount}"
                    : $"模板 {templateImage.Width}x{templateImage.Height}，轮廓预览失败";
            buttonRestoreTemplate.Enabled = originalTemplateImage != null;
            buttonOk.Enabled = templateImage != null;
        }

        private void checkBoxEraseTemplate_CheckedChanged(object sender, EventArgs e)
        {
            pictureBoxTemplate.Cursor = checkBoxEraseTemplate.Checked ? Cursors.Cross : Cursors.Default;
        }

        private void pictureBoxTemplate_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !checkBoxEraseTemplate.Checked)
                return;

            isPaintingTemplate = true;
            PaintTemplateAt(e.Location);
        }

        private void pictureBoxTemplate_MouseMove(object sender, MouseEventArgs e)
        {
            if (!isPaintingTemplate || e.Button != MouseButtons.Left)
                return;

            PaintTemplateAt(e.Location);
        }

        private void pictureBoxTemplate_MouseUp(object sender, MouseEventArgs e)
        {
            isPaintingTemplate = false;
        }

        private void PaintTemplateAt(Point clientPoint)
        {
            if (templateImage == null)
                return;

            Point? imagePoint = TemplateClientToImage(clientPoint);
            if (!imagePoint.HasValue)
                return;

            int brushSize = Math.Max(1, (int)numericBrushSize.Value);
            EnsureTemplateEraseMask();
            using (Graphics g = Graphics.FromImage(templateEraseMask))
            using (Brush brush = new SolidBrush(Color.White))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.FillEllipse(
                    brush,
                    imagePoint.Value.X - brushSize / 2f,
                    imagePoint.Value.Y - brushSize / 2f,
                    brushSize,
                    brushSize);
            }

            RefreshEditedTemplateFromOriginal();
            UpdateTemplatePreview();
        }

        private void EnsureTemplateEraseMask()
        {
            if (templateImage == null)
                return;

            if (templateEraseMask == null || templateEraseMask.Width != templateImage.Width || templateEraseMask.Height != templateImage.Height)
            {
                templateEraseMask?.Dispose();
                templateEraseMask = FastTemplateMatcher.CreateEmptyMask(templateImage.Size);
            }
        }

        private Point? TemplateClientToImage(Point clientPoint)
        {
            if (templateImage == null || pictureBoxTemplate.Width <= 0 || pictureBoxTemplate.Height <= 0)
                return null;

            Rectangle imageRect = GetZoomImageRectangle(pictureBoxTemplate, templateImage.Size);
            if (!imageRect.Contains(clientPoint) || imageRect.Width <= 0 || imageRect.Height <= 0)
                return null;

            int x = (int)Math.Round((clientPoint.X - imageRect.X) * templateImage.Width / (double)imageRect.Width);
            int y = (int)Math.Round((clientPoint.Y - imageRect.Y) * templateImage.Height / (double)imageRect.Height);
            x = Math.Max(0, Math.Min(templateImage.Width - 1, x));
            y = Math.Max(0, Math.Min(templateImage.Height - 1, y));
            return new Point(x, y);
        }

        private static Rectangle GetZoomImageRectangle(PictureBox pictureBox, Size imageSize)
        {
            if (imageSize.Width <= 0 || imageSize.Height <= 0 || pictureBox.Width <= 0 || pictureBox.Height <= 0)
                return Rectangle.Empty;

            double imageRatio = imageSize.Width / (double)imageSize.Height;
            double boxRatio = pictureBox.Width / (double)pictureBox.Height;
            int width;
            int height;
            if (imageRatio > boxRatio)
            {
                width = pictureBox.Width;
                height = (int)Math.Round(width / imageRatio);
            }
            else
            {
                height = pictureBox.Height;
                width = (int)Math.Round(height * imageRatio);
            }

            return new Rectangle((pictureBox.Width - width) / 2, (pictureBox.Height - height) / 2, width, height);
        }

        private Color GetTemplateEraseColor()
        {
            if (templateImage == null)
                return Color.Black;

            int sample = Math.Min(10, Math.Min(templateImage.Width, templateImage.Height));
            long r = 0;
            long g = 0;
            long b = 0;
            int count = 0;
            for (int y = 0; y < sample; y++)
            {
                for (int x = 0; x < sample; x++)
                    AddSample(templateImage.GetPixel(x, y), ref r, ref g, ref b, ref count);
                for (int x = templateImage.Width - sample; x < templateImage.Width; x++)
                    AddSample(templateImage.GetPixel(x, y), ref r, ref g, ref b, ref count);
            }
            for (int y = templateImage.Height - sample; y < templateImage.Height; y++)
            {
                for (int x = 0; x < sample; x++)
                    AddSample(templateImage.GetPixel(x, y), ref r, ref g, ref b, ref count);
                for (int x = templateImage.Width - sample; x < templateImage.Width; x++)
                    AddSample(templateImage.GetPixel(x, y), ref r, ref g, ref b, ref count);
            }

            if (count == 0)
                return Color.Black;

            return Color.FromArgb((int)(r / count), (int)(g / count), (int)(b / count));
        }

        private static void AddSample(Color color, ref long r, ref long g, ref long b, ref int count)
        {
            r += color.R;
            g += color.G;
            b += color.B;
            count++;
        }

        private void buttonRestoreTemplate_Click(object sender, EventArgs e)
        {
            if (originalTemplateImage == null)
                return;

            templateEraseMask?.Dispose();
            templateEraseMask = FastTemplateMatcher.CreateEmptyMask(originalTemplateImage.Size);
            RefreshEditedTemplateFromOriginal();
            UpdateTemplatePreview();
        }

        private void buttonOk_Click(object sender, EventArgs e)
        {
            if (templateImage == null)
            {
                MessageBoxTD.Show("请先生成模板。");
                return;
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private void buttonCancel_Click(object sender, EventArgs e)
        {
            DialogResult = DialogResult.Cancel;
            Close();
        }

        private void DisposeImages()
        {
            sourceImage?.Dispose();
            sourceImage = null;
            templateImage?.Dispose();
            templateImage = null;
            originalTemplateImage?.Dispose();
            originalTemplateImage = null;
            templateEraseMask?.Dispose();
            templateEraseMask = null;
            if (pictureBoxTemplate != null)
            {
                Image oldImage = pictureBoxTemplate.Image;
                pictureBoxTemplate.Image = null;
                oldImage?.Dispose();
            }
        }
    }
}
