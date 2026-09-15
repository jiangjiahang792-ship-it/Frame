using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._3_Detection.BatteryEar
{
    /// <summary>
    /// 电池极耳模板管理窗体，负责模板缓存、编辑预览和模板快照生命周期。
    /// </summary>
    public partial class FormNewTemplate : Form
    {
        /// <summary>
        /// 极耳模板文件名前缀。
        /// </summary>
        private const string JiErTemplateName = "Template_JiEr";

        /// <summary>
        /// Mark点模板文件名前缀。
        /// </summary>
        private const string MarkTemplateName = "Template_Mark";

        /// <summary>
        /// 全部节点共享的模板文件版本，模板新增或删除后递增。
        /// </summary>
        private static int _globalTemplateRevision;

        /// <summary>
        /// 模板缓存同步对象，保护模板替换、复制和释放。
        /// </summary>
        private readonly object _templateSync = new object();

        /// <summary>
        /// 窗体持有的极耳模板缓存。
        /// </summary>
        private Mat[] _templateJiErs = new Mat[3];

        /// <summary>
        /// 窗体持有的Mark点模板缓存。
        /// </summary>
        private Mat[] _templateMarks = new Mat[3];

        /// <summary>
        /// 缺少模板时用于生成独立预览副本的占位图。
        /// </summary>
        private Image _placeholderImage;

        /// <summary>
        /// 极耳模板是否已经从磁盘加载。
        /// </summary>
        private bool _jiErTemplatesLoaded;

        /// <summary>
        /// Mark点模板是否已经从磁盘加载。
        /// </summary>
        private bool _markTemplatesLoaded;

        /// <summary>
        /// 当前极耳模板缓存对应的全局版本。
        /// </summary>
        private int _jiErTemplateRevision = -1;

        /// <summary>
        /// 当前Mark点模板缓存对应的全局版本。
        /// </summary>
        private int _markTemplateRevision = -1;

        /// <summary>
        /// 窗体自有资源是否已经释放。
        /// </summary>
        private int _resourcesReleased;

        /// <summary>
        /// 初始化模板管理窗体和占位图。
        /// </summary>
        public FormNewTemplate()
        {
            InitializeComponent();
            DetachDesignerImageReferences();
            try
            {
                _placeholderImage = LoadBitmapWithoutLock("./Template/BatteryEar/添加模版.png");
            }
            catch (Exception ex)
            {
                LogHelper.AddLog(MsgLevel.Warn, "电池极耳模板占位图加载失败：" + ex.Message, true);
            }

            RefreshTemplatePreviews(JiErTemplateName, GetJiErPictureBoxes());
            RefreshTemplatePreviews(MarkTemplateName, GetMarkPictureBoxes());
            Shown += FormNewTemplate_Shown;
        }

        /// <summary>
        /// 将源图交给ROI编辑控件，并由控件负责替换和销毁。
        /// </summary>
        /// <param name="bitmap">由ROI编辑控件接管的图像。</param>
        public void LoadImages(Bitmap bitmap)
        {
            imageROIEditControl1.SetImage(bitmap);
        }

        /// <summary>
        /// 每次显示模板窗体时重新读取磁盘模板，并刷新六个独立预览图。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void FormNewTemplate_Shown(object sender, EventArgs e)
        {
            LoadTemplateImages(JiErTemplateName, GetJiErPictureBoxes());
            LoadTemplateImages(MarkTemplateName, GetMarkPictureBoxes());
        }

        /// <summary>
        /// 从磁盘重新加载指定模板组，并刷新对应PictureBox。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="pictureBoxes">模板预览控件。</param>
        private void LoadTemplateImages(string name, PictureBox[] pictureBoxes)
        {
            int revision = Volatile.Read(ref _globalTemplateRevision);
            Mat[] loadedTemplates = ReadTemplateSet(name);
            try
            {
                ReplaceTemplateSet(name, loadedTemplates, revision);
                loadedTemplates = null;
                RefreshTemplatePreviews(name, pictureBoxes);
            }
            finally
            {
                DisposeTemplateSet(loadedTemplates);
            }
        }

        /// <summary>
        /// 从磁盘读取一组模板，失败或空图位置保留为null。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <returns>由调用方交给模板缓存接管的三张Mat。</returns>
        private static Mat[] ReadTemplateSet(string name)
        {
            Mat[] templates = new Mat[3];
            for (int index = 0; index < templates.Length; index++)
            {
                string filePath = GetTemplateFilePath(name, index);
                if (!File.Exists(filePath))
                    continue;

                Mat template = null;
                try
                {
                    template = Cv2.ImRead(filePath, ImreadModes.Color);
                    if (template == null || template.Empty())
                    {
                        template?.Dispose();
                        template = null;
                        continue;
                    }

                    templates[index] = template;
                    template = null;
                }
                catch (Exception ex)
                {
                    LogHelper.AddLog(MsgLevel.Warn, "电池极耳模板加载失败：" + filePath + "，原因：" + ex.Message, true);
                }
                finally
                {
                    template?.Dispose();
                }
            }

            return templates;
        }

        /// <summary>
        /// 确保指定模板组至少从磁盘加载一次，正式检测不会每个ROI重复读盘。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        private void EnsureTemplateImagesLoaded(string name)
        {
            int revision = Volatile.Read(ref _globalTemplateRevision);
            Mat[] previousTemplates = null;
            lock (_templateSync)
            {
                ThrowIfResourcesReleased();
                if (IsTemplateSetCurrentNoLock(name, revision))
                    return;

                Mat[] loadedTemplates = ReadTemplateSet(name);
                previousTemplates = GetTemplateSetNoLock(name);
                SetTemplateSetNoLock(name, loadedTemplates, true, revision);
            }

            DisposeTemplateSet(previousTemplates);
        }

        /// <summary>
        /// 返回极耳模板的独立快照，调用方必须释放列表中的每张Mat。
        /// </summary>
        /// <returns>极耳模板快照。</returns>
        public List<Mat> GetJiErTemplate()
        {
            return GetTemplateSnapshots(JiErTemplateName);
        }

        /// <summary>
        /// 返回Mark点模板的独立快照，调用方必须释放列表中的每张Mat。
        /// </summary>
        /// <returns>Mark点模板快照。</returns>
        public List<Mat> GetMarkTemplate()
        {
            return GetTemplateSnapshots(MarkTemplateName);
        }

        /// <summary>
        /// 在锁内复制有效模板，使正式检测不借用可被界面替换的Mat。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <returns>调用方拥有的模板副本。</returns>
        private List<Mat> GetTemplateSnapshots(string name)
        {
            EnsureTemplateImagesLoaded(name);
            var snapshots = new List<Mat>();
            try
            {
                lock (_templateSync)
                {
                    ThrowIfResourcesReleased();
                    foreach (Mat template in GetTemplateSetNoLock(name))
                    {
                        if (template != null && !template.Empty())
                            snapshots.Add(template.Clone());
                    }
                }

                return snapshots;
            }
            catch
            {
                DisposeTemplateList(snapshots);
                throw;
            }
        }

        /// <summary>
        /// 响应六个模板预览框的点击事件。
        /// </summary>
        /// <param name="sender">被点击的PictureBox。</param>
        /// <param name="e">事件参数。</param>
        private void pictureBox_Click(object sender, EventArgs e)
        {
            var clickedBox = sender as PictureBox;
            if (clickedBox != null)
                HandlePictureBoxClick(clickedBox);
        }

        /// <summary>
        /// 将当前唯一ROI保存为目标模板，并原子替换模板缓存和预览图。
        /// </summary>
        /// <param name="clickedBox">目标模板预览框。</param>
        private void HandlePictureBoxClick(PictureBox clickedBox)
        {
            List<Mat> roiImages = imageROIEditControl1.GetROIImages();
            try
            {
                if (roiImages == null || roiImages.Count == 0)
                {
                    MessageBoxTD.Show("当前图像未绘制任何模版！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                if (roiImages.Count != 1)
                {
                    MessageBoxTD.Show("当前图像绘制了多个模版，每次只能添加一个模版！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                string templateName;
                string filePath;
                int templateIndex;
                if (!TryResolveTemplateTarget(clickedBox, out templateName, out templateIndex, out filePath))
                    return;

                Mat template = roiImages[0];
                Bitmap preview = null;
                try
                {
                    Directory.CreateDirectory(Path.GetDirectoryName(filePath));
                    template.SaveImage(filePath);
                    preview = template.ToBitmap();
                    int revision = Interlocked.Increment(ref _globalTemplateRevision);
                    ReplaceTemplateAt(templateName, templateIndex, template, revision);
                    roiImages[0] = null;
                    ReplacePictureBoxImage(clickedBox, preview);
                    preview = null;
                }
                finally
                {
                    preview?.Dispose();
                }
            }
            finally
            {
                DisposeTemplateList(roiImages);
            }
        }

        /// <summary>
        /// 清空全部极耳模板。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void button2_Click(object sender, EventArgs e)
        {
            DeleteTemplateImages(JiErTemplateName, GetJiErPictureBoxes());
        }

        /// <summary>
        /// 清空全部Mark点模板。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void button1_Click(object sender, EventArgs e)
        {
            DeleteTemplateImages(MarkTemplateName, GetMarkPictureBoxes());
        }

        /// <summary>
        /// 删除指定模板组的磁盘文件、缓存Mat和预览Bitmap。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="pictureBoxes">模板预览控件。</param>
        private void DeleteTemplateImages(string name, PictureBox[] pictureBoxes)
        {
            if (MessageBoxTD.Show("删除后无法恢复!是否删除所有模版？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes)
            {
                return;
            }

            for (int index = 0; index < pictureBoxes.Length; index++)
            {
                string filePath = GetTemplateFilePath(name, index);
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }

            int revision = Interlocked.Increment(ref _globalTemplateRevision);
            ReplaceTemplateSet(name, new Mat[3], revision);
            RefreshTemplatePreviews(name, pictureBoxes);
        }

        /// <summary>
        /// 从文件选择器加载一张不锁定源文件的ROI底图。
        /// </summary>
        /// <param name="sender">事件发送方。</param>
        /// <param name="e">事件参数。</param>
        private void button3_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() != DialogResult.OK)
                return;

            Bitmap bitmap = null;
            try
            {
                bitmap = LoadBitmapWithoutLock(openFileDialog1.FileName);
                imageROIEditControl1.SetImage(bitmap);
                bitmap = null;
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show("无法加载图像：" + ex.Message, "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// 读取图像后复制为独立Bitmap，关闭文件流后仍可安全使用。
        /// </summary>
        /// <param name="filePath">图像文件路径。</param>
        /// <returns>调用方拥有的独立Bitmap。</returns>
        private static Bitmap LoadBitmapWithoutLock(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件未找到", filePath);

            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (var source = new Bitmap(fileStream))
            {
                return new Bitmap(source);
            }
        }

        /// <summary>
        /// 用新的模板组替换窗体缓存，并释放旧模板Mat。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="templates">由窗体接管的新模板组。</param>
        /// <param name="revision">新模板组对应的全局版本。</param>
        private void ReplaceTemplateSet(string name, Mat[] templates, int revision)
        {
            Mat[] previousTemplates;
            lock (_templateSync)
            {
                ThrowIfResourcesReleased();
                previousTemplates = GetTemplateSetNoLock(name);
                SetTemplateSetNoLock(name, templates, true, revision);
            }

            DisposeTemplateSet(previousTemplates);
        }

        /// <summary>
        /// 替换模板组中的一张Mat，并释放旧Mat。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="index">模板索引。</param>
        /// <param name="template">由模板缓存接管的新Mat。</param>
        /// <param name="revision">新模板对应的全局版本。</param>
        private void ReplaceTemplateAt(string name, int index, Mat template, int revision)
        {
            Mat previousTemplate;
            lock (_templateSync)
            {
                ThrowIfResourcesReleased();
                Mat[] templates = GetTemplateSetNoLock(name);
                previousTemplate = templates[index];
                templates[index] = template;
                SetTemplateSetStateNoLock(name, true, revision);
            }

            previousTemplate?.Dispose();
        }

        /// <summary>
        /// 按当前模板缓存刷新一组预览框，每个PictureBox拥有独立Bitmap。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="pictureBoxes">模板预览控件。</param>
        private void RefreshTemplatePreviews(string name, PictureBox[] pictureBoxes)
        {
            for (int index = 0; index < pictureBoxes.Length; index++)
                ReplacePictureBoxImage(pictureBoxes[index], CreateTemplatePreview(name, index));
        }

        /// <summary>
        /// 为指定模板创建独立预览Bitmap，缺少模板时克隆占位图。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="index">模板索引。</param>
        /// <returns>由PictureBox接管的预览图。</returns>
        private Bitmap CreateTemplatePreview(string name, int index)
        {
            lock (_templateSync)
            {
                ThrowIfResourcesReleased();
                Mat template = GetTemplateSetNoLock(name)[index];
                if (template != null && !template.Empty())
                    return template.ToBitmap();
                return _placeholderImage == null ? null : new Bitmap(_placeholderImage);
            }
        }

        /// <summary>
        /// 替换PictureBox图像并释放旧图。
        /// </summary>
        /// <param name="pictureBox">目标预览框。</param>
        /// <param name="image">由PictureBox接管的新图。</param>
        private static void ReplacePictureBoxImage(PictureBox pictureBox, Image image)
        {
            Image previous = pictureBox.Image;
            if (ReferenceEquals(previous, image))
                return;

            pictureBox.Image = image;
            previous?.Dispose();
        }

        /// <summary>
        /// 解除Designer资源图引用但不释放共享资源，后续统一使用窗体创建的独立Bitmap。
        /// </summary>
        private void DetachDesignerImageReferences()
        {
            foreach (PictureBox pictureBox in GetJiErPictureBoxes())
                pictureBox.Image = null;
            foreach (PictureBox pictureBox in GetMarkPictureBoxes())
                pictureBox.Image = null;
        }

        /// <summary>
        /// 将被点击的PictureBox映射为模板组、索引和文件路径。
        /// </summary>
        /// <param name="pictureBox">被点击的预览框。</param>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="index">模板索引。</param>
        /// <param name="filePath">模板文件路径。</param>
        /// <returns>映射成功时返回true。</returns>
        private static bool TryResolveTemplateTarget(PictureBox pictureBox, out string name, out int index, out string filePath)
        {
            name = null;
            index = -1;
            switch (pictureBox.Name)
            {
                case "pictureBox1": name = JiErTemplateName; index = 0; break;
                case "pictureBox2": name = JiErTemplateName; index = 1; break;
                case "pictureBox3": name = JiErTemplateName; index = 2; break;
                case "pictureBox4": name = MarkTemplateName; index = 0; break;
                case "pictureBox5": name = MarkTemplateName; index = 1; break;
                case "pictureBox6": name = MarkTemplateName; index = 2; break;
            }

            filePath = index < 0 ? null : GetTemplateFilePath(name, index);
            return index >= 0;
        }

        /// <summary>
        /// 生成指定模板的相对文件路径。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="index">从零开始的模板索引。</param>
        /// <returns>模板文件路径。</returns>
        private static string GetTemplateFilePath(string name, int index)
        {
            return "./Template/BatteryEar/" + name + (index + 1) + ".bmp";
        }

        /// <summary>
        /// 返回三个极耳模板预览框。
        /// </summary>
        /// <returns>极耳预览框数组。</returns>
        private PictureBox[] GetJiErPictureBoxes()
        {
            return new[] { pictureBox1, pictureBox2, pictureBox3 };
        }

        /// <summary>
        /// 返回三个Mark点模板预览框。
        /// </summary>
        /// <returns>Mark点预览框数组。</returns>
        private PictureBox[] GetMarkPictureBoxes()
        {
            return new[] { pictureBox4, pictureBox5, pictureBox6 };
        }

        /// <summary>
        /// 在持有同步锁时返回指定模板数组。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <returns>窗体持有的模板数组。</returns>
        private Mat[] GetTemplateSetNoLock(string name)
        {
            if (name == JiErTemplateName)
                return _templateJiErs;
            if (name == MarkTemplateName)
                return _templateMarks;
            throw new ArgumentOutOfRangeException(nameof(name), "未知的模板类型！");
        }

        /// <summary>
        /// 在持有同步锁时设置指定模板数组和加载状态。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="templates">新模板数组。</param>
        /// <param name="loaded">是否已经完成磁盘加载。</param>
        /// <param name="revision">模板组对应的全局版本。</param>
        private void SetTemplateSetNoLock(string name, Mat[] templates, bool loaded, int revision)
        {
            if (name == JiErTemplateName)
                _templateJiErs = templates;
            else if (name == MarkTemplateName)
                _templateMarks = templates;
            else
                throw new ArgumentOutOfRangeException(nameof(name), "未知的模板类型！");

            SetTemplateSetStateNoLock(name, loaded, revision);
        }

        /// <summary>
        /// 在持有同步锁时判断模板组是否已加载且版本为最新。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="revision">当前全局版本。</param>
        /// <returns>缓存可直接复用时返回true。</returns>
        private bool IsTemplateSetCurrentNoLock(string name, int revision)
        {
            if (name == JiErTemplateName)
                return _jiErTemplatesLoaded && _jiErTemplateRevision == revision;
            if (name == MarkTemplateName)
                return _markTemplatesLoaded && _markTemplateRevision == revision;
            throw new ArgumentOutOfRangeException(nameof(name), "未知的模板类型！");
        }

        /// <summary>
        /// 在持有同步锁时设置模板组加载状态和版本。
        /// </summary>
        /// <param name="name">模板文件名前缀。</param>
        /// <param name="loaded">加载状态。</param>
        /// <param name="revision">模板组对应的全局版本。</param>
        private void SetTemplateSetStateNoLock(string name, bool loaded, int revision)
        {
            if (name == JiErTemplateName)
            {
                _jiErTemplatesLoaded = loaded;
                _jiErTemplateRevision = revision;
            }
            else if (name == MarkTemplateName)
            {
                _markTemplatesLoaded = loaded;
                _markTemplateRevision = revision;
            }
            else
                throw new ArgumentOutOfRangeException(nameof(name), "未知的模板类型！");
        }

        /// <summary>
        /// 释放模板集合中的全部Mat。
        /// </summary>
        /// <param name="templates">需要释放的模板集合。</param>
        private static void DisposeTemplateList(IEnumerable<Mat> templates)
        {
            if (templates == null)
                return;

            foreach (Mat template in templates)
                template?.Dispose();
        }

        /// <summary>
        /// 释放模板数组中的全部Mat。
        /// </summary>
        /// <param name="templates">需要释放的模板数组。</param>
        private static void DisposeTemplateSet(Mat[] templates)
        {
            DisposeTemplateList(templates);
        }

        /// <summary>
        /// 窗体已释放时阻止再次加载或复制模板。
        /// </summary>
        private void ThrowIfResourcesReleased()
        {
            if (Volatile.Read(ref _resourcesReleased) != 0)
                throw new ObjectDisposedException(nameof(FormNewTemplate));
        }

        /// <summary>
        /// 释放模板Mat、六张预览Bitmap、ROI底图和占位图。
        /// </summary>
        private void ReleaseTemplateResources()
        {
            if (Interlocked.Exchange(ref _resourcesReleased, 1) != 0)
                return;

            Shown -= FormNewTemplate_Shown;
            imageROIEditControl1.SetImage(null);
            foreach (PictureBox pictureBox in GetJiErPictureBoxes())
                ReplacePictureBoxImage(pictureBox, null);
            foreach (PictureBox pictureBox in GetMarkPictureBoxes())
                ReplacePictureBoxImage(pictureBox, null);

            Mat[] jiErTemplates;
            Mat[] markTemplates;
            Image placeholderImage;
            lock (_templateSync)
            {
                jiErTemplates = _templateJiErs;
                markTemplates = _templateMarks;
                placeholderImage = _placeholderImage;
                _templateJiErs = new Mat[3];
                _templateMarks = new Mat[3];
                _placeholderImage = null;
                _jiErTemplatesLoaded = false;
                _markTemplatesLoaded = false;
                _jiErTemplateRevision = -1;
                _markTemplateRevision = -1;
            }

            DisposeTemplateSet(jiErTemplates);
            DisposeTemplateSet(markTemplates);
            placeholderImage?.Dispose();
        }
    }
}
