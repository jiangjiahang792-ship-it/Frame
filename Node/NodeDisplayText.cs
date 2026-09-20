using System;
using System.Collections.Generic;

namespace TDJS_Vision.Node
{
    /// <summary>统一工具箱与流程图的节点名称显示，并兼容旧方案中保存的未翻译资源键。</summary>
    public static class NodeDisplayText
    {
        /// <summary>新增节点采用中文语言键；已有节点继续使用原资源。</summary>
        private static readonly Dictionary<NodeType, string> ChineseKeys = new Dictionary<NodeType, string>
        {
            { NodeType.ContourMatch, "轮廓模板匹配" },
            { NodeType.TerminalAngle, "端子角度" }
        };

        /// <summary>取得工具箱及节点类型说明文字，资源缺失时避免显示内部资源键。</summary>
        public static string GetTypeName(NodeType type)
        {
            if (ChineseKeys.TryGetValue(type, out string chineseKey)) return LanguageManager.T(chineseKey);
            if (type == NodeType.ResultSend) return "结果发送";
            string key = "ProcessNew.NodeType." + type;
            string translated = LanguageManager.T(key);
            return translated == key ? type.ToString() : translated;
        }

        /// <summary>仅翻译默认名称及旧资源键，不改写模型名称，以免破坏已有订阅或用户自定义名称。</summary>
        public static string GetTitle(NodeType type, string name)
        {
            if (string.IsNullOrEmpty(name) || !ChineseKeys.TryGetValue(type, out string chineseKey)) return name;
            string legacyKey = "ProcessNew.NodeType." + type;
            string englishName = type == NodeType.ContourMatch ? "Contour Template Matching" : "Terminal Angle";
            string prefix = name.StartsWith(legacyKey, StringComparison.Ordinal) ? legacyKey :
                name.StartsWith(englishName, StringComparison.Ordinal) ? englishName : chineseKey;
            if (name == prefix) return GetTypeName(type);
            if (!name.StartsWith(prefix + "_", StringComparison.Ordinal)) return name;
            // 同名节点的自动编号仍显示原数字，自定义后缀保持原样。
            string suffix = name.Substring(prefix.Length + 1);
            if (suffix.Length == 0) return name;
            foreach (char character in suffix) if (character < '0' || character > '9') return name;
            return GetTypeName(type) + "_" + suffix;
        }
    }
}
