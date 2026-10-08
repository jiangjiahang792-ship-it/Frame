using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>集中管理编辑事务、工作副本和有界撤销记录。</summary>
    public sealed class RuntimeLayoutSession
    {
        /// <summary>撤销快照，按时间先后排列。</summary>
        private readonly List<string> undo = new List<string>();
        /// <summary>重做快照。</summary>
        private readonly List<string> redo = new List<string>();
        /// <summary>当前编辑文档。</summary>
        public RuntimeLayoutDocument Document { get; private set; }
        /// <summary>文档变化后通知画布和属性栏。</summary>
        public event EventHandler Changed;
        /// <summary>是否可撤销。</summary>
        public bool CanUndo => undo.Count > 0;
        /// <summary>是否可重做。</summary>
        public bool CanRedo => redo.Count > 0;

        /// <summary>从方案配置生成隔离的编辑副本。</summary>
        public RuntimeLayoutSession(RuntimeLayoutDocument document)
        {
            Document = RuntimeLayoutSerializer.Clone(document ?? new RuntimeLayoutDocument());
        }

        /// <summary>原子执行修改，校验不通过则恢复编辑前状态。</summary>
        public void Edit(Action<RuntimeLayoutDocument> edit)
        {
            string before = RuntimeLayoutSerializer.Serialize(Document);
            try
            {
                edit(Document);
                string after = RuntimeLayoutSerializer.Serialize(Document);
                if (before == after) return;
                Push(undo, before);
                redo.Clear();
            }
            catch
            {
                Document = RuntimeLayoutSerializer.Deserialize(before);
                Changed?.Invoke(this, EventArgs.Empty);
                throw;
            }
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>添加指定类型组件并约束到页面内。</summary>
        public RuntimeLayoutElement Add(RuntimeWidgetKind kind, Point location)
        {
            var element = CreateElement(kind);
            element.Name += Document.Elements.Count(e => e.Kind == kind) + 1;
            ApplyBounds(element, Constrain(new Rectangle(location, new Size(element.Width, element.Height))));
            Edit(doc => doc.Elements.Add(element));
            return element;
        }

        /// <summary>为拖入预览和正式添加使用同一组默认属性，不修改文档。</summary>
        public static RuntimeLayoutElement CreateElement(RuntimeWidgetKind kind)
        {
            var element = new RuntimeLayoutElement { Kind = kind, Name = GetName(kind), Text = GetName(kind) };
            if (kind == RuntimeWidgetKind.Image) { element.Width = 440; element.Height = 300; element.FontSize = 13; }
            if (kind == RuntimeWidgetKind.Elapsed) { element.Width = 260; element.Height = 120; element.FontSize = 28; }
            if (kind == RuntimeWidgetKind.Signal) { element.Width = 200; element.Height = 80; }
            if (kind == RuntimeWidgetKind.Text) { element.Width = 280; element.Height = 80; element.Text = "工位检测结果"; }
            if (kind == RuntimeWidgetKind.RunControl) { element.Width = 128; element.Height = 56; element.Background = "#282828"; }
            if (kind == RuntimeWidgetKind.Log) { element.Width = 600; element.Height = 260; element.FontSize = 12; element.Background = "#282828"; }
            if (kind == RuntimeWidgetKind.ManualParameters || kind == RuntimeWidgetKind.OneClickLearning)
            { element.Width = 160; element.Height = 52; element.FontSize = 16; element.Background = "#282828"; }
            return element;
        }

        /// <summary>获得四类组件的简体中文名称。</summary>
        public static string GetName(RuntimeWidgetKind kind)
        {
            switch (kind)
            {
                case RuntimeWidgetKind.Image: return "图像显示";
                case RuntimeWidgetKind.Elapsed: return "运行耗时";
                case RuntimeWidgetKind.Text: return "文本框";
                case RuntimeWidgetKind.Signal: return "信号灯";
                case RuntimeWidgetKind.RunControl: return "运行控制";
                case RuntimeWidgetKind.Log: return "日志框";
                case RuntimeWidgetKind.ManualParameters: return "手动参数";
                case RuntimeWidgetKind.OneClickLearning: return "一键学习";
                default: throw new ArgumentOutOfRangeException(nameof(kind));
            }
        }

        /// <summary>按稳定标识查找组件。</summary>
        public RuntimeLayoutElement Find(string id) => Document.Elements.FirstOrDefault(e => e.Id == id);

        /// <summary>拖动和缩放提交为一次可撤销操作。</summary>
        public void SetBounds(string id, Rectangle bounds)
        {
            var element = Find(id);
            Rectangle constrained = Constrain(bounds);
            if (element == null || element.Bounds == constrained) return;
            Edit(doc => ApplyBounds(element, constrained));
        }

        /// <summary>批量提交移动或缩放；忽略失效标识，所有有效变化共用一次撤销事务。</summary>
        public void SetBounds(IReadOnlyDictionary<string, Rectangle> bounds)
        {
            if (bounds == null) throw new ArgumentNullException(nameof(bounds));
            var changes = new List<KeyValuePair<RuntimeLayoutElement, Rectangle>>();
            foreach (var element in Document.Elements)
            {
                Rectangle value;
                if (!bounds.TryGetValue(element.Id, out value)) continue;
                value = Constrain(value);
                if (value != element.Bounds) changes.Add(new KeyValuePair<RuntimeLayoutElement, Rectangle>(element, value));
            }
            if (changes.Count == 0) return;
            Edit(doc => { foreach (var change in changes) ApplyBounds(change.Key, change.Value); });
        }

        /// <summary>限制最小尺寸和页面边界。</summary>
        public Rectangle Constrain(Rectangle bounds)
        {
            int width = Math.Max(48, Math.Min(Document.Width, bounds.Width));
            int height = Math.Max(32, Math.Min(Document.Height, bounds.Height));
            return new Rectangle(Math.Max(0, Math.Min(Document.Width - width, bounds.X)),
                Math.Max(0, Math.Min(Document.Height - height, bounds.Y)), width, height);
        }

        /// <summary>更新独立属性副本，失败时不污染原组件。</summary>
        public void UpdateElement(RuntimeLayoutElement replacement)
        {
            Edit(doc =>
            {
                int index = doc.Elements.FindIndex(e => e.Id == replacement.Id);
                if (index >= 0) doc.Elements[index] = replacement;
            });
        }

        /// <summary>移除选中组件。</summary>
        public void Remove(string id) => Edit(doc => doc.Elements.RemoveAll(e => e.Id == id));

        /// <summary>一次删除选中集合，重复或失效标识不影响其它组件。</summary>
        public void Remove(IEnumerable<string> ids)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var selected = new HashSet<string>(ids, StringComparer.Ordinal);
            if (!Document.Elements.Any(element => selected.Contains(element.Id))) return;
            Edit(doc => doc.Elements.RemoveAll(element => selected.Contains(element.Id)));
        }

        /// <summary>复制组件并生成新标识，保留内嵌图片。</summary>
        public RuntimeLayoutElement Duplicate(string id)
        {
            var source = Find(id);
            if (source == null) return null;
            return InsertCopy(source, new Point(source.X + 24, source.Y + 24));
        }

        /// <summary>插入独立组件快照，跨页面约束边界，并只提交一次可撤销编辑。</summary>
        public RuntimeLayoutElement InsertCopy(RuntimeLayoutElement source, Point location)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            var copy = CreateCopy(source);
            ApplyBounds(copy, Constrain(new Rectangle(location, new Size(copy.Width, copy.Height))));
            Edit(doc => doc.Elements.Add(copy));
            return copy;
        }

        /// <summary>按相对偏移插入一组副本；整体回移到页面内，绝不逐个压缩造成重叠。</summary>
        public IReadOnlyList<RuntimeLayoutElement> InsertCopies(IEnumerable<RuntimeLayoutElement> sources, Point offset)
        {
            if (sources == null) throw new ArgumentNullException(nameof(sources));
            var values = sources.Take(201).ToList();
            if (values.Count > 200 || Document.Elements.Count + values.Count > 200)
                throw new InvalidOperationException("组件总数不能超过200个。");
            if (values.Any(value => value == null)) throw new ArgumentException("复制集合不能包含空组件。", nameof(sources));
            if (values.Count == 0) return new RuntimeLayoutElement[0];
            if (values.Count == 1)
                return new[] { InsertCopy(values[0], new Point(ClampCoordinate((long)values[0].X + offset.X), ClampCoordinate((long)values[0].Y + offset.Y))) };
            if (values.Any(value => value.Width < 48 || value.Height < 32))
                throw new InvalidOperationException("复制组件尺寸至少为48×32。");
            long left = values.Min(value => (long)value.X), top = values.Min(value => (long)value.Y);
            long width = values.Max(value => (long)value.X + value.Width) - left;
            long height = values.Max(value => (long)value.Y + value.Height) - top;
            if (width > Document.Width || height > Document.Height)
                throw new InvalidOperationException("选中组件整体超出目标页面，无法保持相对位置粘贴。");
            long dx = Math.Max(0, Math.Min(Document.Width - width, left + offset.X)) - left;
            long dy = Math.Max(0, Math.Min(Document.Height - height, top + offset.Y)) - top;
            var copies = values.Select(CreateCopy).ToArray();
            foreach (var copy in copies) { copy.X = (int)(copy.X + dx); copy.Y = (int)(copy.Y + dy); }
            Edit(doc => doc.Elements.AddRange(copies));
            return copies;
        }

        /// <summary>按原位置稳定排序后横向或纵向等间距排列，尺寸不变，整体占用一次撤销。</summary>
        public void Arrange(IEnumerable<string> ids, bool horizontal)
        {
            if (ids == null) throw new ArgumentNullException(nameof(ids));
            var selectedIds = new HashSet<string>(ids, StringComparer.Ordinal);
            // 相同轴坐标以文档原有顺序打破平局，不受框选方向或HashSet枚举顺序影响。
            var selected = Document.Elements.Where(value => selectedIds.Contains(value.Id))
                .OrderBy(value => horizontal ? value.X : value.Y).ToArray();
            if (selected.Length < 2) return;
            int pageLength = horizontal ? Document.Width : Document.Height;
            int pageCross = horizontal ? Document.Height : Document.Width;
            long total = selected.Sum(value => (long)(horizontal ? value.Width : value.Height));
            if (total > pageLength)
                throw new InvalidOperationException(horizontal ? "选中组件总宽度超过页面，无法横向排列。" : "选中组件总高度超过页面，无法纵向排列。");
            int originalStart = selected.Min(value => horizontal ? value.X : value.Y);
            long originalEnd = selected.Max(value => horizontal ? (long)value.X + value.Width : (long)value.Y + value.Height);
            int span = (int)Math.Min(pageLength, Math.Max(originalEnd - originalStart, total + 12L * (selected.Length - 1)));
            int start = Math.Max(0, Math.Min(originalStart, pageLength - span));
            int cross = selected.Min(value => horizontal ? value.Y : value.X);
            int maximumCross = selected.Max(value => horizontal ? value.Height : value.Width);
            cross = Math.Max(0, Math.Min(cross, pageCross - maximumCross));
            var bounds = new Dictionary<string, Rectangle>(StringComparer.Ordinal);
            long sizesBefore = 0;
            for (int index = 0; index < selected.Length; index++)
            {
                var value = selected[index];
                // 累计舍入把不足一像素的余量均匀分散，最后一项准确落在跨度终点。
                int position = start + (int)sizesBefore + (int)Math.Round((span - total) * index / (double)(selected.Length - 1), MidpointRounding.AwayFromZero);
                bounds.Add(value.Id, new Rectangle(horizontal ? position : cross, horizontal ? cross : position, value.Width, value.Height));
                sizesBefore += horizontal ? value.Width : value.Height;
            }
            SetBounds(bounds);
        }

        /// <summary>复制共用的深拷贝、新标识及有界名称规则。</summary>
        private static RuntimeLayoutElement CreateCopy(RuntimeLayoutElement source)
        {
            var copy = source.Copy();
            copy.Id = Guid.NewGuid().ToString("N");
            string name = copy.Name ?? string.Empty;
            int length = Math.Min(name.Length, 117);
            if (length < name.Length && length > 0 && char.IsHighSurrogate(name[length - 1])) length--;
            copy.Name = name.Substring(0, length) + " 副本";
            return copy;
        }

        /// <summary>先用长整数计算跨页偏移，再饱和到现有单组件约束入口，避免整数回绕。</summary>
        private static int ClampCoordinate(long value) => (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, value));

        /// <summary>导入通过校验的模板，并允许撤销整个导入。</summary>
        public void Replace(RuntimeLayoutDocument document)
        {
            var copy = RuntimeLayoutSerializer.Clone(document);
            Edit(ignored => Document = copy);
        }

        /// <summary>撤销最近一次编辑。</summary>
        public void Undo() => Restore(undo, redo);
        /// <summary>恢复最近一次撤销。</summary>
        public void Redo() => Restore(redo, undo);

        /// <summary>转移历史快照并通知界面重新选中组件。</summary>
        private void Restore(List<string> from, List<string> to)
        {
            if (from.Count == 0) return;
            var document = RuntimeLayoutSerializer.Deserialize(from[from.Count - 1]);
            Push(to, RuntimeLayoutSerializer.Serialize(Document));
            from.RemoveAt(from.Count - 1);
            Document = document;
            Changed?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>历史最多 30 步且最多约 32 MB 字符存储。</summary>
        private static void Push(List<string> history, string snapshot)
        {
            history.Add(snapshot);
            long total = history.Sum(s => (long)s.Length * 2);
            while (history.Count > 1 && (history.Count > 30 || total > 32 * 1024 * 1024))
            { total -= history[0].Length * 2L; history.RemoveAt(0); }
        }

        /// <summary>写入统一的逻辑边界字段。</summary>
        private static void ApplyBounds(RuntimeLayoutElement element, Rectangle bounds)
        {
            element.X = bounds.X; element.Y = bounds.Y; element.Width = bounds.Width; element.Height = bounds.Height;
        }
    }
}
