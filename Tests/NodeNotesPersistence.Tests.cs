using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using TDJS_Vision;
using TDJS_Vision.Forms.ProcessNew;
using TDJS_Vision.Node;
using TDJS_Vision.Node._6_LogicTool.CompositeModule;

/// <summary>验证真实方案保存及流程重建后的备注恢复，不加载现场方案或设备。</summary>
internal static class NodeNotesPersistenceTests
{
    /// <summary>对错误给出明确场景。</summary>
    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    /// <summary>在独立进程中验证备注保存、普通流程及组合内部恢复、旧配置兼容。</summary>
    [STAThread]
    private static int Main()
    {
        try
        {
            string notes = "工位一显示\r\n备注第二行";
            var process = new Process("备注持久化测试");
            using (var node = new NodeBase(1, "备注节点", process, NodeType.ProcessSignal))
            {
                node.SetNotes(notes);
                process.Nodes.Add(node);
                Solution.Instance.AllProcesses.Add(process);
                string path = Path.GetFullPath("node-notes-validation.Sol");
                ConfigHelper.SolSave(path);
                var saved = JsonConvert.DeserializeObject<SolConfig>(File.ReadAllText(path));
                Check(saved.ProcessInfos[0].NodeInfos[0].Notes == notes, "正式方案保存必须包含完整备注");
                using (var panel = new ProcessEditPanel("恢复备注", false, saved.ProcessInfos[0]))
                {
                    var restored = (Process)typeof(ProcessEditPanel).GetProperty("_process", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).GetValue(panel);
                    Check(restored.Nodes.Single().Notes == notes, "重新加载流程必须回填备注");
                }
                var builder = typeof(NodeCompositeModule).Assembly.GetType("TDJS_Vision.Node._6_LogicTool.CompositeModule.CompositeModuleSnapshotBuilder");
                var snapshot = (NodeParamCompositeModule)builder.GetMethod("CreateFromProcess").Invoke(null, new object[] { process });
                snapshot = JsonConvert.DeserializeObject<NodeParamCompositeModule>(JsonConvert.SerializeObject(snapshot));
                Check(snapshot.NodeInfos.Single().Notes == notes, "组合模块快照必须保留备注");
                using (var module = new NodeCompositeModule(2, "备注组合", process, NodeType.CompositeModule))
                {
                    module.ParamForm.Params = snapshot;
                    Check(module.GetEditableProcess().Nodes.Any(item => item.ID == 1 && item.Notes == notes), "组合内部重建必须恢复备注");
                }
                Check(JsonConvert.DeserializeObject<NodeConfig>("{}").Notes == "无备注", "旧方案缺少字段应正常兼容");
                Check(JsonConvert.DeserializeObject<NodeConfig>("{\"Notes\":\"\"}").Notes == "", "用户清空的备注应保留为空");
            }
            Console.WriteLine("备注正式保存、普通流程重建、组合模块重建和旧方案兼容验证通过。");
            return 0;
        }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
}
