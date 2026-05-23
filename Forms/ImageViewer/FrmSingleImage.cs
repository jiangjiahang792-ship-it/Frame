using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Forms.SolRunParam;
using System;
using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Forms.ImageViewer
{
    public partial class FrmSingleImage : DockContent
    {

        public string FormName;
        private bool _isBoundToProcess;
        private Process _process;

        public FrmSingleImage(string name)
        {
            InitializeComponent();
            BindLanguage();
            FormName = NormalizeWindowKey(name);
            this.Text = GetWindowDisplayName(FormName);
            NodeImageShow.ImageShowChanged += NodeImageShow_ImageShowChanged;
            NodeImageShow.ImageShowWindowNameChanged += NodeImageShow_ImageShowWindowNameChanged1;
            SolRunParamControl.ImageShowChanged += NodeImageShow_ImageShowChanged;
            button1.Click += button1_Click;
            button2.Click += button2_Click;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(button1, "ImageViewer.OneClickLearning");
            LanguageManager.Bind(button2, "ImageViewer.ManualParam");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(button1);
            LanguageManager.Apply(button2);
            if (!_isBoundToProcess)
                Text = GetWindowDisplayName(FormName);
        }

        public static string GetWindowKey(int index)
        {
            return "ImageWindow" + index;
        }

        public static string NormalizeWindowKey(string windowName)
        {
            if (string.IsNullOrWhiteSpace(windowName))
                return string.Empty;

            if (windowName.StartsWith("ImageWindow", StringComparison.OrdinalIgnoreCase))
                return windowName;

            bool isImageWindowName = windowName.StartsWith("图像窗口", StringComparison.OrdinalIgnoreCase) ||
                windowName.StartsWith("Image Window", StringComparison.OrdinalIgnoreCase);
            if (!isImageWindowName)
                return windowName;

            string numberText = string.Empty;
            foreach (char c in windowName)
            {
                if (char.IsDigit(c))
                    numberText += c;
            }

            if (int.TryParse(numberText, out int index) && index > 0)
                return GetWindowKey(index);

            return windowName;
        }

        public static string GetWindowDisplayName(string windowName)
        {
            string key = NormalizeWindowKey(windowName);
            if (key.StartsWith("ImageWindow", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(key.Substring("ImageWindow".Length), out int index))
            {
                return LanguageManager.Format("ImageViewer.ImageWindow", index);
            }

            return windowName;
        }

        private void NodeImageShow_ImageShowWindowNameChanged1(Process process, string winName)
        {
            if (FormName != NormalizeWindowKey(winName))
                return;

            _process = process;
            _isBoundToProcess = true;
            
            // 封装 UI 更新操作
            Action updateImage = () =>
            {
                this.Text = process.ProcessName;
            };

            // 跨线程安全调用
            if (this.InvokeRequired)
            {
                this.BeginInvoke(updateImage);
            }
            else
            {
                updateImage();
            }
        }


        public static void ShowImage(string name, Mat mat)
        {
            var win = new FrmSingleImage(name);
            win.SetImage(mat);
            win.Show();
        }
        public void SetImage(Mat mat)
        {
            SetViewerImage(mat == null ? null : BitmapConverter.ToBitmap(mat));
        }

        private void NodeImageShow_ImageShowChanged(object sender, ImageShowPamra e)
        {
            if (FormName != NormalizeWindowKey(e.WinName))
                return;


            SetViewerImage(e.Bitmap);
        }

        private void SetViewerImage(Bitmap bitmap)
        {
            if (IsDisposed)
            {
                bitmap?.Dispose();
                return;
            }

            Action updateImage = () =>
            {
                if (showImageControl1.IsDisposed)
                {
                    bitmap?.Dispose();
                    return;
                }

                showImageControl1.ImageBitmap = bitmap;
            };

            if (InvokeRequired)
            {
                if (!IsHandleCreated)
                {
                    bitmap?.Dispose();
                    return;
                }

                try
                {
                    BeginInvoke(updateImage);
                }
                catch
                {
                    bitmap?.Dispose();
                }
            }
            else
            {
                updateImage();
            }
        }

        private void FrmSingleImage_FormClosing(object sender, FormClosingEventArgs e)
        {
            FrmImageViewer.CurWindowsNum--;
            e.Cancel = true;
            Hide();
        }

        private Process GetCurrentProcess()
        {
            if (_process != null)
                return _process;

            foreach (var process in Solution.Instance.AllProcesses)
            {
                foreach (var node in process.Nodes)
                {
                    if (node is NodeImageShow nodeShow &&
                        nodeShow.ParamForm.Params is NodeParamImageShow showParam &&
                        NormalizeWindowKey(showParam.WindowName) == FormName)
                    {
                        _process = process;
                        return _process;
                    }
                }
            }

            return null;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            var process = GetCurrentProcess();
            if (process == null)
            {
                MessageBoxTD.Show(LanguageManager.T("ImageViewer.ProcessNotFound"));
                return;
            }

            try
            {
                using (var solRunParam = new SolRunParamControl())
                {
                    solRunParam.Init(process);
                    if (!solRunParam.StartAutoLearning())
                    {
                        MessageBoxTD.Show(LanguageManager.T("ImageViewer.AINodeNotConfigured"));
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show(LanguageManager.Format("ImageViewer.OneClickLearningFailed", ex.Message));
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var process = GetCurrentProcess();
            if (process == null)
            {
                MessageBoxTD.Show(LanguageManager.T("ImageViewer.ProcessNotFound"));
                return;
            }

            using (var form = new FormSolRunParam(process.ProcessName))
            {
                form.ShowDialog(this);
            }
        }
    }
    public class ImageShowPamra
    {
        public string WinName;
        public Bitmap Bitmap;
        public ImageShowPamra(string winname, Bitmap bitmap)
        {
            WinName = FrmSingleImage.NormalizeWindowKey(winname);
            Bitmap = bitmap;
        }
    }
}
