using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._6_LogicTool.MultiCondition;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;

/// <summary>验证组合布尔文本的实际读取、失效输入、依赖、保存及窗体编辑。</summary>
internal static class CombinedBooleanOverlayTests
{
    /// <summary>定位真实绘制构建器，不执行相机或PLC。</summary>
    private static readonly MethodInfo Build = typeof(NodeResultOverlayDraw).Assembly
        .GetType("TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw.ResultOverlayDrawBuilder")
        .GetMethod("BuildDisplayResult", BindingFlags.Static | BindingFlags.NonPublic);

    /// <summary>失败时报告具体断言。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    /// <summary>读取测试窗体私有控件。</summary>
    private static T Field<T>(object target, string name) => (T)target.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);

    /// <summary>模拟参数编辑事件。</summary>
    private static object Call(object target, string name, params object[] args) => target.GetType()
        .GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, args);

    /// <summary>运行真值表和异常场景，再验证保存重载与可视布局。</summary>
    [STAThread]
    private static int Main()
    {
        try { Run(); return 0; }
        catch (Exception ex)
        {
            Console.WriteLine("失败：" + ex.GetType().Name + " " + ex.Message);
            if (ex.InnerException != null) Console.WriteLine("内部：" + ex.InnerException.Message);
            return 1;
        }
    }

    /// <summary>执行实际测试并输出阶段，便于定位环境或控件初始化失败。</summary>
    private static void Run()
    {
        LanguageManager.Initialize();
        var process = new Process("组合布尔回归") { HasCanvasGraph = true };
        using (var first = new NodeMultiCondition(1, "多条件判断", process, NodeType.MultiCondition))
        using (var second = new NodeBase(2, "AI检测", process, NodeType.AITD))
        using (var image = new NodeBase(3, "图像", process, NodeType.ImageSource))
        using (var owner = new NodeResultOverlayDraw(4, "绘制", process, NodeType.ResultOverlayDraw))
        {
            process.Nodes.AddRange(new NodeBase[] { first, second, image, owner });
            foreach (int id in new[] { 1, 2, 3 })
                process.Connections.Add(new ProcessConnection { FromNodeId = id, ToNodeId = 4 });
            typeof(NodeBase).GetProperty("Result").SetValue(image, new NodeResultImageSource());
            var item = new ResultOverlayDrawItem
            {
                ItemType = ResultOverlayDrawItemType.CombinedBooleanText,
                BooleanOkText = "OK", BooleanNgText = "NG", FontSize = 24,
                BooleanSources = new List<OverlayBooleanSource> {
                    new OverlayBooleanSource { SourceText1 = "1.多条件判断", SourceText2 = "条件结果" },
                    new OverlayBooleanSource { SourceText1 = "2.AI检测", SourceText2 = "判定OK" }
                }
            };
            var parameter = new NodeParamResultOverlayDraw {
                ImageText1 = "3.图像", ImageText2 = "输出图像",
                TextCoordinateMode = DisplayTextCoordinateMode.Control,
                OkColor = Color.Green, NgColor = Color.Red,
                Items = new List<ResultOverlayDrawItem> { item }
            };
            Func<AlgorithmResult> render = () => (AlgorithmResult)Build.Invoke(null, new object[] { owner, parameter, null });
            foreach (var mode in new[] { OverlayBooleanMatchMode.All, OverlayBooleanMatchMode.Any })
            foreach (bool a in new[] { false, true })
            foreach (bool b in new[] { false, true })
            {
                item.BooleanMatchMode = mode;
                typeof(NodeBase).GetProperty("Result").SetValue(first, new NodeResultMultiCondition { ConditionResult = a });
                typeof(NodeBase).GetProperty("Result").SetValue(second, new NodeResultTDAI { JudgeOk = b });
                first.SetStatus(NodeStatus.Successful, "0");
                second.SetStatus(NodeStatus.Successful, "0");
                bool expected = mode == OverlayBooleanMatchMode.All ? a && b : a || b;
                var actual = render();
                Check(actual.IsAllOk == expected && actual.Texts.Single().Text == (expected ? "OK" : "NG"), "真值表错误");
                Check(actual.Texts[0].Color.ToArgb() == (expected ? Color.Green : Color.Red).ToArgb(), "判定颜色错误");
            }
            Console.WriteLine("真值表通过");
            // OR已有真值也必须检查另一来源，不能将失败或旧值显示成OK。
            Action assertError = () => {
                var result = render();
                Check(!result.IsAllOk && result.Texts.Single().Text.StartsWith("组合判定异常：")
                    && result.Texts[0].Color == Color.Red, "失效来源未标为异常");
            };
            second.SetStatus(NodeStatus.Failed, "0");
            assertError();
            second.SetStatus(NodeStatus.Successful, "0");
            second.Active = false;
            assertError();
            second.Active = true;
            item.BooleanSources[1].SourceText2 = "RunTime";
            assertError();
            item.BooleanSources[1].SourceText2 = "不存在的结果";
            assertError();
            item.BooleanSources[1].SourceText2 = "判定OK";
            process.IsRuning = true;
            typeof(Process).GetProperty("CurrentRunId").SetValue(process, 10);
            assertError();
            process.IsRuning = false;
            var sources = item.BooleanSources;
            item.BooleanSources = new List<OverlayBooleanSource>();
            assertError();
            item.BooleanSources = sources;
            var clone = item.Clone();
            clone.BooleanSources[0].SourceText1 = "被修改";
            Check(item.BooleanSources[0].SourceText1 == "1.多条件判断", "编辑克隆修改了原参数");
            var restored = JsonConvert.DeserializeObject<NodeParamResultOverlayDraw>(JsonConvert.SerializeObject(parameter));
            Check(restored.Items[0].BooleanSources.Count == 2
                && restored.Items[0].BooleanMatchMode == OverlayBooleanMatchMode.Any, "组合配置保存丢失");
            owner.ParamForm.Params = restored;
            Check(owner.GetSubscriptionDependencyNodeIds().OrderBy(x => x).SequenceEqual(new[] { 1, 2, 3 }), "组合依赖未完整登记");
            restored.Items[0].Enabled = false;
            Check(owner.GetSubscriptionDependencyNodeIds().SequenceEqual(new[] { 3 }), "禁用项仍等待订阅");
            item.Enabled = false;
            Check(render().Texts.Count == 0, "禁用项仍绘制");
            item.Enabled = true;
            restored.Items[0].Enabled = true;
            Console.WriteLine("异常、保存、依赖测试通过");

            first.ParamForm.Params = new NodeParamMultiCondition {
                Conditions = new List<MultiConditionItem> {
                    new MultiConditionItem { Name = "线芯高度" },
                    new MultiConditionItem { Name = "端子数量" }
                }
            };
            Check(first.GetDynamicResultVariableNames().Count() == 2, "未运行时不能选择单项条件");
            Check(SubscriptionPortCatalog.GetOutputs(first, SubscriptionInputContract.ForType(typeof(bool)), true, null)
                .Any(o => o.PropertyPath == "$variable:单项结果：线芯高度"), "布尔选择器没有提供单项端口");
            var details = new List<NodeConditionEvaluation> {
                new NodeConditionEvaluation { Name = "线芯高度", IsEnabled = true, IsMatched = true },
                new NodeConditionEvaluation { Name = "端子数量", IsEnabled = true, IsMatched = false }
            };
            var conditionResult = new NodeResultMultiCondition { ConditionResult = false, Details = details };
            typeof(NodeBase).GetProperty("Result").SetValue(first, conditionResult);
            first.SetStatus(NodeStatus.Successful, "0");
            item.BooleanSources = new List<OverlayBooleanSource> {
                new OverlayBooleanSource { SourceText1 = "1.多条件判断", SourceText2 = "变量.单项结果：线芯高度" },
                new OverlayBooleanSource { SourceText1 = "1.多条件判断", SourceText2 = "变量.单项结果：端子数量" }
            };
            item.BooleanMatchMode = OverlayBooleanMatchMode.Any;
            Check(render().Texts.Single().Text == "OK", "单项组合误用了总结果");
            item.BooleanMatchMode = OverlayBooleanMatchMode.All;
            Check(render().Texts.Single().Text == "NG", "同一节点多个单项组合错误");
            details.Reverse();
            object value;
            Check(conditionResult.TryGetDynamicVariable("$variable:单项结果：线芯高度", out value) && (bool)value,
                "行顺序变化导致读取另一项");
            details[1].IsEnabled = false;
            assertError();
            details[1].IsEnabled = true;
            details[1].SourceDisabled = true;
            assertError();
            details[1].SourceDisabled = false;
            details.Add(new NodeConditionEvaluation { Name = "线芯高度", IsEnabled = true, IsMatched = false });
            assertError();
            details.RemoveAt(2);
            item.BooleanSources = sources;
            Console.WriteLine("单项端口、同节点多项、行顺序、停用来源及重名防错测试通过");

            var form = (NodeParamFormResultOverlayDraw)owner.ParamForm;
            form.SetParam2Form();
            var list = Field<ListBox>(form, "listBoxBooleanSources");
            Check(list.Items.Count == 2, "窗体未恢复两个订阅");
            list.SelectedIndex = 1;
            Call(form, "buttonRemoveBooleanSource_Click", form, EventArgs.Empty);
            Check(list.Items.Count == 1 && restored.Items[0].BooleanSources.Count == 2, "删除编辑影响已保存参数");
            var selector = Field<NodeSubscription>(form, "nodeSubscriptionSource");
            selector.SetText("2.AI检测", "判定OK");
            Call(form, "buttonAddBooleanSource_Click", form, EventArgs.Empty);
            Call(form, "buttonAddBooleanSource_Click", form, EventArgs.Empty);
            Check(list.Items.Count == 2, "来源添加失败或重复");
            Check((bool)Call(form, "SaveParams", false), "窗体保存失败");
            Check(((NodeParamResultOverlayDraw)form.Params).Items[0].BooleanSources.Count == 2, "保存丢失订阅");
            // 在屏幕外完成真实布局，避免隐藏窗体的DrawToBitmap只返回空白背景。
            form.ShowInTaskbar = false;
            form.StartPosition = FormStartPosition.Manual;
            form.Location = new Point(-10000, -10000);
            form.Show();
            Application.DoEvents();
            using (var bitmap = new Bitmap(form.Width, form.Height))
            {
                form.DrawToBitmap(bitmap, form.ClientRectangle);
                bitmap.Save("combined-boolean-preview.png");
            }
            form.Hide();
            Console.WriteLine("通过：AND/OR真值表、失败/禁用/错误类型/缺失/旧批次/空列表、克隆保存、完整依赖、界面增删去重及保存。");
        }
    }
}
