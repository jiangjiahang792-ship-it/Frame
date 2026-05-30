using System.Drawing;
using System.Windows.Forms;
using WeifenLuo.WinFormsUI.Docking;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Forms.SolRunParam;
using System;
using System.Collections.Generic;
using System.Linq;
using Logger;
using OpenCvSharp;
using OpenCvSharp.Extensions;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Forms.YTMessageBox;
using TDJS_Vision.Node;
using TDJS_Vision.Node._7_ResultProcessing.ImageDraw;

namespace TDJS_Vision.Forms.ImageViewer
{
    public partial class FrmSingleImage : DockContent
    {

        public string FormName;
        private bool _isBoundToProcess;
        private Process _process;
        private int? _lastImageShowNodeId;
        // 记录每个流程窗口上一次选择的AI节点，避免同一窗口反复弹出选择框。
        private static readonly Dictionary<string, int> AiNodeSelectionCache = new Dictionary<string, int>();

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

            // 同一个窗口可能被多个图像显示节点复用，这里记录真正刷新当前画面的节点。
            if (sender is NodeImageShow nodeShow)
            {
                _process = nodeShow.Process;
                _lastImageShowNodeId = nodeShow.ID;
                _isBoundToProcess = true;
            }

            SetViewerImage(e.Bitmap, e.DisplayResult);
        }

        private void SetViewerImage(Bitmap bitmap, AlgorithmResult displayResult = null)
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

                showImageControl1.SetImage(bitmap, displayResult);
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

        /// <summary>
        /// 根据当前图像窗口解析按钮应该操作的AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="selectionCanceled"></param>
        /// <returns></returns>
        private NodeTDAI ResolveTargetAiNode(Process process, out bool selectionCanceled)
        {
            selectionCanceled = false;
            if (process == null)
                return null;

            List<NodeTDAI> candidates = ResolveAiNodesByWindow(process);
            if (candidates.Count == 0)
                candidates = GetAllAiNodes(process);

            if (candidates.Count == 0)
                return null;

            if (candidates.Count == 1)
            {
                CacheSelectedAiNode(process, candidates[0]);
                return candidates[0];
            }

            NodeTDAI cachedNode = GetCachedAiNode(process, candidates);
            if (cachedNode != null)
                return cachedNode;

            NodeTDAI selectedNode = FrmSelectAiNode.SelectNode(this, process.ProcessName, candidates);
            if (selectedNode == null)
            {
                selectionCanceled = true;
                return null;
            }

            CacheSelectedAiNode(process, selectedNode);
            return selectedNode;
        }

        /// <summary>
        /// 从当前图像窗口的显示链路反查AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private List<NodeTDAI> ResolveAiNodesByWindow(Process process)
        {
            var result = new List<NodeTDAI>();

            if (_lastImageShowNodeId.HasValue)
            {
                NodeImageShow lastShowNode = process.Nodes
                    .OfType<NodeImageShow>()
                    .FirstOrDefault(node => node.ID == _lastImageShowNodeId.Value);

                if (lastShowNode?.ParamForm.Params is NodeParamImageShow lastShowParam &&
                    NormalizeWindowKey(lastShowParam.WindowName) == FormName)
                {
                    AddAiNodesFromSubscription(process, lastShowParam.Text1, result);
                    return DistinctAiNodes(result);
                }
            }

            foreach (var node in process.Nodes)
            {
                if (!(node is NodeImageShow nodeShow) ||
                    !(nodeShow.ParamForm.Params is NodeParamImageShow showParam) ||
                    NormalizeWindowKey(showParam.WindowName) != FormName)
                {
                    continue;
                }

                AddAiNodesFromSubscription(process, showParam.Text1, result);
            }

            return DistinctAiNodes(result);
        }

        /// <summary>
        /// 从订阅节点继续解析AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="nodeText"></param>
        /// <param name="result"></param>
        private void AddAiNodesFromSubscription(Process process, string nodeText, List<NodeTDAI> result)
        {
            if (!TryGetNodeByText(process, nodeText, out NodeBase sourceNode))
                return;

            if (sourceNode is NodeTDAI aiNode)
            {
                result.Add(aiNode);
                return;
            }

            if (sourceNode is NodeImageDraw &&
                sourceNode.ParamForm.Params is NodeParamImageDraw drawParam)
            {
                AddAiNodeByText(process, drawParam.Text3, result);
                AddAiNodeByText(process, drawParam.Text5, result);
            }
        }

        /// <summary>
        /// 根据节点显示文本添加AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="nodeText"></param>
        /// <param name="result"></param>
        private void AddAiNodeByText(Process process, string nodeText, List<NodeTDAI> result)
        {
            if (TryGetNodeByText(process, nodeText, out NodeBase node) && node is NodeTDAI aiNode)
                result.Add(aiNode);
        }

        /// <summary>
        /// 获取流程中所有AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private static List<NodeTDAI> GetAllAiNodes(Process process)
        {
            return process.Nodes.OfType<NodeTDAI>().ToList();
        }

        /// <summary>
        /// 按节点ID去重AI检测节点。
        /// </summary>
        /// <param name="nodes"></param>
        /// <returns></returns>
        private static List<NodeTDAI> DistinctAiNodes(IEnumerable<NodeTDAI> nodes)
        {
            return nodes
                .Where(node => node != null)
                .GroupBy(node => node.ID)
                .Select(group => group.First())
                .ToList();
        }

        /// <summary>
        /// 通过“ID.节点名”文本查找节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="nodeText"></param>
        /// <param name="node"></param>
        /// <returns></returns>
        private static bool TryGetNodeByText(Process process, string nodeText, out NodeBase node)
        {
            node = null;
            if (process == null || string.IsNullOrWhiteSpace(nodeText))
                return false;

            int dotIndex = nodeText.IndexOf('.');
            if (dotIndex > 0 && int.TryParse(nodeText.Substring(0, dotIndex), out int nodeId))
                node = process.Nodes.FirstOrDefault(item => item.ID == nodeId);

            if (node == null)
                node = process.Nodes.FirstOrDefault(item => $"{item.ID}.{item.NodeName}" == nodeText);

            return node != null;
        }

        /// <summary>
        /// 获取当前窗口对应的AI节点缓存键。
        /// </summary>
        /// <param name="process"></param>
        /// <returns></returns>
        private string GetAiNodeCacheKey(Process process)
        {
            return $"{process.ProcessName}|{FormName}";
        }

        /// <summary>
        /// 缓存当前窗口选择的AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="aiNode"></param>
        private void CacheSelectedAiNode(Process process, NodeTDAI aiNode)
        {
            if (process == null || aiNode == null)
                return;

            AiNodeSelectionCache[GetAiNodeCacheKey(process)] = aiNode.ID;
        }

        /// <summary>
        /// 获取当前窗口缓存的AI检测节点。
        /// </summary>
        /// <param name="process"></param>
        /// <param name="candidates"></param>
        /// <returns></returns>
        private NodeTDAI GetCachedAiNode(Process process, List<NodeTDAI> candidates)
        {
            if (process == null || candidates == null)
                return null;

            if (!AiNodeSelectionCache.TryGetValue(GetAiNodeCacheKey(process), out int nodeId))
                return null;

            return candidates.FirstOrDefault(node => node.ID == nodeId);
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
                NodeTDAI targetAiNode = ResolveTargetAiNode(process, out bool selectionCanceled);
                if (selectionCanceled)
                    return;
                if (targetAiNode == null)
                {
                    MessageBoxTD.Show(LanguageManager.T("ImageViewer.AINodeNotConfigured"));
                    return;
                }

                using (var solRunParam = new SolRunParamControl())
                {
                    solRunParam.Init(process, targetAiNode);
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

            NodeTDAI targetAiNode = ResolveTargetAiNode(process, out bool selectionCanceled);
            if (selectionCanceled)
                return;

            using (var form = new FormSolRunParam(process.ProcessName, targetAiNode))
            {
                form.ShowDialog(this);
            }
        }
    }
    public class ImageShowPamra
    {
        public string WinName;
        public Bitmap Bitmap;
        public AlgorithmResult DisplayResult;
        public ImageShowPamra(string winname, Bitmap bitmap, AlgorithmResult displayResult = null)
        {
            WinName = FrmSingleImage.NormalizeWindowKey(winname);
            Bitmap = bitmap;
            DisplayResult = displayResult;
        }
    }
}
