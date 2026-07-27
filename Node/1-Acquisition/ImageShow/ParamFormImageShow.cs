using Logger;
using OpenCvSharp.Extensions;
using Sunny.UI;
using System;
using System.Drawing;
using System.Windows.Forms;
using TDJS_Vision.Forms.ImageViewer;
using TDJS_Vision.Forms.YTMessageBox;

namespace TDJS_Vision.Node._1_Acquisition.ImageSource
{
    public partial class ParamFormImageShow : FormBase, INodeParamForm
    {
        public ParamFormImageShow()
        {
            InitializeComponent();
            BindLanguage();
            LoadWindowNameList();
            CanvasSet.WindowNumChangeEvent += CanvasSet_WindowNumChangeEvent;
            LanguageManager.LanguageChanged += LanguageManager_LanguageChanged;
            FormClosed += (s, e) => LanguageManager.LanguageChanged -= LanguageManager_LanguageChanged;
        }

        private void CanvasSet_WindowNumChangeEvent(object sender, int e)
        {
            LoadWindowNameList();
        }

        private void BindLanguage()
        {
            LanguageManager.Bind(this, "ImageViewer.ImageShowTitle");
            LanguageManager.Bind(WindowNameText, "ImageViewer.WindowName");
            LanguageManager.Bind(subImageText, "ImageViewer.SubscribeImage");
            LanguageManager.Bind(button1, "Common.OK");
            ApplyLanguage();
        }

        private void LanguageManager_LanguageChanged(object sender, EventArgs e)
        {
            ApplyLanguage();
            RefreshWindowNameListDisplay();
        }

        private void ApplyLanguage()
        {
            LanguageManager.Apply(this);
        }

        private void LoadWindowNameList()
        {
            string selectedKey = GetSelectedWindowKey();
            WindowNameList.Items.Clear();
            for (int i = 0; i < FrmImageViewer.FrmSingleImages.Count; i++)
            {
                WindowNameList.Items.Add(new ImageWindowListItem(FrmImageViewer.FrmSingleImages[i].FormName));
            }
            SelectWindowKey(selectedKey);
        }

        private void RefreshWindowNameListDisplay()
        {
            string selectedKey = GetSelectedWindowKey();
            LoadWindowNameList();
            SelectWindowKey(selectedKey);
        }

        private string GetSelectedWindowKey()
        {
            if (WindowNameList.SelectedItem is ImageWindowListItem selectedItem)
                return selectedItem.Key;

            return FrmSingleImage.NormalizeWindowKey(WindowNameList.Text);
        }

        private bool SelectWindowKey(string key)
        {
            key = FrmSingleImage.NormalizeWindowKey(key);
            for (int i = 0; i < WindowNameList.Items.Count; i++)
            {
                if (WindowNameList.Items[i] is ImageWindowListItem item && item.Key == key)
                {
                    WindowNameList.SelectedIndex = i;
                    return true;
                }
            }
            return false;
        }

        public INodeParam Params { get; set; }

        /// <summary>
        /// 用于节点参数界面需要订阅结果的情况调用
        /// </summary>
        /// <param name="node"></param>
        public void SetNodeBelong(NodeBase node)
        {
            nodeSubscription1.SetExpectedValueType<OutputImage>();
            nodeSubscription1.Init(node);
        }

        public void SetParam2Form()
        {
            if(Params is NodeParamImageShow param)
            {
                nodeSubscription1.SetText(param.Text1, param.Text2);
                param.WindowName = FrmSingleImage.NormalizeWindowKey(param.WindowName);
                if(!SelectWindowKey(param.WindowName))
                    throw new Exception(LanguageManager.T("ImageViewer.WindowNameNotFound"));
            }
        }

        /// <summary>
        /// 获取订阅的图片结果
        /// </summary>
        /// <returns></returns>
        public Bitmap GetImage()
        {
            return GetOutputImage().Bitmaps[0].ToBitmap();
        }

        public OutputImage GetOutputImage()
        {
            return nodeSubscription1.GetValue<OutputImage>();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (nodeSubscription1.GetText1().IsNullOrEmpty())
            {
                MessageBoxTD.Show(LanguageManager.T("ImageViewer.NoSubscribedResult"));
                LogHelper.AddLog(MsgLevel.Fatal, LanguageManager.T("ImageViewer.NoSubscribedResult"), true);
                return;
            }
            string windowKey = GetSelectedWindowKey();
            if (windowKey.IsNullOrEmpty())
            {
                MessageBoxTD.Show(LanguageManager.T("ImageViewer.WindowNameRequired"));
                LogHelper.AddLog(MsgLevel.Fatal, LanguageManager.T("ImageViewer.WindowNameRequired"), true);
                return;
            }

            NodeParamImageShow nodeParamImageShow = new NodeParamImageShow();
            nodeParamImageShow.WindowName = windowKey;
            nodeParamImageShow.Text1 = nodeSubscription1.GetText1();
            nodeParamImageShow.Text2 = nodeSubscription1.GetText2();
            Params = nodeParamImageShow;

            //NodeImageShow.ImageShowWindowNameChanged

            Hide();
        }

        private sealed class ImageWindowListItem
        {
            public ImageWindowListItem(string key)
            {
                Key = FrmSingleImage.NormalizeWindowKey(key);
            }

            public string Key { get; }

            public override string ToString()
            {
                return FrmSingleImage.GetWindowDisplayName(Key);
            }
        }
    }
}
