using System;
using System.Collections.Generic;
using System.Reflection;
using System.Windows.Forms;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._3_Detection.TDAI.Parse;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

/// <summary>独立进程保存、恢复参数，并验证真实窗体及RL12学习完成边界。</summary>
internal static class LearningSettingsTests
{
    /// <summary>验证条件，不满足时终止测试。</summary>
    private static void Check(bool value, string message)
    {
        if (!value) throw new Exception(message);
    }

    /// <summary>读取设计器中的真实文本框。</summary>
    private static TextBox Field(ParamFormTDAI form, string name)
    {
        return (TextBox)typeof(ParamFormTDAI).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(form);
    }

    /// <summary>写入与读取分别在独立进程运行，避免误把内存缓存当作重启持久化。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            int count = int.Parse(args[2]);
            float percentage = float.Parse(args[3]);
            if (args[0] == "write")
            {
                JsonProjectSerializer.SaveProject(new NodeParamTDAI { StudyNum = count, StudyPercentage = percentage,
                    NodeName = "学习参数回归", CurDetectItemName = "学习参数回归", IsAutoStudy = true }, args[1]);
                return 0;
            }
            var param = JsonProjectSerializer.LoadProject<NodeParamTDAI>(args[1]);
            Check(param.StudyNum == count && param.StudyPercentage == percentage, "独立进程恢复参数失败");
            // 延迟模型加载；本测试只执行参数窗体与解析器，不加载模型或连接设备。
            typeof(ParamFormTDAI).Assembly.GetType("TDJS_Vision.Startup.StartupAiRuntimeLoadGate")
                .GetMethod("BeginTDAIDeferral").Invoke(null, null);
            LanguageManager.SetLanguage("zh-CN", false);
            var node = new NodeTDAI(1, param.NodeName, new TDJS_Vision.Process("学习参数回归"), default(TDJS_Vision.Node.NodeType));
            using (var form = (ParamFormTDAI)node.ParamForm)
            {
                form.Params = param;
                form.SetParam2Form();
                Check(Field(form, "textBox_StudyNum").Text == count.ToString(), "次数回显错误");
                Check(float.Parse(Field(form, "textBox_studyPercentage").Text) == percentage, "百分率回显错误");
                var item = new DetectItemInfo { Name = "前铆脚长度", Enable = true, MinValue = "0", MaxValue = "0" };
                Solution.Instance.DetectItemDic[param.CurDetectItemName] = new List<DetectItemInfo> { item };
                for (int i = 1; i <= count; i++)
                {
                    var boxes = new List<DetResult> { new DetResult { ClassId = 1, Box = new Rect(0, 0, 100, 20) } };
                    var result = new NodeResultTDAI();
                    RL12Parse.Parse(boxes, 1, param, new TDJS_Vision.Process("学习参数回归"), ref result);
                    Check(param.IsAutoStudy == (i < count), "学习未在指定次数结束");
                }
                Check(Math.Abs(float.Parse(item.MinValue) - (100 - percentage)) < 0.01, "下限百分率错误");
                Check(Math.Abs(float.Parse(item.MaxValue) - (100 + percentage)) < 0.01, "上限百分率错误");
                form.SetParam2Form();
                Check(Field(form, "textBox_StudyNum").Text == count.ToString()
                    && float.Parse(Field(form, "textBox_studyPercentage").Text) == percentage, "学习完成后回显被覆盖");
            }
            Console.WriteLine("独立进程恢复、窗体回显、学习次数及上下限验证通过：" + count + "次 / " + percentage + "%");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
