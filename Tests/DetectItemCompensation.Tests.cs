using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Forms.SolRunParam;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>检测项补偿的数据兼容、真实解析与表格编辑回归，不连接现场设备。</summary>
internal static class DetectItemCompensationTests
{
    /// <summary>通过的断言数量。</summary>
    private static int checks;
    /// <summary>验证条件并累计断言。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    /// <summary>读取最终输出，结果发送使用同一结果字典。</summary>
    private static float Value(NodeResultTDAI result, string name)
    {
        return float.Parse(result.AlgorithmResult.DetectResults[DetectItemLanguage.NormalizeName(name)][0].Value);
    }

    /// <summary>对三个DET解析入口验证补偿顺序、判定、学习及数量隔离。</summary>
    private static void VerifyDet(Type parser, string name, int id, string countName, int countId)
    {
        var item = new DetectItemInfo { Name = name, Enable = true, MinValue = "2.4", MaxValue = "2.6", Compensation = 0.5f };
        var count = new DetectItemInfo { Name = countName, Enable = true, MinValue = "0", MaxValue = "1", Compensation = 100 };
        string key = parser.Name;
        Solution.Instance.DetectItemDic[key] = new List<DetectItemInfo> { item, count };
        var param = new NodeParamTDAI { CurDetectItemName = key, NeedConvert = true, Scale = 0.1f, StudyNum = 1, StudyPercentage = 0 };
        var detections = new List<DetResult> {
            new DetResult { ClassId = id, Box = new Rect(10, 10, 20, 30) },
            new DetResult { ClassId = countId, Box = new Rect(50, 50, 5, 5) } };
        Func<List<DetResult>, NodeResultTDAI> parse = values =>
        {
            object[] args = { values, values.Count, param, new TDJS_Vision.Process("补偿验证"), new NodeResultTDAI() };
            parser.GetMethod("Parse").Invoke(null, args);
            return (NodeResultTDAI)args[4];
        };
        var result = parse(detections);
        Check(result.IsOk && Value(result, name) == 2.5f && item.CurValue == "2.50", key + "应先换算再补偿并按最终值判定");
        Check(Value(result, countName) == 1, key + "数量不应补偿");
        Check(result.AlgorithmResult.Texts.Any(t => t.Text.Contains("2.50mm")), key + "显示未使用最终值");
        param.IsAutoStudy = true;
        parse(detections);
        Check(item.MinValue == "2.50" && item.MaxValue == "2.50", key + "学习应采用同一补偿后的数值");
        item.Compensation = -0.5f;
        result = parse(detections);
        Check(!result.IsOk && Value(result, name) == 1.5f, key + "负补偿或判定错误");
        result = parse(new List<DetResult>());
        Check(Value(result, name) == 0 && Value(result, countName) == 0, key + "未检出不应叠加补偿");
    }

    /// <summary>验证SEG距离补偿和缺少依赖时的NG行为。</summary>
    private static void VerifySeg()
    {
        var item = new DetectItemInfo { Name = "L2_编组切割长度", Enable = true, MinValue = "5.4", MaxValue = "5.6", Compensation = 0.5f };
        Solution.Instance.DetectItemDic["SEG补偿"] = new List<DetectItemInfo> { item };
        var param = new NodeParamTDAI { CurDetectItemName = "SEG补偿", NeedConvert = true, Scale = 0.1f };
        var segments = new List<SegResult>();
        foreach (int id in new[] { 1, 3 })
            segments.Add(new SegResult { ClassId = id, Score = 1, Rect = new Rect(0, id == 1 ? 10 : 60, 20, 20),
                MaskGeometry = new SegMaskGeometry { Bounds = new Rect(0, id == 1 ? 10 : 60, 20, 20), UpperEdgeLowestY = id == 1 ? 10 : 60, Contours = new PointF[0][] } });
        var result = new NodeResultTDAI();
        ASTStation1SegParse.Parse(segments, 2, param, new TDJS_Vision.Process("验证"), ref result);
        Check(result.IsOk && Value(result, item.Name) == 5.5f, "SEG距离补偿错误");
        segments.RemoveAt(1);
        ASTStation1SegParse.Parse(segments, 1, param, new TDJS_Vision.Process("验证"), ref result);
        Check(!result.IsOk && Value(result, item.Name) == 0, "SEG缺少依赖不应补偿或判OK");
    }

    /// <summary>验证旧文件默认值、序列化、克隆以及设计器创建的真实表格列。</summary>
    private static void VerifyGrid()
    {
        var old = JsonConvert.DeserializeObject<DetectItemInfo>("{\"Name\":\"露线芯宽度\",\"MinValue\":\"1\",\"MaxValue\":\"9\",\"Enable\":true}");
        Check(old.Compensation == 0, "旧配置默认补偿不是0");
        old.Compensation = -0.25f;
        Check(JsonConvert.DeserializeObject<DetectItemInfo>(JsonConvert.SerializeObject(old)).Compensation == -0.25f, "序列化丢失补偿");
        foreach (var type in new[] { typeof(NodeTDAI), typeof(SolRunParamControl) })
        {
            var method = type.GetMethod("CloneDetectItems", BindingFlags.Static | BindingFlags.NonPublic);
            var clone = (List<DetectItemInfo>)method.Invoke(null, new object[] { new List<DetectItemInfo> { old } });
            Check(clone[0].Compensation == -0.25f, type.Name + "克隆丢失补偿");
        }
        using (var form = new Form { Size = new System.Drawing.Size(1000, 400), StartPosition = FormStartPosition.Manual, Location = new System.Drawing.Point(-20000, -20000) })
        using (var grid = new MyDataGridViewForm { Dock = DockStyle.Fill })
        {
            form.Controls.Add(grid);
            grid.LoadConfig(new List<DetectItemInfo> { old });
            form.Show(); Application.DoEvents();
            var table = (DataGridView)typeof(MyDataGridViewForm).GetField("dataGridView1", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(grid);
            Check(table.Columns.Count == 7 && table.Columns[5].Name == "Compensation" && table.Columns[6].Name == "IsCountItem", "补偿列不是倒数第二列");
            Check(Convert.ToSingle(table.Rows[0].Cells[5].Value) == -0.25f, "加载丢失补偿");
            table.Rows[0].Cells[5].Value = "0.75";
            var saved = grid.GetConfig();
            Check(saved[0].Compensation == 0.75f && old.Compensation == -0.25f, "编辑保存补偿失败或提前污染原配置");
            table.Rows[0].Cells[5].Value = "";
            Check(grid.GetConfig()[0].Compensation == 0, "空补偿未按0保存");
            table.Rows[0].Cells[5].Value = "NaN";
            Check(grid.GetConfig() == null, "非有限补偿不应保存");
            table.Rows[0].Cells[5].Value = "0.75";
            LanguageManager.SetLanguage("en-US", false);
            Check(table.Columns[5].HeaderText == "Compensation", "补偿列英文翻译失效");
            LanguageManager.SetLanguage("zh-CN", false);
            using (var image = new Bitmap(form.ClientSize.Width, form.ClientSize.Height))
            {
                form.DrawToBitmap(image, new Rectangle(System.Drawing.Point.Empty, image.Size));
                image.Save("DetectItemCompensation.preview.png");
            }
            grid.LoadRunParamItems(new List<RunParamGridItem> { new RunParamGridItem { SourceType = RunParamGridSourceType.MultiCondition, Info = old } });
            Check(table.Rows[0].Cells[5].ReadOnly, "多条件行不应允许修改无效补偿");
        }
    }

    /// <summary>执行真实程序集离线验证。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            VerifyDet(typeof(ASTStation1Parse), "铜环宽度", 2, "飞丝", 4);
            VerifyDet(typeof(ASTStation2Parse), "L6_箔剥宽度", 2, "飞丝", 6);
            VerifyDet(typeof(ASTStation3Parse), "露线芯宽度", 0, "压接不良", 3);
            VerifySeg(); VerifyGrid();
            Console.WriteLine("DetectItemCompensation: " + checks + " assertions passed.");
            return 0;
        }
        catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
    }
}
