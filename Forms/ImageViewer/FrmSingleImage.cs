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
        private Process _process;

        public FrmSingleImage(string name)
        {
            InitializeComponent();
            this.Text = name;
            FormName = name;
            NodeImageShow.ImageShowChanged += NodeImageShow_ImageShowChanged;
            NodeImageShow.ImageShowWindowNameChanged += NodeImageShow_ImageShowWindowNameChanged1;
            SolRunParamControl.ImageShowChanged += NodeImageShow_ImageShowChanged;
            button1.Click += button1_Click;
            button2.Click += button2_Click;
        }

        private void NodeImageShow_ImageShowWindowNameChanged1(Process process, string winName)
        {
            if (this.FormName != winName)
                return;

            _process = process;
            
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
            // 确保图像更新在 UI 线程中执行
            if (this.InvokeRequired)
            {
                this./*Begin*/Invoke(new MethodInvoker(() => { ytPictrueBox1.Image = BitmapConverter.ToBitmap(mat); }));
            }
            else
            {
                ytPictrueBox1.Image = BitmapConverter.ToBitmap(mat);
            }
        }

        private void NodeImageShow_ImageShowChanged(object sender, ImageShowPamra e)
        {
            if (this.FormName != e.WinName)
                return;


            // 封装 UI 更新操作
            Action updateImage = () =>
            {
                // 释放旧图像
                if (ytPictrueBox1.Image != null)
                {
                    ytPictrueBox1.Image.Dispose();
                }
                // 设置新图像
                ytPictrueBox1.Image = e.Bitmap;
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
                        showParam.WindowName == FormName)
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
                MessageBoxTD.Show("未找到当前图像窗口对应的工位！");
                return;
            }

            try
            {
                using (var solRunParam = new SolRunParamControl())
                {
                    solRunParam.Init(process);
                    if (!solRunParam.StartAutoLearning())
                    {
                        MessageBoxTD.Show("当前工位未配置AI节点，无法一键学习！");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBoxTD.Show($"一键学习失败！原因：{ex.Message}");
            }
        }

        private void button2_Click(object sender, EventArgs e)
        {
            var process = GetCurrentProcess();
            if (process == null)
            {
                MessageBoxTD.Show("未找到当前图像窗口对应的工位！");
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
            WinName = winname;
            Bitmap = bitmap;
        }
    }
}
