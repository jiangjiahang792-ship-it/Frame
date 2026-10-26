using System;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Node;
using TDJS_Vision.Node._4_Measurement.BlobAnalysis;

/// <summary>使用真实算法验证连通域过滤、孔洞、输出模式和参数持久化。</summary>
internal static class BlobAreaFilterTests
{
    /// <summary>校验实际分析输出，并释放图像。</summary>
    private static void Check(Mat image, NodeParamBlobAnalysis param, int count, int area)
    {
        var result = (NodeResultBlobAnalysis)typeof(NodeBlobAnalysis).GetMethod("ExecuteAnalysis",
            BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { image, param, null });
        try
        {
            if (result.Count != count || result.Area != area || Cv2.CountNonZero(result.OutputImage.SrcImg) != area)
                throw new Exception("区域数量、面积或输出图像不一致。");
        }
        finally { result.OutputImage.Dispose(); }
    }

    /// <summary>读取真实窗体的设计器控件。</summary>
    private static T Field<T>(object form, string name) => (T)form.GetType().GetField(name,
        BindingFlags.Instance | BindingFlags.NonPublic).GetValue(form);

    /// <summary>执行离线回归，不连接生产设备。</summary>
    [STAThread]
    private static void Main()
    {
        using (var image = new Mat(40, 60, MatType.CV_8UC1, Scalar.Black))
        {
            Cv2.Rectangle(image, new Rect(2, 2, 2, 2), Scalar.White, -1);
            Cv2.Rectangle(image, new Rect(10, 2, 5, 5), Scalar.White, -1);
            Cv2.Rectangle(image, new Rect(25, 2, 10, 10), Scalar.White, -1);
            // 环形区域真实面积64，验证不能使用轮廓包围面积100过滤。
            Cv2.Rectangle(image, new Rect(27, 4, 6, 6), Scalar.Black, -1);
            var p = JsonConvert.DeserializeObject<NodeParamBlobAnalysis>("{\"MinGray\":255,\"MaxGray\":255}");
            Check(image, p, 3, 93);
            p.EnableAreaFilter = true; p.MinArea = 25; p.MaxArea = 64;
            Check(image, p, 2, 89);
            p.OutputRegionMode = BlobOutputRegionMode.Max; Check(image, p, 1, 64);
            p.OutputRegionMode = BlobOutputRegionMode.Min; Check(image, p, 1, 25);
            p.MinArea = 26; p.MaxArea = 63; Check(image, p, 0, 0);
            p.MinArea = 64; p.MaxArea = 64; Check(image, p, 1, 64);
            p.EnableAreaFilter = false; p.OutputRegionMode = BlobOutputRegionMode.All;
            image.SetTo(Scalar.Black); Check(image, p, 0, 0);
            image.Set(1, 1, (byte)255); image.Set(2, 2, (byte)255);
            Check(image, p, 1, 2);
        }
        using (var form = new NodeParamFormBlobAnalysis())
        {
            form.Params = new NodeParamBlobAnalysis { EnableAreaFilter = true, MinArea = 25, MaxArea = 64 };
            form.SetParam2Form();
            if (!Field<CheckBox>(form, "checkBoxAreaFilter").Checked || Field<NumericUpDown>(form, "numericMinArea").Value != 25)
                throw new Exception("面积过滤参数未回显。");
            var saved = (NodeParamBlobAnalysis)form.GetType().GetMethod("BuildParamFromControls",
                BindingFlags.Instance | BindingFlags.NonPublic).Invoke(form, new object[] { false });
            saved = JsonConvert.DeserializeObject<NodeParamBlobAnalysis>(JsonConvert.SerializeObject(saved));
            if (!saved.EnableAreaFilter || saved.MinArea != 25 || saved.MaxArea != 64) throw new Exception("面积过滤参数保存失败。");
        }
        using (var node = new NodeBlobAnalysis(1, "Blob测试", null, NodeType.BlobAnalysis))
        {
            var outputs = SubscriptionPortCatalog.GetOutputs(node, null, false, "");
            if (!outputs.Any(o => o.PropertyPath == "Count" && o.DisplayName == "区域数量" && o.ValueType == typeof(int)))
                throw new Exception("订阅目录未提供区域数量。");
        }
        Console.WriteLine("Blob数量及面积过滤通过：边界、孔洞、全过滤、空图、八连通、最大最小、旧方案、保存恢复及真实订阅目录。");
    }
}
