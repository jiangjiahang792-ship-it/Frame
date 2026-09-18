using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using TDJS_Vision.Node._3_Detection.ContourMatch;

/// <summary>用发布程序集的真实节点窗体显示现场图像和匹配结果，不创建任何设备流程。</summary>
internal static class ContourLivePreview
{
    /// <summary>恢复已经验证的模型，运行一次后保留可交互的执行及模板编辑界面。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            string directory = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "artifacts", "VMComparison"));
            string parameterPath = args.Length > 0 ? args[0] : Path.Combine(directory, "parameters.json");
            string imagePath = args.Length > 1 ? args[1] : Path.Combine(directory, "images", "original-000.png");
            var parameters = JsonConvert.DeserializeObject<NodeParamContourMatch>(File.ReadAllText(parameterPath));
            parameters.Text1 = string.Empty; parameters.Text2 = string.Empty; parameters.SourceNodeId = 0;
            using (var form = new NodeParamFormContourMatch())
            {
                form.Params = parameters;
                form.Text = "轮廓模板匹配 · 工位3验证";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.Shown += (sender, eventArgs) => {
                    var flags = BindingFlags.Instance | BindingFlags.NonPublic;
                    typeof(NodeParamFormContourMatch).GetMethod("SetImage", flags)
                        .Invoke(form, new object[] { ImageFrame.Load(imagePath) });
                    typeof(NodeParamFormContourMatch).GetMethod("ExecuteButton_Click", flags)
                        .Invoke(form, new object[] { form, EventArgs.Empty });
                };
                Application.Run(form);
            }
            return 0;
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.ToString(), "验证预览启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return 1;
        }
    }
}
