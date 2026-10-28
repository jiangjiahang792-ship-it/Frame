using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Newtonsoft.Json;
using OpenCvSharp;
using TDJS_Vision.Node;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision;
using TDJS_Vision.Node._3_Detection.NewContourMatch;

/// <summary>真实双版本原生库、尺度坐标及节点持久化回归。</summary>
internal static class NewContourMatchTests
{
    /// <summary>反射访问测试所需的内部会话和设计器字段。</summary>
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public;
    /// <summary>累计有效断言。</summary>
    private static int checks;
    /// <summary>验证失败立即终止。</summary>
    private static void Check(bool ok, string message) { if (!ok) throw new Exception(message); checks++; }
    /// <summary>创建不对称轮廓，包含偏离 ROI 中心的轮廓原点。</summary>
    private static Mat Template()
    {
        var image = new Mat(88, 96, MatType.CV_8UC3, Scalar.All(20));
        int[,] boxes = { {14,14,62,5}, {14,14,5,58}, {14,67,45,5}, {54,41,5,31}, {54,37,25,5}, {74,37,5,18}, {30,30,16,12} };
        for (int i=0;i<boxes.GetLength(0);i++) Cv2.Rectangle(image,new Rect(boxes[i,0],boxes[i,1],boxes[i,2],boxes[i,3]),Scalar.All(230),-1);
        return image;
    }
    /// <summary>生成已知顺时针角度和独立缩放的目标图像。</summary>
    private static Mat Transform(Mat template, double angle, double sx, double sy)
    {
        var image=new Mat(); double radians=angle*Math.PI/180, c=Math.Cos(radians), s=Math.Sin(radians);
        using(var affine=new Mat(2,3,MatType.CV_64FC1)) {
            affine.Set(0,0,c*sx); affine.Set(0,1,-s*sy); affine.Set(1,0,s*sx); affine.Set(1,1,c*sy);
            affine.Set(0,2,152-c*sx*48+s*sy*44); affine.Set(1,2,102-s*sx*48-c*sy*44);
            Cv2.WarpAffine(template,image,affine,new OpenCvSharp.Size(280,220),InterpolationFlags.Linear,BorderTypes.Constant,Scalar.All(20));
        }
        return image;
    }
    /// <summary>验证原生匹配、零结果、非连续 Mat、缩放及擦除恢复。</summary>
    private static void Native(int version, Mat template)
    {
        using(var matcher=new NativeShapeMatcher()) {
            var create=new CreateModelOptions { AlgorithmVersion=version, PyramidLevels=2, AngleStartDegrees=-15,AngleEndDegrees=15,AngleStepDegrees=2,ScaleToleranceX=.15,ScaleToleranceY=.15 };
            matcher.CreateModel(template,new Rectangle(0,0,96,88),create);
            Check(matcher.HasModel && matcher.GetModelContours().Count>0,"建模或轮廓读取失败");
            foreach(var test in new[]{new[]{0.0,1.0,1.0},new[]{7.0,1.08,.94}})
            using(var image=Transform(template,test[0],test[1],test[2]))
            using(var view=new Mat(image,new Rect(0,0,270,210))) {
                var found=matcher.Find(view,new FindOptions { AngleStartDegrees=-15,AngleEndDegrees=15,MinimumScore=.55,MaximumMatches=1 });
                Check(found.Count==1,"版本"+version+"未找到目标"); var m=found[0];
                Console.WriteLine("版本{0}: X={1:F3},Y={2:F3},角度={3:F3},尺度={4:F4}/{5:F4},分数={6:F4}",version,m.CenterX,m.CenterY,m.AngleDegrees,m.ScaleX,m.ScaleY,m.Score);
                Check(Math.Abs(m.CenterX-152)<1.5 && Math.Abs(m.CenterY-102)<1.5,"ROI中心转换不正确");
                Check(Math.Abs(m.AngleDegrees-test[0])<1.5,"角度约定错误");
                Check(Math.Abs(m.ScaleX-test[1])<.04 && Math.Abs(m.ScaleY-test[2])<.04,"独立缩放输出错误");
            }
            using(var blank=new Mat(220,280,MatType.CV_8UC1,Scalar.All(20)))
                Check(matcher.Find(blank,new FindOptions {MinimumScore=.7}).Count==0,"空图应正常返回零目标");
            int before=matcher.GetModelFeatures().Count;
            var erase=new byte[96*88]; for(int y=25;y<47;y++)for(int x=25;x<50;x++)erase[y*96+x]=255;
            matcher.EraseModelFeatures(erase,96,88);
            Check(matcher.GetModelFeatures().Count<before,"擦除没有减少轮廓");
            var all=Enumerable.Repeat((byte)255,96*88).ToArray(); bool rejected=false;
            try { matcher.EraseModelFeatures(all,96,88); }catch(InvalidOperationException){rejected=true;}
            Check(rejected && matcher.HasModel,"擦除失败必须保留旧模型");
            // 分离建模区域不应因掩膜边界产生伪轮廓。
            var mask=new byte[96*88];
            for(int y=8;y<78;y++)for(int x=8;x<83;x++)if(x<24 || x>50)mask[y*96+x]=255;
            matcher.CreateMaskedModel(template,new Rectangle(0,0,96,88),create,mask);
            using(var self=Transform(template,0,1,1))
                Check(matcher.Find(self,new FindOptions {MinimumScore=.55,MaximumMatches=1}).Count==1,"组合掩膜模板搜索失败");
        }
    }
    /// <summary>验证模板包、独立节点注册、会话缓存、多模板合并、界面恢复。</summary>
    private static void Integration(Mat template, string directory)
    {
        byte[] bytes; Cv2.ImEncode(".png",template,out bytes);
        var model=new NodeParamNewContourMatch {ModelImageBytes=bytes,ModelRoi=new Rectangle(0,0,96,88),ModelOptions=new CreateModelOptions {PyramidLevels=2,ScaleToleranceX=0,ScaleToleranceY=0},CreateOptions=new CreateModelOptions()};
        var package=new ContourTemplatePackage {Templates=new List<ContourTemplateDefinition>{new ContourTemplateDefinition {Name="第一模板",Model=model}}};
        var restored=ContourTemplatePackage.Decode(package.Encode());
        Check(restored.Templates[0].Model.ModelOptions.AlgorithmVersion==2,"模板包版本丢失");
        var parameters=new NodeParamNewContourMatch {Templates=restored.Templates,FindOptions=new FindOptions {MinimumScore=.6,MaximumMatches=2}};
        parameters.Templates.Add(new ContourTemplateDefinition {Name="同目标备用模板",Model=model.Copy()});
        parameters=JsonConvert.DeserializeObject<NodeParamNewContourMatch>(JsonConvert.SerializeObject(parameters));
        var type=typeof(NativeShapeMatcher).Assembly.GetType("TDJS_Vision.Node._3_Detection.NewContourMatch.NewContourMatchSession");
        using(var session=(IDisposable)Activator.CreateInstance(type,Flags,null,new object[]{null},null))
        using(var image=Transform(template,0,1,1)) {
            var execute=type.GetMethod("Execute",Flags);
            for(int i=0;i<2;i++) {
                var result=execute.Invoke(session,new object[]{image,parameters,CancellationToken.None});
                var matches=(IReadOnlyList<ShapeMatchResult>)result.GetType().GetProperty("Matches",Flags).GetValue(result);
                Check(matches.Count==1,"多模板去重或缓存失败");
            }
            parameters.AllSearch=false;parameters.SearchRegion=new Rectangle(0,0,40,40);
            var outside=execute.Invoke(session,new object[]{image,parameters,CancellationToken.None});
            Check(((IReadOnlyList<ShapeMatchResult>)outside.GetType().GetProperty("Matches",Flags).GetValue(outside)).Count==0,"搜索区域没有限制目标");
        }
        var process=new TDJS_Vision.Process("新版轮廓离线验证");
        var node=NodeFactory.CreateNode(1,"新版本轮廓模板匹配",process,NodeType.NewContourMatch);
        Check(node is NodeNewContourMatch,"节点工厂未注册");
        var toolbox=File.ReadAllText("ToolTreeView.xml");
        Check(toolbox.Contains("Tag=\"NewContourMatch\""),"工具箱缺少新节点入口");
        Check(NodeDisplayText.GetTypeName(NodeType.NewContourMatch)=="新版本轮廓模板匹配","节点名称不正确");
        using(var form=(NodeParamFormNewContourMatch)node.ParamForm) {
            parameters.AllSearch=true;
            var source=new NodeBase(2,"输入图像",process,NodeType.ImageSource);
            var owned=Transform(template,7,1.08,.94);
            var owner=new OutputImage {SrcImg=owned,Bitmaps=new List<Mat>{owned}};owner.TakeOwnership(owned);
            typeof(NodeBase).GetProperty("Result").SetValue(source,new NodeResultImageSource {OutputImage=owner});
            process.Nodes.Add(source);process.Nodes.Add(node);
            process.Connections.Add(new ProcessConnection {FromNodeId=2,ToNodeId=1});
            parameters.Text1="2.输入图像";parameters.Text2="输出图像";parameters.SourceNodeId=2;
            parameters.Templates[0].Model.ModelOptions.ScaleToleranceX=.15;parameters.Templates[0].Model.ModelOptions.ScaleToleranceY=.15;
            form.Params=parameters;form.SetParam2Form();Check(((NodeParamNewContourMatch)form.Params).Templates.Count==2,"参数恢复丢失模板");
            node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
            var production=(NodeResultNewContourMatch)node.Result;
            Check(production.MatchCount==1 && production.Result.Contours.Count>0,"生产节点输出或轮廓绘制失败");
            Check(Math.Abs(production.Poses[0].ScaleX-1.08)<.04 && Math.Abs(production.Poses[0].ScaleY-.94)<.04,"下游位姿尺度丢失");
            object coordinate;Check(production.TryGetDynamicVariable("目标1中心X",out coordinate) && Math.Abs((double)coordinate-152)<1.5,"动态坐标输出错误");
            var runtime=form.GetType().GetField("_runtimeSession",Flags).GetValue(form);
            var models=runtime.GetType().GetField("_models",Flags).GetValue(runtime);
            node.Run(CancellationToken.None,false).GetAwaiter().GetResult();
            Check(ReferenceEquals(models,runtime.GetType().GetField("_models",Flags).GetValue(runtime)),"生产运行重复建模");
            bool cancelled=false;try{node.Run(new CancellationToken(true),false).GetAwaiter().GetResult();}catch(OperationCanceledException){cancelled=true;}
            Check(cancelled && ((NodeResultNewContourMatch)node.Result).MatchCount==0,"取消后残留旧结果");
            form.Show();Application.DoEvents();
            var canvas=(ImageCanvas)form.GetType().GetField("imageCanvas",Flags).GetValue(form);
            var tabs=(TabControl)form.GetType().GetField("parameterTabs",Flags).GetValue(form);
            var execute=(Button)form.GetType().GetField("executeButton",Flags).GetValue(form);
            tabs.SelectedIndex=0;
            canvas.BeginCreate();tabs.Enabled=false;execute.Enabled=false;
            typeof(ImageCanvas).GetField("_draftRoi",Flags).SetValue(canvas,new Rectangle(24,32,120,96));
            FormClosingEventHandler keepFormForReuse=(sender,args)=>args.Cancel=true;
            form.FormClosing+=keepFormForReuse;form.Close();
            var closeParameters=(NodeParamNewContourMatch)form.Params;
            Check(tabs.Enabled && execute.Enabled && canvas.EditorMode==CanvasEditorMode.查看,
                "新版节点直接确定后搜索区域编辑状态或按钮未恢复。");
            Check(!closeParameters.AllSearch && closeParameters.SearchRegion==new Rectangle(24,32,120,96),
                "新版节点直接确定没有提交有效搜索区域。");
            form.FormClosing-=keepFormForReuse;
            ((TabControl)form.GetType().GetField("parameterTabs",Flags).GetValue(form)).SelectedIndex=2;Application.DoEvents();
            using(var bmp=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(directory,"新版本轮廓节点.png"));}
            form.Close();source.Dispose();owner.Dispose();
        }
        using(var editor=new ContourTemplateEditor()) {
            editor.Show();Application.DoEvents();
            var combo=(ComboBox)editor.GetType().GetField("algorithmComboBox",Flags).GetValue(editor);
            Check(combo.SelectedIndex==1 && combo.Items.Count==2,"默认算法或切换选项不正确");
            ((TabControl)editor.GetType().GetField("editorParameterTabs",Flags).GetValue(editor)).SelectedIndex=1;Application.DoEvents();
            using(var bmp=new Bitmap(editor.Width,editor.Height)){editor.DrawToBitmap(bmp,new Rectangle(0,0,bmp.Width,bmp.Height));bmp.Save(Path.Combine(directory,"新版本轮廓编辑器.png"));}
            editor.Close();
        }
    }
    /// <summary>不连接设备的独立测试入口。</summary>
    [STAThread] private static int Main(string[] args)
    {
        try { Application.EnableVisualStyles();Directory.CreateDirectory(args[0]);using(var template=Template()){Native(1,template);Native(2,template);Integration(template,args[0]);}Console.WriteLine("新版本轮廓匹配验证通过，断言数："+checks);return 0; }
        catch(Exception e){Console.Error.WriteLine(e);return 1;}
    }
}
