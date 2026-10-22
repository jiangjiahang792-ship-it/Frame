using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>使用真实程序集离线验证勒刚侧面业务及参数界面。</summary>
internal static class LeGangSideTests
{
    /// <summary>已完成的断言数量。</summary>
    private static int _checks;
    /// <summary>中文检测项键，顺序与发布模板一致。</summary>
    private static readonly string[] Names = { "后铆脚面积（侧面）", "飞丝存在（侧面）", "防水栓破损（侧面）" };
    /// <summary>断言业务行为。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
        _checks++;
    }
    /// <summary>创建独立节点及中文检测配置。</summary>
    private static NodeParamTDAI Parameter(string name)
    {
        Solution.Instance.DetectItemDic[name] = Names.Select((n, i) => new DetectItemInfo {
            Name = n, Enable = true, MinValue = i == 0 ? "1" : "0", MaxValue = i == 0 ? "99999" : "0",
            IsCountItem = i != 0, CurValue = "0" }).ToList();
        return new NodeParamTDAI { CurDetectItemName = name, NodeName = name, DetectItemName1 = name,
            IsFixed = true, Scale = 1, StudyNum = 2, StudyPercentage = 0, ModelName = ModelName.勒刚端子侧面15类OBB模型 };
    }
    /// <summary>创建给定类别的OBB模拟检出。</summary>
    private static ObbResult Box(int id, float x = 500, float y = 500, float w = 20, float h = 10)
    {
        return new ObbResult { class_id = id, center_x = x, center_y = y, width = w, height = h, angle = 0.3f };
    }
    /// <summary>执行真实解析器，支持空列表及空引用。</summary>
    private static NodeResultTDAI Parse(NodeParamTDAI p, params ObbResult[] boxes)
    {
        var result = new NodeResultTDAI();
        TerminalSide_C15_Obb.Parse(boxes == null ? null : boxes.ToList(), 0, p, ref result);
        return result;
    }
    /// <summary>读取检测项当前结果。</summary>
    private static float Value(NodeResultTDAI r, int i)
    {
        return float.Parse(r.AlgorithmResult.DetectResults[Names[i]][0].Value);
    }
    /// <summary>验证类别、过滤边界、缺失清零、换算补偿及缺失红框。</summary>
    private static void VerifyParsing()
    {
        var p = Parameter("侧面解析");
        var items = Solution.Instance.DetectItemDic[p.CurDetectItemName];
        var r = Parse(p, Box(4), Box(8, 400, 450), Box(8, 800), Box(11), Box(10));
        Check(Value(r, 0) == 200 && Value(r, 1) == 1 && Value(r, 2) == 1, "类别或飞丝过滤错误");
        Check(!r.IsOk && r.DetectItemCount == 3 && r.AlgorithmResult.Texts.Count == 3, "结果不完整");
        Check(r.AlgorithmResult.Rects.Count == 2, "同框合并错误");
        Check(r.AlgorithmResult.Rects.All(x => x.Color == Color.Red), "NG同框未合并为红色");
        p.NeedConvert = true; p.Scale = 0.1f; items[0].Compensation = 2;
        r = Parse(p, Box(4), Box(8, 800), Box(11));
        Check(Value(r, 0) == 22 && Value(r, 1) == 1 && Value(r, 2) == 1, "面积换算补偿或数量隔离错误");
        r = Parse(p, (ObbResult[])null);
        Check(Value(r, 0) == 0 && Value(r, 1) == 0 && Value(r, 2) == 0, "空结果没有清零");
        Check(!r.IsOk && r.AlgorithmResult.Rects.Count == 1, "面积缺失红框或正常缺陷补框错误");
        Check(items.All(i => i.CurValue == "0"), "当前值残留");
        r = Parse(p, Box(8, 1, 1));
        Check(Value(r, 1) == 1, "没有后铆脚时错误过滤了原点附近飞丝");
        r = Parse(p, Box(4), Box(8, 390, 435), Box(8, 520, 500), Box(8, 500, 545));
        Check(Value(r, 1) == 2, "飞丝过滤边界错误");
        items[0].Enable = false;
        Check(Parse(p).DetectItemCount == 2, "禁用项仍然输出");
        items[0].Enable = true; items[0].MinValue = "0";
        Check(Parse(p).IsOk, "缺失0应按上下限判定");
        items[0].MinValue = "1";
        Check(!Parse(p, Box(4, w: float.NaN)).IsOk, "无效几何参与测量");
        r = Parse(p, Box(4), Box(4, 900, w: 40));
        Check(r.AlgorithmResult.DetectResults[Names[0]].Count == 2 && items[0].CurValue == "22.00,42.00", "多目标面积丢失");
        p.Scale = 0;
        bool rejected = false;
        try { Parse(p); } catch (InvalidOperationException) { rejected = true; }
        Check(rejected, "无效比例没有拒绝");
    }
    /// <summary>验证多节点学习隔离、完成事件、无防水栓禁用和切换配置清空历史。</summary>
    private static void VerifyLearning()
    {
        var a = Parameter("侧面学习甲"); var b = Parameter("侧面学习乙");
        a.IsAutoStudy = b.IsAutoStudy = true;
        int closed = 0;
        EventHandler<string> handler = (sender, name) => closed++;
        ParseCommon.CloseAutoStudyEvent += handler;
        try
        {
            Check(Parse(a, Box(4), Box(10)).IsOk, "学习未默认OK");
            Parse(b, Box(4, w: 40), Box(10));
            Check(a.IsAutoStudy && b.IsAutoStudy, "节点学习计数相互污染");
            Parse(a, Box(4), Box(10));
            Check(!a.IsAutoStudy && b.IsAutoStudy && closed == 1, "学习完成事件或隔离错误");
            Check(Solution.Instance.DetectItemDic[a.CurDetectItemName][0].MinValue == "200.00", "甲面积学习错误");
            Parse(b, Box(4, w: 40), Box(10));
            Check(Solution.Instance.DetectItemDic[b.CurDetectItemName][0].MinValue == "400.00", "乙混入甲样本");
            a.IsAutoStudy = true; a.StudyNum = 1;
            Parse(a, Box(4, w: 30));
            Check(!Solution.Instance.DetectItemDic[a.CurDetectItemName][2].Enable, "无防水栓未禁用破损项");
            Check(Solution.Instance.DetectItemDic[a.CurDetectItemName][0].MinValue == "300.00", "新会话沿用旧样本");
            var c = Parameter("侧面切换配置");
            a.CurDetectItemName = c.CurDetectItemName;
            Check(Parse(a).AlgorithmResult.Rects.Count == 0, "切换配置沿用旧红框");
        }
        finally { ParseCommon.CloseAutoStudyEvent -= handler; }
    }
    /// <summary>验证设计器模型选项、参数恢复和发布模板。</summary>
    private static void VerifyForm()
    {
        Check((int)ModelName.新端子12类模型 == 10, "旧枚举值改变");
        var p = Parameter("侧面界面");
        var saved = JsonConvert.DeserializeObject<NodeParamTDAI>(JsonConvert.SerializeObject(p));
        Check(saved.ModelName == p.ModelName, "模型序列化失败");
        using (var form = new ParamFormTDAI())
        {
            var combo = (ComboBox)typeof(ParamFormTDAI).GetField("comboBoxModelName", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
            Check(combo.Items.Contains("勒刚端子侧面15类OBB模型"), "设计器没有模型选项");
            form.Params = saved;
            var gate = typeof(NodeParamTDAI).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate");
            using ((IDisposable)gate.GetMethod("BeginTDAIDeferral").Invoke(null, null)) form.SetParam2Form();
            Check(combo.Text == "勒刚端子侧面15类OBB模型", "模型选项恢复失败");
        }
        var items = JsonConvert.DeserializeObject<List<DetectItemInfo>>(File.ReadAllText("Template/TDAI/勒刚端子侧面15类检测项.items"));
        Check(items.Select(i => i.Name).SequenceEqual(Names), "模板名称错误");
        Check(items.Skip(1).All(i => i.IsCountItem && i.MinValue == "0" && i.MaxValue == "0"), "缺陷模板阈值错误");
    }
    /// <summary>执行离线验证，不加载模型或连接相机。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            LanguageManager.SetLanguage("zh-CN", false);
            VerifyParsing(); VerifyLearning(); VerifyForm();
            Console.WriteLine("勒刚侧面离线验证通过，共" + _checks + "项断言。"); return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
