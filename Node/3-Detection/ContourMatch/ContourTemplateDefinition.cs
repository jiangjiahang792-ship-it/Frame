using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>一个可独立创建、启用、保存和交换的特征模板。</summary>
    public sealed class ContourTemplateDefinition
    {
        /// <summary>模板稳定标识，不随名称编辑改变。</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>列表和匹配结果显示的名称。</summary>
        public string Name { get; set; } = "新模板";
        /// <summary>是否参与生产搜索。</summary>
        public bool Enabled { get; set; } = true;
        /// <summary>模板独占的源图、ROI、创建参数和删除记录，不包含模板列表。</summary>
        public NodeParamContourMatch Model { get; set; }
        /// <summary>创建独立副本，防止编辑修改保存中的方案。</summary>
        public ContourTemplateDefinition Copy() { return new ContourTemplateDefinition { Id = Id, Name = Name, Enabled = Enabled, Model = Model?.Copy() }; }
        /// <summary>从编辑结果提取纯模型参数，避免递归保存节点模板列表。</summary>
        internal static ContourTemplateDefinition FromModel(NodeParamContourMatch value, string name, string id = null)
        {
            return new ContourTemplateDefinition { Id = id ?? Guid.NewGuid().ToString("N"), Name = name,
                Model = new NodeParamContourMatch { ModelImageBytes = value.ModelImageBytes == null ? null : (byte[])value.ModelImageBytes.Clone(),
                    ModelRoi = value.ModelRoi, ModelOptions = NodeParamContourMatch.CopyCreate(value.ModelOptions),
                    ModelRegions = value.ModelRegions?.Select(region => region.Copy()).ToList(),
                    CreateOptions = NodeParamContourMatch.CopyCreate(value.CreateOptions), BrushSize = value.BrushSize,
                    EraseMasks = (value.EraseMasks ?? new List<byte[]>()).Select(mask => (byte[])mask.Clone()).ToList() } };
        }
    }

    /// <summary>模板交换文件，禁用运行时类型元数据，仅接受明确的数据结构。</summary>
    public sealed class ContourTemplatePackage
    {
        /// <summary>文件格式标识。</summary>
        public string Format { get; set; } = "TDJS.ContourTemplates";
        /// <summary>格式版本。</summary>
        public int Version { get; set; } = 1;
        /// <summary>需要交换的模板集合。</summary>
        public List<ContourTemplateDefinition> Templates { get; set; } = new List<ContourTemplateDefinition>();
        /// <summary>序列化为可移植模板文件。</summary>
        public string Encode() { return JsonConvert.SerializeObject(this, Formatting.Indented); }
        /// <summary>验证整个导入包后返回副本；失败时不改变现有列表。</summary>
        public static ContourTemplatePackage Decode(string json)
        {
            var package = JsonConvert.DeserializeObject<ContourTemplatePackage>(json, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.None, MaxDepth = 24 });
            if (package == null || package.Format != "TDJS.ContourTemplates" || package.Version != 1 || package.Templates == null || package.Templates.Count == 0)
                throw new ArgumentException("不是受支持的轮廓模板文件。");
            var validated = new List<ContourTemplateDefinition>();
            foreach (var entry in package.Templates)
            {
                if (entry?.Model == null || entry.Model.Templates != null || string.IsNullOrWhiteSpace(entry.Name)) throw new ArgumentException("模板数据不完整。");
                using (var matcher = ContourMatchSession.Restore(entry.Model)) { }
                var copy = ContourTemplateDefinition.FromModel(entry.Model, entry.Name); copy.Enabled = entry.Enabled; validated.Add(copy);
            }
            package.Templates = validated; return package;
        }
    }
}
