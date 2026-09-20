using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using OpenCvSharp;
using TDJS_Vision;
using TDJS_Vision.Forms.DispShowImage;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._4_Measurement.TerminalAngle;
using TDJS_Vision.Node._4_Measurement.Common;
using TDJS_Vision.Node._4_Measurement.PositionCorrection;
using TDJS_Vision.Node._3_Detection.TDAI;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw;
using TDJS_Vision.Node._7_ResultProcessing.ResultOverlayDraw2;
using DrawingPoint = System.Drawing.Point;

/// <summary>使用实际程序集验证两 ROI 节点、图像资源、持久化和已测原图。</summary>
internal static class TerminalAngleTests
{
    /// <summary>累计断言数。</summary>
    private static int _checks;
    /// <summary>私有控件访问标志，仅用于离线验证。</summary>
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic;
    /// <summary>断言失败时停止，不把未执行伪装为成功。</summary>
    private static void Check(bool condition, string message) { _checks++; if (!condition) throw new Exception(message); }
    /// <summary>获取真实设计器控件或编辑状态。</summary>
    private static T Field<T>(object target, string name) { return (T)target.GetType().GetField(name, Flags).GetValue(target); }
    /// <summary>经真实按钮事件链执行操作，能发现设计器丢失事件绑定的问题。</summary>
    private static void Click(object target, string name)
    {
        if (name.EndsWith("Button_Click"))
        {
            string field=char.ToLowerInvariant(name[0])+name.Substring(1,name.Length-7);
            Field<Button>(target,field).PerformClick();
        }
        else target.GetType().GetMethod(name, Flags).Invoke(target, new object[] { null, EventArgs.Empty });
        Application.DoEvents();
    }
    /// <summary>测试用参数，端子纵向占满搜索区。</summary>
    private static NodeParamTerminalAngle Settings()
    {
        return new NodeParamTerminalAngle { Text1 = "1.输入图像", Text2 = "输出图像",
            TerminalRoi = new TerminalAngleRoi { Left = 30, Right = 370, Top = 50, Bottom = 850 },
            BaseRoi = new TerminalAngleRoi { Left = 150, Right = 250, Top = 870, Bottom = 920 }, MinimumWidth = 20 };
    }
    /// <summary>创建已知倾角的平行侧边端子，底部基准不参与方向拟合。</summary>
    private static Mat Synthetic(double angle)
    {
        var image = new Mat(950, 410, MatType.CV_8UC3, Scalar.White);
        double slope = -Math.Tan(angle * Math.PI / 180);
        var vertices = new[] { new OpenCvSharp.Point((int)Math.Round(200 - 410*slope - 20),40),new OpenCvSharp.Point((int)Math.Round(200 - 410*slope + 20),40),
            new OpenCvSharp.Point((int)Math.Round(200 + 410*slope + 20),860),new OpenCvSharp.Point((int)Math.Round(200 + 410*slope - 20),860) };
        Cv2.FillConvexPoly(image, vertices, new Scalar(20,100,160)); return image;
    }
    /// <summary>创建被反光分割成多段的端子，用于验证左右轮廓拟合不依赖最大连通域。</summary>
    private static Mat SyntheticBroken(double angle)
    {
        var image = new Mat(950, 410, MatType.CV_8UC3, Scalar.White);
        double slope = -Math.Tan(angle * Math.PI / 180);
        Func<double,double> center = y => 200 + (y - 450) * slope;
        foreach (var segment in new[] { Tuple.Create(40.0, 250.0), Tuple.Create(330.0, 600.0), Tuple.Create(680.0, 860.0) })
        {
            double top = segment.Item1, bottom = segment.Item2;
            var vertices = new[]
            {
                new OpenCvSharp.Point((int)Math.Round(center(top) - 20), (int)top),
                new OpenCvSharp.Point((int)Math.Round(center(top) + 20), (int)top),
                new OpenCvSharp.Point((int)Math.Round(center(bottom) + 20), (int)bottom),
                new OpenCvSharp.Point((int)Math.Round(center(bottom) - 20), (int)bottom)
            };
            Cv2.FillConvexPoly(image, vertices, new Scalar(20, 100, 160));
        }
        return image;
    }
    /// <summary>验证符号、独立基准、非法输入与失败空值。</summary>
    private static void Geometry()
    {
        var algorithm = new OpenCvTerminalAngleMeasurer();
        foreach (double angle in new[] { -15.0,-5.0,0.0,5.0,15.0 })
            using (var image = Synthetic(angle))
            {
                var config = Settings(); var r = algorithm.Measure(image,config,CancellationToken.None);
                Check(r.Success && Math.Abs(r.Angle.Value-angle)<0.12, "合成角度错误："+angle+" / "+r.Message);
                var display=TerminalAngleDisplay.Build(config,r);
                Check(display.Lines.Count==2 && display.Lines.All(line=>!line.ShowCenterCross),"单目标结果未限定为无辅助标记的两条中线。");
                config.BaseRoi.Left += 30; config.BaseRoi.Right += 30;
                var shifted = algorithm.Measure(image,config,CancellationToken.None);
                Check(shifted.Angle == r.Angle, "移动基座 ROI 改变了固定垂线倾角。");
            }
        using (var broken = SyntheticBroken(7.0))
        {
            var r = algorithm.Measure(broken, Settings(), CancellationToken.None);
            Check(r.Success && Math.Abs(r.Angle.Value - 7.0) < 0.18, "分段端子左右轮廓拟合失败：" + r.Angle + " / " + r.Message);
        }
        using(var blank = new Mat(950,410,MatType.CV_8UC1,Scalar.White))
        {
            var r=algorithm.Measure(blank,Settings(),CancellationToken.None);
            Check(!r.Success && !r.Angle.HasValue,"空图输出了有效角度。");
            Check(TerminalAngleDisplay.Build(Settings(),r).Lines.Count==0,"失败结果残留 ROI 或中线。");
            Cv2.Rectangle(blank,new Rect(30,40,40,820),Scalar.Black,-1);
            r=algorithm.Measure(blank,Settings(),CancellationToken.None);
            Check(!r.Success && r.Message.Contains("边界"),"触边没有拒绝。");
            blank.SetTo(Scalar.White); Cv2.Rectangle(blank,new Rect(180,100,40,350),Scalar.Black,-1);
            r=algorithm.Measure(blank,Settings(),CancellationToken.None);
            Check(!r.Success && r.Message.Contains("高度"),"断裂主体没有拒绝。");
            var config=Settings(); config.TerminalRoi.Right=410;
            Check(!algorithm.Measure(blank,config,CancellationToken.None).Success,"越界没有拒绝。");
            config=Settings(); config.BaseRoi=null;
            Check(!algorithm.Measure(blank,config,CancellationToken.None).Success,"缺第二 ROI 没有拒绝。");
            try { algorithm.Measure(blank,Settings(),new CancellationToken(true)); throw new Exception("取消未传播。"); }
            catch(OperationCanceledException) { _checks++; }
        }
        Console.WriteLine("已知角度、固定基准、空图、触边、断裂、缺失 ROI 与取消验证通过。");
    }
    /// <summary>沿用真实方案多态转换器保存并还原新节点。</summary>
    private static NodeParamTerminalAngle RoundTrip(NodeParamTerminalAngle config)
    {
        var node=new NodeConfig { NodeParam=config };
        var json=JsonConvert.SerializeObject(node);
        var restored=JsonConvert.DeserializeObject<NodeConfig>(json);
        Check(restored.NodeParam is NodeParamTerminalAngle,"方案参数多态类型丢失。");
        var result=(NodeParamTerminalAngle)restored.NodeParam;
        Check(result.TerminalRoi.Left==config.TerminalRoi.Left && result.BaseRoi.Bottom==config.BaseRoi.Bottom,"两 ROI 不能往返保存。");
        return result;
    }
    /// <summary>读取图像上的当前结果，替代已经删除的侧栏状态控件。</summary>
    private static string ImageText(NodeParamFormTerminalAngle form)
    {
        var display=Field<TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult>(form,"_lastDisplay");
        return display==null ? "" : string.Join("；",display.Texts.Select(text=>text.Text));
    }
    /// <summary>等待独立窗体的异步测量，不跳过真实按钮事件链。</summary>
    private static void ExecutePreview(NodeParamFormTerminalAngle form)
    {
        form.Show();Application.DoEvents();
        Click(form,"TestButton_Click");
        var deadline=DateTime.UtcNow.AddSeconds(5);
        while(Field<bool>(form,"_busy") && DateTime.UtcNow<deadline) { Application.DoEvents();Thread.Sleep(5); }
        Check(!Field<bool>(form,"_busy") && Field<TableLayoutPanel>(form,"sidebar").Enabled,"预览完成后未恢复操作。");
        Check(Field<ShowImageControl>(form,"viewer").GetAllDynamicRects().Count==0 && !Field<bool>(form,"_drawing"),"执行结果残留动态 ROI 框及名称。");
        var lines=Field<List<ShowImageControl.IRoiShape>>(Field<ShowImageControl>(form,"viewer"),"_staticRois").OfType<ShowImageControl.RoiLine>().ToList();
        Check(lines.All(line=>!line.ShowCenterCross && string.IsNullOrEmpty(line.Label)),"实际画图控件仍显示中心十字或 ROI 标签。");
    }
    /// <summary>调用真实结果绘制构建器，覆盖订阅复制和旋转而非只检查源数据。</summary>
    private static object OverlayCall(string builder,string method,params object[] arguments)
    {
        var type=typeof(NodeBase).Assembly.GetType("TDJS_Vision.Node._7_ResultProcessing."+builder,true);
        return type.GetMethods(BindingFlags.Static|BindingFlags.NonPublic).Single(candidate=>candidate.Name==method && candidate.GetParameters().Length==arguments.Length).Invoke(null,arguments);
    }
    /// <summary>两版 ROI 结果绘制都必须保留纯中线，且已有线段仍默认显示中心标记。</summary>
    private static void VerifyOverlay(TDJS_Vision.Process process)
    {
        using(var owner=new NodeBase(3,"ROI结果绘制",process,NodeType.ResultOverlayDraw))
        {
            process.Nodes.Add(owner);
            var connection=new ProcessConnection {FromNodeId=2,ToNodeId=3};process.Connections.Add(connection);
            try
            {
                var config=new NodeParamResultOverlayDraw {Items=new List<ResultOverlayDrawItem>{new ResultOverlayDrawItem {ItemType=ResultOverlayDrawItemType.Roi,SourceText1="2.端子角度",SourceText2="算法结果"}}};
                var display=(AlgorithmResult)OverlayCall("ResultOverlayDraw.ResultOverlayDrawBuilder","BuildDisplayResult",owner,config,null);
                Check(display.Lines.Count==2 && display.Lines.All(line=>!line.ShowCenterCross) && display.Rects.Count==0,"ROI结果绘制未保留纯中线。");
                var rotated=(AlgorithmResult)OverlayCall("ResultOverlayDraw.ResultOverlayDrawBuilder","RotateDisplayResult",display,410,950,90);
                Check(rotated.Lines.Count==2 && rotated.Lines.All(line=>!line.ShowCenterCross),"旋转结果恢复了中心标记。");
                var config2=new NodeParamResultOverlayDraw2 {Items=new List<ResultOverlayDraw2Item>{new ResultOverlayDraw2Item {ItemType=ResultOverlayDraw2ItemType.Roi,SourceText1="2.端子角度",SourceText2="算法结果"}}};
                var state=OverlayCall("ResultOverlayDraw2.ResultOverlayDraw2Builder","ResolveColorState",owner,config2);
                var display2=(AlgorithmResult)OverlayCall("ResultOverlayDraw2.ResultOverlayDraw2Builder","BuildDisplayResult",owner,config2,state);
                Check(display2.Lines.Count==2 && display2.Lines.All(line=>!line.ShowCenterCross) && display2.Rects.Count==0,"ROI结果绘制2未保留纯中线。");
                Check(new ColorLine(PointF.Empty,new PointF(10,10),Color.Lime).ShowCenterCross,"改变了其他节点的线段默认显示行为。");
            }
            finally {process.Connections.Remove(connection);process.Nodes.Remove(owner);}
        }
    }
    /// <summary>通过真实订阅读取和两版绘制器验证一位小数、零值、多目标和数值精度。</summary>
    private static void SubscriptionText()
    {
        var process=new TDJS_Vision.Process("端子订阅文本验证");
        using(var source=new NodeBase(2,"端子角度",process,NodeType.TerminalAngle))
        using(var owner=new NodeBase(3,"结果绘制",process,NodeType.ResultOverlayDraw))
        {
            process.Nodes.Add(source);process.Nodes.Add(owner);
            process.Connections.Add(new ProcessConnection {FromNodeId=2,ToNodeId=3});
            var result=new NodeResultTerminalAngle {Angle=5.018,Success=true};
            typeof(NodeBase).GetProperty("Result").SetValue(source,result);
            foreach(string output in new[]{"端子角度","Angle"})
            {
                foreach(bool multiple in new[]{false,true})
                {
                    result.Items=new List<TerminalAngleTargetResult>();
                    if(multiple)foreach(double? value in new double?[]{5.018,-1.234,0.0,null})
                        result.Items.Add(new TerminalAngleTargetResult {Angle=value,IsOk=value.HasValue});
                    string expected=multiple?"[5.0,-1.2,0.0,]":"5.0";
                    var config=new NodeParamResultOverlayDraw {Items=new List<ResultOverlayDrawItem>{new ResultOverlayDrawItem {ItemType=ResultOverlayDrawItemType.Text,SourceText1="2.端子角度",SourceText2=output,UseManualText=false,TextPrefix=""}}};
                    var display=(AlgorithmResult)OverlayCall("ResultOverlayDraw.ResultOverlayDrawBuilder","BuildDisplayResult",owner,config,null);
                    Check(display.Texts.Any(text=>text.Text==expected),"结果绘制订阅角度精度错误："+string.Join(";",display.Texts.Select(text=>text.Text)));
                    var config2=new NodeParamResultOverlayDraw2 {Items=new List<ResultOverlayDraw2Item>{new ResultOverlayDraw2Item {ItemType=ResultOverlayDraw2ItemType.Text,SourceText1="2.端子角度",SourceText2=output,UseManualText=false,TextPrefix=""}}};
                    var state=OverlayCall("ResultOverlayDraw2.ResultOverlayDraw2Builder","ResolveColorState",owner,config2);
                    var display2=(AlgorithmResult)OverlayCall("ResultOverlayDraw2.ResultOverlayDraw2Builder","BuildDisplayResult",owner,config2,state);
                    Check(display2.Texts.Any(text=>text.Text==expected),"结果绘制2订阅角度精度错误："+string.Join(";",display2.Texts.Select(text=>text.Text)));
                    Check(result.Angle==5.018 && (!multiple || result.Items[1].Angle==-1.234),"文本格式化改变了数值型订阅精度。");
                }
            }
            string formatted;
            Check(result.TryFormatSubscriptionText("端子角度",null,out formatted) && formatted=="","无效角度被格式化为零。");
            Check(!result.TryFormatSubscriptionText("算法耗时",1.234,out formatted),"修改了其他订阅结果的精度。");
            Console.WriteLine("两版结果绘制真实订阅、中文和属性名、单值/多目标一位小数及原始精度验证通过。");
        }
    }
    /// <summary>通过真实确定按钮验证未配置、缺图、缺区域、无定位、非法输入和忙碌均可关闭。</summary>
    private static void ConfirmCloses()
    {
        using(var form=new NodeParamFormTerminalAngle())
        using(var image=Synthetic(5))
        {
            form.StartPosition=FormStartPosition.Manual;form.Location=new DrawingPoint(-20000,-20000);form.ShowInTaskbar=false;
            for(int scenario=0;scenario<6;scenario++)
            {
                var saved=scenario==0?null:Settings();
                if(scenario==2) {saved.TerminalRoi=null;saved.BaseRoi=null;}
                if(scenario==3) saved.UsePositionCorrection=true;
                form.Params=saved;form.SetParam2Form();form.Show();Application.DoEvents();
                if(scenario>=2)form.LoadPreview(image,false);
                if(scenario==4)
                {
                    var editor=Field<TextBox>(form,"segmentationGaussianTextBox");editor.Text="NaN";editor.Focus();
                }
                using(var cancellation=new CancellationTokenSource())
                {
                    if(scenario==5)
                    {
                        form.GetType().GetField("_cancellation",Flags).SetValue(form,cancellation);
                        form.GetType().GetMethod("SetBusy",Flags).Invoke(form,new object[]{true});
                    }
                    Click(form,"SaveButton_Click");
                    Check(!form.Visible && !form.IsDisposed,"确定没有关闭并保留窗体实例，场景："+scenario);
                    if(scenario==4)Check(ReferenceEquals(saved,form.Params),"关闭时非法输入覆盖了原参数。");
                    if(scenario==5)
                    {
                        Check(cancellation.IsCancellationRequested,"忙碌关闭没有取消预览。");
                        form.GetType().GetField("_cancellation",Flags).SetValue(form,null);
                        form.GetType().GetMethod("SetBusy",Flags).Invoke(form,new object[]{false});
                    }
                }
                form.Show();Application.DoEvents();Check(form.Visible,"关闭后无法再次打开。");
                form.Hide();
            }
            form.Params=Settings();form.SetParam2Form();form.Show();Application.DoEvents();
            Field<TextBox>(form,"segmentationThresholdTextBox").Text="210";Click(form,"SaveButton_Click");
            Check(!form.Visible && ((NodeParamTerminalAngle)form.Params).Threshold==210,"关闭未保留有效编辑。");
        }
        Console.WriteLine("确定按钮：未配置、缺图、缺区域、无定位、非法输入、忙碌关闭及再次打开均通过。");
    }
    /// <summary>检查实际布局容纳标题字体和图标，并留足订阅控件显示空间。</summary>
    private static void CheckLayout(NodeParamFormTerminalAngle form)
    {
        var title=Field<TableLayoutPanel>(form,"tableLayoutPanelTiltle");
        Check(Field<TableLayoutPanel>(form,"root").Top>=title.Bottom,"内容覆盖标题栏。");
        foreach(Control child in title.Controls)
        {
            if(!child.Visible)continue;
            int height=child is Label ? child.GetPreferredSize(System.Drawing.Size.Empty).Height : child.Height;
            Check(child.Top>=0 && child.Bottom<=title.ClientSize.Height && child.Height>=height,"标题文字或图标被裁切："+child.Name);
        }
        foreach(string name in new[]{"imageSubscription","correctionSubscription"})
        {
            var subscription=Field<Control>(form,name);
            Check(subscription.Bottom<=subscription.Parent.ClientSize.Height-subscription.Parent.Padding.Bottom,"订阅下拉框被裁切："+name);
        }
    }
    /// <summary>验证真实刷新、绘制、执行、保存、原参数输入、资源和简化界面。</summary>
    private static void NodeAndUi(string artifacts)
    {
        var process=new TDJS_Vision.Process("端子角度离线验证");
        using(var sourceNode=new NodeBase(1,"输入图像",process,NodeType.ImageSource))
        using(var node=(NodeTerminalAngle)NodeFactory.CreateNode(2,"端子角度",process,NodeType.TerminalAngle))
        using(var image=Synthetic(5))
        {
            var owner=OutputImage.FromOwnedSingleImage(image.Clone());
            typeof(NodeBase).GetProperty("Result").SetValue(sourceNode,new NodeResultImageSource {OutputImage=owner});
            process.Nodes.Add(sourceNode);process.Nodes.Add(node);
            process.Connections.Add(new ProcessConnection {FromNodeId=1,ToNodeId=2});
            var form=(NodeParamFormTerminalAngle)node.ParamForm;
            form.SetNodeBelong(node);form.Params=RoundTrip(Settings());form.SetParam2Form();
            node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
            var result=(NodeResultTerminalAngle)node.Result;
            Check(result.Success && result.Items.Count==1 && Math.Abs(result.Angle.Value-5)<0.12,"单目标生产测量回归失败。");
            VerifyOverlay(process);
            Check(result.OutputImage.OwnedImageCount==0 && owner.ActiveLeaseCount==1,"节点复制整图或没有持有上游租约。");
            for(int i=0;i<8;i++)node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
            Check(owner.ActiveLeaseCount==1,"重复运行累积租约。");
            try {node.Run(new CancellationToken(true),false).GetAwaiter().GetResult();throw new Exception("生产取消未传播。");}
            catch(OperationCanceledException){_checks++;}
            Check(!((NodeResultTerminalAngle)node.Result).Angle.HasValue && owner.ActiveLeaseCount==0,"取消未清理旧结果。");
            form.StartPosition=FormStartPosition.Manual;form.Location=new DrawingPoint(-20000,-20000);form.ShowInTaskbar=false;
            form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
            CheckLayout(form);
            int run=process.CurrentRunId;Click(form,"RefreshButton_Click");
            Check(Field<Mat>(form,"_preview")!=null && process.CurrentRunId==run,"真实刷新没有读图或重跑上游。");
            foreach(string field in new[]{"correctionTarget","terminalGroup","baseGroup","resultGroup","terminalLeftTextBox","resultLabel","loadButton"})
                Check(form.GetType().GetField(field,Flags)==null,"界面仍有用户要求删除的控件："+field);
            var saved=(NodeParamTerminalAngle)form.Params;
            Field<TextBox>(form,"segmentationThresholdTextBox").Text="210";
            Check(saved.Threshold==220,"未确定的输入污染运行参数。");
            Click(form,"SaveButton_Click");
            Check(((NodeParamTerminalAngle)form.Params).Threshold==210,"真实确定按钮未保存输入。");
            Check(!form.Visible && !form.IsDisposed,"确定未关闭窗口或销毁了可复用窗体。");
            form.Show();Application.DoEvents();
            saved=(NodeParamTerminalAngle)form.Params;
            foreach(string value in new[]{"NaN","3.5","4",""})
            {
                Field<TextBox>(form,"segmentationGaussianTextBox").Text=value;Click(form,"SaveButton_Click");
                Check(ReferenceEquals(form.Params,saved),"非法高斯尺寸覆盖了保存参数。");
                Check(!form.Visible,"非法参数不应阻止确定关闭窗口。");
                form.Show();Application.DoEvents();
            }
            form.Params=Settings();form.SetParam2Form();Click(form,"RefreshButton_Click");
            var viewer=Field<ShowImageControl>(form,"viewer");
            Click(form,"TerminalButton_Click");Drag(viewer,new PointF(40,55),new PointF(355,840));
            Check(Math.Abs(viewer.GetAllDynamicRects().Single().Phi)<0.001,"普通新框发生旋转。");
            Click(form,"BaseButton_Click");Drag(viewer,new PointF(140,872),new PointF(260,923));
            ExecutePreview(form);
            Check(Field<TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult>(form,"_lastDisplay").Lines.Count==2,"绘制后执行未清除 ROI 结果轮廓。");
            Click(form,"SaveButton_Click");
            var drawn=(NodeParamTerminalAngle)form.Params;
            Check(Math.Abs(drawn.TerminalRoi.Left-40)<3 && Math.Abs(drawn.BaseRoi.Left-140)<3,"真实两框绘制没有保存。");
            ExecutePreview(form);
            Check(ImageText(form).Contains("角度"),"执行结果没有直接显示到图像。");
            var display=Field<TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult>(form,"_lastDisplay");
            Click(form,"SaveButton_Click");
            Check(ReferenceEquals(display,Field<TDJS_Vision.Node._3_Detection.TDAI.AlgorithmResult>(form,"_lastDisplay")),"确定覆盖了图像测量结果。");
            Check(!form.Visible && !form.IsDisposed,"执行后确定没有关闭窗口。");
            form.Show();Application.DoEvents();
            form.Refresh();Application.DoEvents();
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(artifacts,"端子角度节点-默认界面.png"));}
            form.Size=form.MinimumSize;Application.DoEvents();
            Check(Field<Button>(form,"saveButton").Right<=Field<FlowLayoutPanel>(form,"toolbar").ClientSize.Width,"最小窗体确定按钮被截断。");
            CheckLayout(form);
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(artifacts,"端子角度节点-最小界面.png"));}
            form.Scale(new SizeF(1.5F,1.5F));Application.DoEvents();CheckLayout(form);
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(artifacts,"端子角度节点-放大布局.png"));}
            form.Params=RoundTrip(drawn);form.SetParam2Form();Click(form,"RefreshButton_Click");
            Check(viewer.GetAllDynamicRects().Count==0,"重新加载方案还残留可回写的旧绘制框。");
            owner.Bitmaps[0].SetTo(Scalar.White);node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
            Check(!((NodeResultTerminalAngle)node.Result).Angle.HasValue,"失败帧沿用了旧角度。");
            node.Active=false;node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
            Check(owner.ActiveLeaseCount==0,"禁用后未释放租约。");
            Check(SubscriptionPortCatalog.GetStaticOutputs(typeof(NodeResultTerminalAngle)).Count==7,"全部目标结果未进入订阅目录。");
        }
        Console.WriteLine("真实按钮、界面精简、图像结果、参数校验与资源回归通过。");
    }
    /// <summary>在真实控件中拖动原图坐标，调用鼠标入口而非直接赋 ROI。</summary>
    private static void Drag(ShowImageControl viewer,PointF from,PointF to)
    {
        float scale=Field<float>(viewer,"_scale");var offset=Field<PointF>(viewer,"_offset");
        var a=new DrawingPoint((int)Math.Round(from.X*scale+offset.X),(int)Math.Round(from.Y*scale+offset.Y));
        var b=new DrawingPoint((int)Math.Round(to.X*scale+offset.X),(int)Math.Round(to.Y*scale+offset.Y));
        foreach(var item in new[]{Tuple.Create("OnMouseDown",a),Tuple.Create("OnMouseMove",b),Tuple.Create("OnMouseUp",b)})
            typeof(ShowImageControl).GetMethod(item.Item1,Flags).Invoke(viewer,new object[]{new MouseEventArgs(MouseButtons.Left,1,item.Item2.X,item.Item2.Y,0)});
        Application.DoEvents();
    }
    /// <summary>用同一原图和原参数比较节点裁剪优化与独立 Demo 的角度。</summary>
    private static void ActualImages(string demo,string artifacts)
    {
        var rows=new List<object>();var durations=new List<double>();double maximum=0;int count=0;
        var algorithm=new OpenCvTerminalAngleMeasurer();
        for(int group=1;group<=4;group++)
        {
            var session=JObject.Parse(File.ReadAllText(Path.Combine(demo,"对比验证","文件夹"+group,"测量结果.json")));
            var settings=(JObject)session["Settings"];
            foreach(JObject item in session["Images"])
            using(var image=Cv2.ImDecode(File.ReadAllBytes((string)item["SourcePath"]),ImreadModes.Unchanged))
            foreach(JObject terminal in item["Terminals"])
            {
                string prefix=(string)terminal["Name"]=="左端子"?"Left":"Right";
                JObject baseRoi=(JObject)settings["Alignment"][prefix+"Base"];
                var p=new NodeParamTerminalAngle {
                    TerminalRoi=new TerminalAngleRoi {Left=(double)settings[prefix+"Column"],Right=(double)settings[prefix+"EndColumn"],Top=(double)settings[prefix+"Top"],Bottom=(double)settings[prefix+"Bottom"]},
                    BaseRoi=new TerminalAngleRoi {Left=(double)baseRoi["Left"],Right=(double)baseRoi["Right"],Top=(double)baseRoi["Top"],Bottom=(double)baseRoi["Bottom"]},
                    Threshold=(double)settings["SilhouetteThreshold"],GaussianSize=(int)settings["GaussianSize"],MinimumWidth=(double)settings["MinimumWidth"],MaximumWidth=(double)settings["MaximumWidth"] };
                var r=algorithm.Measure(image,p,CancellationToken.None);
                double reference=(double)terminal["Alignment"]["RelativeAngleDegrees"];
                Check(r.Success,"现场图测量失败："+item["FileName"]+" "+prefix+" "+r.Message);
                double difference=Math.Abs(reference-r.Angle.Value); maximum=Math.Max(maximum,difference);count++;
                Check(difference<0.0001,"节点与独立 Demo 倾角不一致："+difference);
                durations.Add(r.AlgorithmMilliseconds);
                rows.Add(new { Group=group,File=(string)item["FileName"],Side=(string)terminal["Name"],Angle=r.Angle,Reference=reference,Difference=difference,Milliseconds=r.AlgorithmMilliseconds });
            }
        }
        Check(count==112,"现场图片数量与预期不符。");
        File.WriteAllText(Path.Combine(artifacts,"112端子节点验证.json"),JsonConvert.SerializeObject(rows,Formatting.Indented));
        Console.WriteLine(string.Format("56 张 / {0} 个端子通过；与原 OpenCV Demo 最大角度差 {1:F9}°；单端子 ROI 算法平均 {2:F3} ms（排除图像读取/解码、显示）。",count,maximum,durations.Average()));
    }
    /// <summary>按照框架仿射约定生成已知平移、旋转和尺度的测试图。</summary>
    private static Mat CorrectedImage(Mat source, PositionCorrectionInfo correction)
    {
        return WarpImage(source,new TerminalAngleTransform(correction));
    }
    /// <summary>根据完整相对仿射矩阵生成跨帧测试图。</summary>
    private static Mat WarpImage(Mat source, TerminalAngleTransform transform)
    {
        var origin=transform.Forward(0,0);var x=transform.Forward(1,0);var y=transform.Forward(0,1);
        using(var affine=new Mat(2,3,MatType.CV_64FC1))
        {
            affine.Set(0,0,(double)(x.X-origin.X));affine.Set(0,1,(double)(y.X-origin.X));affine.Set(0,2,(double)origin.X);
            affine.Set(1,0,(double)(x.Y-origin.Y));affine.Set(1,1,(double)(y.Y-origin.Y));affine.Set(1,2,(double)origin.Y);
            var result=new Mat();Cv2.WarpAffine(source,result,affine,new OpenCvSharp.Size(1400,1400),InterpolationFlags.Linear,BorderTypes.Constant,Scalar.White);return result;
        }
    }
    /// <summary>位置修正、多目标及弯端子切直端子重画回归。</summary>
    private static void PositionCorrection(string artifacts)
    {
        var algorithm=new OpenCvTerminalAngleMeasurer();
        using(var original=Synthetic(5))
        {
            var config=Settings();config.UsePositionCorrection=true;
            var reference=algorithm.Measure(original,config,CancellationToken.None);
            foreach(double angle in new[]{0.0,-12.0,18.0})
            {
                var pose=new PositionCorrectionInfo {IsValid=true,BaseX=205,BaseY=475,CurrentX=650,CurrentY=650,CurrentAngle=angle,CurrentScaleX=1.2,CurrentScaleY=0.9};
                var transform=new TerminalAngleTransform(pose);
                using(var image=CorrectedImage(original,pose))
                {
                    var measured=TerminalAnglePositionCorrection.Measure(algorithm,image,config,transform,CancellationToken.None);
                    var top=transform.Forward(reference.Top.X,reference.Top.Y);var bottom=transform.Forward(reference.Bottom.X,reference.Bottom.Y);
                    double expected=Math.Atan2(top.X-bottom.X,bottom.Y-top.Y)*180/Math.PI;
                    Check(measured.Success && Math.Abs(measured.Angle.Value-expected)<0.2,"旧方案平移旋转缩放回归失败。");
                    Check(Math.Abs(measured.Top.X-top.X)<2 && Math.Abs(measured.Top.Y-top.Y)<2,"几何未回到当前图像。");
                }
            }
            var process=new TDJS_Vision.Process("端子位置修正回归");
            using(var source=new NodeBase(1,"图像裁剪",process,NodeType.ImageSource))
            using(var correctionNode=new NodeBase(2,"位置修正",process,NodeType.PositionCorrection))
            using(var node=(NodeTerminalAngle)NodeFactory.CreateNode(3,"端子角度",process,NodeType.TerminalAngle))
            {
                var bentPose=new PositionCorrectionInfo {IsValid=true,BaseX=205,BaseY=475,CurrentX=650,CurrentY=650,CurrentAngle=-12,CurrentScaleX=1.2,CurrentScaleY=0.9};
                var poseResult=new NodeResultPositionCorrection {Items=new List<PositionCorrectionInfo>{bentPose},IsValid=true};
                using(var curved=new Mat(950,410,MatType.CV_8UC3,Scalar.White))
                using(var straight=new Mat(1400,1400,MatType.CV_8UC3,Scalar.White))
                {
                    // 真实折弯轮廓而非仅倾斜直线，覆盖弯端子模板切换直端子后的重画。
                    Cv2.Polylines(curved,new[]{new[]{new OpenCvSharp.Point(200,40),new OpenCvSharp.Point(160,475),new OpenCvSharp.Point(200,860)}},false,new Scalar(20,100,160),40);
                    Cv2.Rectangle(straight,new Rect(600,150,50,800),new Scalar(20,100,160),-1);
                    var owner=OutputImage.FromOwnedSingleImage(CorrectedImage(curved,bentPose));
                    typeof(NodeBase).GetProperty("Result").SetValue(source,new NodeResultImageSource {OutputImage=owner});
                    typeof(NodeBase).GetProperty("Result").SetValue(correctionNode,poseResult);
                    process.Nodes.Add(source);process.Nodes.Add(correctionNode);process.Nodes.Add(node);
                    process.Connections.Add(new ProcessConnection {FromNodeId=1,ToNodeId=2});process.Connections.Add(new ProcessConnection {FromNodeId=2,ToNodeId=3});
                    var form=(NodeParamFormTerminalAngle)node.ParamForm;form.SetNodeBelong(node);
                    config=Settings();config.Text1="1.图像裁剪";config.UsePositionCorrection=true;config.CorrectionText1="2.位置修正";config.CorrectionText2="位置修正信息列表";
                    form.Params=RoundTrip(config);form.SetParam2Form();
                    form.StartPosition=FormStartPosition.Manual;form.Location=new DrawingPoint(-20000,-20000);form.ShowInTaskbar=false;
                    form.Show();Application.DoEvents();SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                    Click(form,"RefreshButton_Click");
                    var viewer=Field<ShowImageControl>(form,"viewer");
                    Click(form,"TerminalButton_Click");Drag(viewer,new PointF(540,300),new PointF(750,980));
                    Check(viewer.GetAllDynamicRects().Single().Phi==0,"非零定位角度立即带斜新画区域。");
                    var pendingMouse=typeof(NodeParamFormTerminalAngle).GetMethod("Viewer_MouseUp",Flags);
                    pendingMouse.Invoke(form,new object[]{viewer,new MouseEventArgs(MouseButtons.Left,1,100,100,0)});
                    // 模板定位仍带角度时切到直端子，覆盖截图所述场景，并丢弃旧帧延迟回调。
                    var straightPose=new PositionCorrectionInfo {IsValid=true,BaseX=205,BaseY=475,CurrentX=650,CurrentY=650,CurrentAngle=18,CurrentScaleX=1.2,CurrentScaleY=0.9};
                    poseResult.Items=new List<PositionCorrectionInfo>{straightPose};
                    straight.CopyTo(owner.Bitmaps[0]);form.LoadPreview(straight,false);Application.DoEvents();
                    Check(viewer.GetAllDynamicRects().Count==0 && !Field<bool>(form,"_drawing"),"换图未取消旧绘制或旧回调写入新图。");
                    Click(form,"TerminalButton_Click");Drag(viewer,new PointF(560,200),new PointF(700,900));
                    Check(viewer.GetAllDynamicRects().Single().Phi==0,"切直端子重画后 ROI 被定位角度带斜。");
                    var drawn=Field<NodeParamTerminalAngle>(form,"_editing").TerminalRoi;
                    var currentTransform=new TerminalAngleTransform(straightPose);
                    var relative=currentTransform.RelativeTo(drawn.DrawingPose);
                    Check(relative.IsIdentity && Math.Abs(drawn.Left-560)<3,"同一绘制帧没有保持恒等变换。");
                    Click(form,"BaseButton_Click");Drag(viewer,new PointF(570,1000),new PointF(680,1100));
                    Click(form,"SaveButton_Click");
                    var saved=(NodeParamTerminalAngle)form.Params;
                    Check(saved.TerminalRoi.DrawingPose!=null && saved.BaseRoi.DrawingPose!=null,"绘制位姿没有持久化。");
                    var restored=RoundTrip(saved);
                    Check(currentTransform.RelativeTo(restored.TerminalRoi.DrawingPose).IsIdentity,"序列化后同帧新框重新变斜。");
                    var copy=saved.Copy();copy.TerminalRoi.DrawingPose.X+=10;
                    Check(copy.TerminalRoi.DrawingPose.X!=saved.TerminalRoi.DrawingPose.X,"绘制位姿快照未独立复制。");
                    ExecutePreview(form);
                    Check(ImageText(form)=="端子角度：0.0°","直端子的图像测量结果未按一位小数显示零度。");
                    node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
                    var result=(NodeResultTerminalAngle)node.Result;
                    Check(result.Success && Math.Abs(result.Angle.Value)<0.12,"直端子生产角度被绘制基准角度抵消或带斜。");
                    Check(result.Items[0].DisplayResult.Lines.Count==2,"生产叠加未限定为两条中线。");
                    form.Refresh();Application.DoEvents();
                    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,form.Width,form.Height));bitmap.Save(Path.Combine(artifacts,"端子角度节点-直端子重画回归.png"));}
                    // 同一帧追加第二个端子，卡尺式运行必须自动测量全部列表项。
                    Cv2.Rectangle(owner.Bitmaps[0],new Rect(1000,150,50,800),new Scalar(20,100,160),-1);
                    var second=new PositionCorrectionInfo {IsValid=true,BaseX=205,BaseY=475,CurrentX=1050,CurrentY=650,CurrentAngle=18,CurrentScaleX=1.2,CurrentScaleY=0.9};
                    poseResult.Items=new List<PositionCorrectionInfo>{straightPose,second,new PositionCorrectionInfo {IsValid=false}};
                    node.Run(CancellationToken.None,false).GetAwaiter().GetResult();result=(NodeResultTerminalAngle)node.Result;
                    Check(result.Items.Count==3 && result.Items[0].IsOk && result.Items[1].IsOk && !result.Items[2].IsOk,"未按卡尺遍历全部目标或失败目标丢失顺序。");
                    Check(result.Items[0].TargetIndex==1 && result.Items[1].TargetIndex==2 && !result.Items[2].Angle.HasValue,"多目标序号或失败角度错误。");
                    Click(form,"RefreshButton_Click");ExecutePreview(form);
                    Check(ImageText(form).Contains("端子1") && ImageText(form).Contains("端子2") && ImageText(form).Contains("端子3"),"多目标结果没有全部显示在图像。");
                    // 新绘制选择第二目标时，由共用归属服务自动关联，无手选目标控件。
                    Click(form,"TerminalButton_Click");Drag(viewer,new PointF(960,200),new PointF(1100,900));
                    var automatic=Field<NodeParamTerminalAngle>(form,"_editing").TerminalRoi;
                    Check(new TerminalAngleTransform(second).RelativeTo(automatic.DrawingPose).IsIdentity,"绘制没有自动关联最近定位目标。");
                    // 从绘制帧到下一帧的真实旋转仍需生效，不允许用全局禁用旋转掩盖重画问题。
                    var later=new PositionCorrectionInfo {IsValid=true,BaseX=205,BaseY=475,CurrentX=700,CurrentY=700,CurrentAngle=28,CurrentScaleX=1.2,CurrentScaleY=0.9};
                    var laterTransform=new TerminalAngleTransform(later).RelativeTo(saved.TerminalRoi.DrawingPose);
                    Check(Math.Abs(laterTransform.AngleDegrees-10)<0.001,"下一帧位置修正的相对旋转未生效。");
                    using(var nextFrame=WarpImage(straight,laterTransform))
                    {
                        var nextResult=TerminalAnglePositionCorrection.Measure(algorithm,nextFrame,saved,new TerminalAngleTransform(later),CancellationToken.None);
                        Check(nextResult.Success && Math.Abs(nextResult.Angle.Value-10)<0.2,"保存后下一帧实际旋转图像测量错误："+nextResult.Message);
                    }
                    var nonUniform=new PositionCorrectionInfo {IsValid=true,BaseX=205,BaseY=475,CurrentX=700,CurrentY=700,CurrentAngle=28,CurrentScaleX=1.4,CurrentScaleY=0.7};
                    var general=new TerminalAngleTransform(nonUniform).RelativeTo(saved.TerminalRoi.DrawingPose);
                    var basePoint=currentTransform.Inverse(625,500);
                    var expectedPoint=new TerminalAngleTransform(nonUniform).Forward(basePoint.X,basePoint.Y);
                    var actualPoint=general.Forward(625,500);
                    Check(Math.Abs(expectedPoint.X-actualPoint.X)<0.001 && Math.Abs(expectedPoint.Y-actualPoint.Y)<0.001,"非等比尺度相对位姿组合错误。");
                    poseResult.Items.Clear();form.Params=saved;form.SetParam2Form();
                    node.Run(CancellationToken.None,false).GetAwaiter().GetResult();result=(NodeResultTerminalAngle)node.Result;
                    Check(result.Items.Count==0 && !result.Success && !result.Angle.HasValue,"无定位目标沿用旧结果。");
                    node.Active=false;node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
                    Check(owner.ActiveLeaseCount==0,"停止后图像租约未释放。");
                }
            }
        }
        Console.WriteLine("非零角度重画、倾斜换直端子、延迟回调隔离、绘制位姿持久化及卡尺多目标流程通过。");
    }
    /// <summary>离线验证入口，不启动主程序、不连接相机或 PLC。</summary>
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Console.OutputEncoding=System.Text.Encoding.UTF8;Application.EnableVisualStyles();Directory.CreateDirectory(args[1]);
            string scope=args.Length>2?args[2]:"all";
            if(scope=="geometry") {Geometry();Console.WriteLine("端子角度几何专项通过，断言数："+_checks);return 0;}
            if(scope!="confirm-close")SubscriptionText();
            if(scope!="subscription-text")ConfirmCloses();
            if(scope=="all") {Geometry();NodeAndUi(args[1]);PositionCorrection(args[1]);ActualImages(args[0],args[1]);}
            Console.WriteLine("验证通过，断言数："+_checks);return 0;
        }
        catch(Exception exception) { Console.Error.WriteLine(exception);return 1; }
    }
}
