using System;
using System.Drawing;
using System.Windows.Forms;
using OpenCvSharp.Extensions;
using OpenCvSharp;

namespace TDJS_Vision.Forms.ImageViewer
{
    public partial class MatViewer : FormBase
    {
        public MatViewer(string name)
        {
            InitializeComponent();
            LanguageManager.Bind(button1, "Common.Close");
            LanguageManager.Apply(this);
            Text = name;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            LanguageManager.Apply(button1);
        }

        public static void ShowImage(string name, Mat mat)
        {
            using (var win = new MatViewer(name))
            {
                win.SetImage(mat);
                win.ShowDialog();
            }
        }

        /// <summary>
        /// 在调用线程完成Mat到Bitmap转换，再将Bitmap所有权移交给UI控件。
        /// </summary>
        /// <param name="mat">只读源Mat，方法不接管其生命周期。</param>
        public void SetImage(Mat mat)
        {
            Bitmap bitmap = null;
            try
            {
                bitmap = mat == null ? null : BitmapConverter.ToBitmap(mat);
                if (IsDisposed)
                    return;

                if (InvokeRequired)
                {
                    if (!IsHandleCreated)
                        return;

                    Invoke(new Action<Bitmap>(SetViewerBitmap), bitmap);
                }
                else
                {
                    SetViewerBitmap(bitmap);
                }

                bitmap = null;
            }
            finally
            {
                bitmap?.Dispose();
            }
        }

        /// <summary>
        /// 在UI线程将Bitmap交给通用图片控件。
        /// </summary>
        /// <param name="bitmap">由图片控件接管的新图。</param>
        private void SetViewerBitmap(Bitmap bitmap)
        {
            ytPictrueBox1.Image = bitmap;
        }

        private void button1_Click(object sender, EventArgs e)
        {
            Close();
        }
    }
}
