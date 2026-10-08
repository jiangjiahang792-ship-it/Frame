using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>一个方案运行窗体的稳定标识与独立布局。</summary>
    public sealed class RuntimeInterfacePage
    {
        /// <summary>重命名或调整顺序时不改变的窗体标识。</summary>
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        /// <summary>窗体布局，名称保存在布局中，模板导入导出保持一致。</summary>
        public RuntimeLayoutDocument Layout { get; set; } = new RuntimeLayoutDocument();

        /// <summary>完整校验并复制界面集合，草稿和生产方案不共享布局或绑定。</summary>
        public static List<RuntimeInterfacePage> ClonePages(IEnumerable<RuntimeInterfacePage> pages)
        {
            var result = new List<RuntimeInterfacePage>();
            var ids = new HashSet<string>(StringComparer.Ordinal);
            long bytes = 0;
            foreach (var page in pages ?? new RuntimeInterfacePage[0])
            {
                if (page == null || string.IsNullOrWhiteSpace(page.Id) || page.Id.Length > 64 || !ids.Add(page.Id))
                    throw new InvalidDataException("运行界面标识不能为空、重复或超过 64 个字符。");
                string json = RuntimeLayoutSerializer.Serialize(page.Layout);
                // 合并限制所有布局，防止多窗口绕过原模板内存边界。
                bytes += Encoding.UTF8.GetByteCount(json) + Encoding.UTF8.GetByteCount(JsonConvert.ToString(page.Id)) + 32;
                if (bytes > RuntimeLayoutSerializer.MaximumDocumentBytes)
                    throw new InvalidDataException("方案内运行界面配置总大小不能超过 16 MB。");
                result.Add(new RuntimeInterfacePage { Id = page.Id, Layout = RuntimeLayoutSerializer.Deserialize(json) });
            }
            return result;
        }
    }
}
