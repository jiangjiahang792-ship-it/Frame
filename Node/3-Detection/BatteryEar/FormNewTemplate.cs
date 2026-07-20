using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Properties;

namespace TDJS_Vision.Node._3_Detection.BatteryEar
{
    public partial class FormNewTemplate : Form
    {
        // 极耳模版
        private Mat[] _templateJiErs = new Mat[3];
        // Mark点模版
        private Mat[] _templateMarks = new Mat[3];
        // 初始化图像
        Image InitImg = null;
        public FormNewTemplate()
        {
            InitializeComponent();
            try { InitImg = new Bitmap("./Template/BatteryEar/添加模版.png"); } catch (Exception) { }
            Shown += FormNewTemplate_Shown;
        }
        /// <summary>
        /// 加载图像
        /// </summary>
        /// <param name="bitmap"></param>
        public void LoadImages(Bitmap bitmap)
        {
            imageROIEditControl1.SetImage(bitmap);
        }

        private void FormNewTemplate_Shown(object sender, EventArgs e)
        {
            LoadTemplateImages("Template_JiEr", new[] { pictureBox1, pictureBox2, pictureBox3 });
            LoadTemplateImages("Template_Mark", new[] { pictureBox4, pictureBox5, pictureBox6 });
        }
        /// <summary>
        /// 加载模板图像并显示到 PictureBox
        /// </summary>
        private void LoadTemplateImages(string name, PictureBox[] pictureBoxes)
        {
            for (int i = 1; i <= 3; i++)
            {
                string filePath = "./Template/BatteryEar/" + name + $"{i}.bmp";

                // 检查文件是否存在
                if (!File.Exists(filePath))
                {
                    if (name == "Template_JiEr")
                        _templateJiErs[i - 1] = null;
                    else if (name == "Template_Mark")
                        _templateMarks[i - 1] = null;
                    pictureBoxes[i - 1].Image = InitImg;
                    continue;
                }

                try
                {
                    // 使用 OpenCvSharp 加载图像为 Mat
                    Mat mat = Cv2.ImRead(filePath, ImreadModes.Color);

                    if (name == "Template_JiEr")
                        _templateJiErs[i - 1] = mat;
                    else if(name == "Template_Mark")
                        _templateMarks[i - 1] = mat;

                    if (mat.Empty())
                        continue;

                    // 将 Mat 转换为 Bitmap 并显示到对应的 PictureBox
                    using (var bitmap = mat.ToBitmap())
                    {
                        pictureBoxes[i - 1].Image = (Bitmap)bitmap.Clone(); // 克隆避免资源占用
                    }
                }
                catch (Exception ex)
                {
                }
            }
        }
        /// <summary>
        /// 获取极耳模版图像
        /// </summary>
        /// <returns></returns>
        public List<Mat> GetJiErTemplate()
        {
            LoadTemplateImages("Template_JiEr", new[] { pictureBox1, pictureBox2, pictureBox3 });
            return _templateJiErs?
                .Where(mat => mat != null && !mat.Empty())  // 过滤 null 和空 Mat
                .ToList()
                ?? new List<Mat>(); // 如果源为 null，返回空列表
        }
        /// <summary>
        /// 获取Mark点模版图像
        /// </summary>
        /// <returns></returns>
        public List<Mat> GetMarkTemplate()
        {
            LoadTemplateImages("Template_Mark", new[] { pictureBox4, pictureBox5, pictureBox6 });
            return _templateMarks?
                .Where(mat => mat != null && !mat.Empty())  // 过滤 null 和空 Mat
                .ToList()
                ?? new List<Mat>(); // 如果源为 null，返回空列表
        }
        /// <summary>
        /// 点击添加模版
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void pictureBox_Click(object sender, EventArgs e)
        {
            PictureBox clickedBox = sender as PictureBox;

            if (clickedBox == null) return;

            HandlePictureBoxClick(clickedBox);
        }
        /// <summary>
        /// 处理模版图像点击事件
        /// </summary>
        private void HandlePictureBoxClick(PictureBox clickedBox)
        {
            // 从当前图像中获取绘制的模版图像
            var imgs = imageROIEditControl1.GetROIImages();
            if (imgs.Count == 0)
            {
                MessageBoxTD.Show("当前图像未绘制任何模版！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            else if (imgs.Count == 1)
            {
                clickedBox.Image = imgs[0].ToBitmap();
                switch (clickedBox.Name)
                {
                    case "pictureBox1":
                        _templateJiErs[0] = imgs[0];
                        imgs[0].SaveImage("./Template/BatteryEar/Template_JiEr1.bmp");
                        break;
                    case "pictureBox2":
                        _templateJiErs[1] = imgs[0];
                        imgs[0].SaveImage("./Template/BatteryEar/Template_JiEr2.bmp");
                        break;
                    case "pictureBox3":
                        _templateJiErs[2] = imgs[0];
                        imgs[0].SaveImage("./Template/BatteryEar/Template_JiEr3.bmp");
                        break;
                    case "pictureBox4":
                        _templateMarks[0] = imgs[0];
                        imgs[0].SaveImage("./Template/BatteryEar/Template_Mark1.bmp");
                        break;
                    case "pictureBox5":
                        _templateMarks[1] = imgs[0];
                        imgs[0].SaveImage("./Template/BatteryEar/Template_Mark2.bmp");
                        break;
                    case "pictureBox6":
                        _templateMarks[2] = imgs[0];
                        imgs[0].SaveImage("./Template/BatteryEar/Template_Mark3.bmp");
                        break;
                    default:
                        break;
                }
            }
            else
            {
                MessageBoxTD.Show("当前图像绘制了多个模版，每次只能添加一个模版！", "警告", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
        }
        /// <summary>
        /// 清空极耳模版
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button2_Click(object sender, EventArgs e)
        {
            DeleteTemplateImages("Template_JiEr", new[] { pictureBox1, pictureBox2, pictureBox3 });
        }

        /// <summary>
        /// 清空Mark点模版
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void button1_Click(object sender, EventArgs e)
        {
            DeleteTemplateImages("Template_Mark", new[] { pictureBox4, pictureBox5, pictureBox6 });
        }

        /// <summary>
        /// 删除模版图像
        /// </summary>
        private void DeleteTemplateImages(string name, PictureBox[] pictureBoxes)
        {
            if (MessageBoxTD.Show("删除后无法恢复!是否删除所有模版？", "警告", MessageBoxButtons.YesNo, MessageBoxIcon.Warning)
                != DialogResult.Yes)
                return;
            for (int i = 1; i <= 3; i++)
            {
                string filePath = "./Template/BatteryEar/" + name + $"{i}.bmp";

                // 模版文件存在侧删除
                if (File.Exists(filePath))
                    File.Delete(filePath);
                pictureBoxes[i - 1].Image = InitImg;
                if (name == "Template_JiEr")
                    _templateJiErs[i - 1] = null;
                else if (name == "Template_Mark")
                    _templateMarks[i - 1] = null;
            }

        }

        private void button3_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string filePath = openFileDialog1.FileName;

                try
                {
                    // 使用自定义方法加载图像（不锁定文件）
                    Bitmap bitmap = LoadBitmapWithoutLock(filePath);

                    // 赋值给 PictureBox（或其他控件）
                    imageROIEditControl1.SetImage(bitmap);

                    // ✅ 此时文件已解锁，其他程序可访问
                }
                catch (Exception ex)
                {
                    MessageBoxTD.Show($"无法加载图像：{ex.Message}", "错误",
                        MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        private Bitmap LoadBitmapWithoutLock(string filePath)
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("文件未找到", filePath);

            using (var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read))
            {
                // 将文件数据读入内存
                byte[] buffer = new byte[fs.Length];
                fs.Read(buffer, 0, buffer.Length);

                // 使用 MemoryStream 从内存创建 Bitmap
                using (var ms = new MemoryStream(buffer))
                {
                    return (Bitmap)Bitmap.FromStream(ms);
                    // 注意：MemoryStream 不会锁定原始文件
                }
            }
        }
    }
}
