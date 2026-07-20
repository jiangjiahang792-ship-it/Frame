using Basler.Pylon;
using Logger;
using Microsoft.Win32;
using Newtonsoft.Json;
using ServiceStack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.Expando;
using System.Windows.Forms;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Forms.DetectItemManager
{
    public partial class FormDetectItemManager : FormBase
    {
        public FormDetectItemManager()
        {
            InitializeComponent();
            InitList();
        }
        /// <summary>
        /// 初始化列表
        /// </summary>
        private void InitList()
        {
            panelList.Controls.Clear();
            if (Solution.Instance.DetectItemDic!=null)
            {
                foreach (var pair in Solution.Instance.DetectItemDic)
                {
                    DetectItemListBox box = new DetectItemListBox();
                    box.LoadConfig(pair.Key, pair.Value);
                    box.Dock = DockStyle.Top;
                    box.Width = panelList.Width;
                    panelList.Controls.Add(box);
                }
            }
            
        }
        /// <summary>
        /// 选择文件
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSelect_Click(object sender, EventArgs e)
        {
            if(openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                string[] strings = openFileDialog1.FileNames;
                textBoxPath.Text = "";
                textBoxName.Text = "";
                int pathIndex = 1;
                foreach (var item in strings)
                {
                    string enter = Environment.NewLine;
                    if (pathIndex== strings.Length)
                    {
                        enter = "";
                    }
                    textBoxPath.AppendText(item+ enter);
                    textBoxName.AppendText(Path.GetFileNameWithoutExtension(item)+ enter);
                    pathIndex++;
                }
                
            }
        }
        /// <summary>
        /// 刷新列表
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonRefresh_Click(object sender, EventArgs e)
        {
            InitList();
        }
        /// <summary>
        /// 添加检测项
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonAdd_Click(object sender, EventArgs e)
        {
            if (textBoxName.Text.IsNullOrEmpty())
            {
                MessageBoxTD.Show($"检测项名称不能为空！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }
            if(Solution.Instance.DetectItemDic == null)
                Solution.Instance.DetectItemDic = new Dictionary<string, List<DetectItemInfo>>();

            string[] paths = textBoxPath.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);
            string[] names = textBoxName.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None);

            //判断有没有重复项
            bool isAnyExist = textBoxName.Text.Split(new string[] { Environment.NewLine }, StringSplitOptions.None)
                                             .Any(name => !string.IsNullOrWhiteSpace(name) && Solution.Instance.DetectItemDic.ContainsKey(name));
            if (isAnyExist)
            {
                DialogResult dr = MessageBoxTD.Show($"该检测项已存在！是否覆盖？", "已存在提示", MessageBoxButtons.OKCancel, MessageBoxIcon.Warning);
                if (dr == DialogResult.Cancel || dr == DialogResult.None)
                    return;
                else
                {
                    for (int i = 0; i < names.Length; i++)
                    {
                        // 移除界面上的
                        foreach (var ctrl in panelList.Controls)
                        {
                            if (ctrl is DetectItemListBox listBox && listBox.ItemName == names[i])
                            {
                                panelList.Controls.Remove(listBox);
                                break;
                            }
                        }
                    }
                }
            }

            // 开始添加
            for(int i = 0;i<paths.Length;i++)
            {
                List<DetectItemInfo> infos = null;
                try
                {
                    string text = File.ReadAllText(paths[i]);
                    infos = JsonConvert.DeserializeObject<List<DetectItemInfo>>(text);// 反序列化配置
                    DetectItemLanguage.NormalizeItems(infos);
                }
                catch (Exception ex)
                {
                    MessageBoxTD.Show($"AI配置文件解析失败！原因：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                DetectItemListBox box = new DetectItemListBox();
                box.LoadConfig(names[i], infos);
                box.Dock = DockStyle.Top;
                box.Width = panelList.Width;
                panelList.Controls.Add(box);
                Solution.Instance.DetectItemDic[names[i]] = infos;
            }

            //List<DetectItemInfo> infos = null;
            //try
            //{
            //    string text = File.ReadAllText(textBoxPath.Text);
            //    infos = JsonConvert.DeserializeObject<List<DetectItemInfo>>(text);// 反序列化配置
            //}
            //catch (Exception ex)
            //{
            //    MessageBoxTD.Show($"AI配置文件解析失败！原因：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
            //    return;
            //}

            //DetectItemListBox box = new DetectItemListBox();
            //box.LoadConfig(textBoxName.Text, infos);
            //box.Dock = DockStyle.Top;
            //box.Width = panelList.Width;
            //panelList.Controls.Add(box);
            //Solution.Instance.DetectItemDic[textBoxName.Text] = infos;
        }
        /// <summary>
        /// 移除选择的检测项
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonDelete_Click(object sender, EventArgs e)
        {
            //倒序遍历删除法
            for (int i = panelList.Controls.Count - 1; i >= 0; i--)
            {
                var ctrl = panelList.Controls[i];
                if (ctrl is DetectItemListBox detectItem && detectItem.IsSelected)
                {
                    panelList.Controls.RemoveAt(i);
                    if (Solution.Instance.DetectItemDic.ContainsKey(detectItem.ItemName))
                        Solution.Instance.DetectItemDic.Remove(detectItem.ItemName);
                }
            }
        }
        /// <summary>
        /// 保存配置
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void buttonSave_Click(object sender, EventArgs e)
        {
            var dic = new Dictionary<string, List<DetectItemInfo>>();
            foreach (var ctrl in panelList.Controls)
            {
                if (ctrl is DetectItemListBox listBox)
                {
                    var (name, infos) = listBox.GetConfig();
                    if (!dic.ContainsKey(name))
                        dic.Add(name, infos);
                }
            }
            Solution.Instance.DetectItemDic = dic;
            if (Solution.Instance.SolFileName.IsNullOrEmpty())
            {
                DialogResult dr = MessageBoxTD.Show("尚未保存方案！是否先保存方案？", "错误", MessageBoxButtons.OKCancel, MessageBoxIcon.Error);
                if (dr == DialogResult.Cancel || dr == DialogResult.None)
                    return;
                else
                {
                    if (saveFileDialog1.ShowDialog() == DialogResult.OK)
                    {
                        Solution.Instance.SolFileName = saveFileDialog1.FileName;
                        Solution.Instance.Save(saveFileDialog1.FileName);
                        LogHelper.AddLog(MsgLevel.Info, $"方案保存成功！路径：{saveFileDialog1.FileName}", true);
                    }
                    else
                    {
                        MessageBoxTD.Show($"方案保存失败！原因：方案路径未选择！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    return;
                }
            }
            Solution.Instance.Save(Solution.Instance.SolFileName);
            LogHelper.AddLog(MsgLevel.Info, $"方案保存成功！路径：{Solution.Instance.SolFileName}", true);
        }

        /// <summary>
        /// 保存对话框
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void saveFileDialog1_FileOk(object sender, System.ComponentModel.CancelEventArgs e)
        {

        }



    /// <summary>
    /// 导出检测项（完整最终版）
    /// </summary>
    private void button1_Click(object sender, EventArgs e)
    {
        using (FolderBrowserDialog fbd = new FolderBrowserDialog())
        {
            fbd.Description = "请选择目标文件夹路径";
            fbd.ShowNewFolderButton = true;

            if (fbd.ShowDialog() == DialogResult.OK)
            {
                string selectPath = fbd.SelectedPath;
                if (!Directory.Exists(selectPath))
                {
                    MessageBox.Show("选中的文件夹路径无效！", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                try
                {
                    foreach (var kvp in Solution.Instance.DetectItemDic)
                    {
                        string fileName = $"{kvp.Key}.items";
                        string fullFilePath = Path.Combine(selectPath, fileName);
                        string jsonContent = JsonConvert.SerializeObject(
                            kvp.Value,
                            Formatting.Indented, 
                            new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore }
                        );
                        File.WriteAllText(fullFilePath, jsonContent, System.Text.Encoding.UTF8);
                    }

                    MessageBox.Show($"✅ 检测项导出完成！\r\n共导出 {Solution.Instance.DetectItemDic.Count} 个文件\r\n保存路径：{selectPath}",
                                    "成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"❌ 导出失败！原因：{ex.Message}", "错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
    }
}
}
