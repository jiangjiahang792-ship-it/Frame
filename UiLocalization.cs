using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace TDJS_Vision
{
    /// <summary>统一管理界面显示翻译，保留输入内容、选项值和设计器中文默认值。</summary>
    public static class UiLocalization
    {
        /// <summary>以弱引用记录控件绑定，控件释放后不阻止垃圾回收。</summary>
        private static readonly ConditionalWeakTable<Control, ControlBinding> Bindings = new ConditionalWeakTable<Control, ControlBinding>();
        /// <summary>由业务层明确控制显示的名称，避免把用户名称识别为提示句式。</summary>
        private static readonly ConditionalWeakTable<Control, object> PreservedTexts = new ConditionalWeakTable<Control, object>();

        /// <summary>在Attach之前标记数据标题，翻译由调用方按内置名称规则处理。</summary>
        public static void PreserveText(Control control)
        {
            if (control != null) PreservedTexts.GetValue(control, key => new object());
        }

        /// <summary>当前界面线程拥有的根控件，用于切换语言时刷新。</summary>
        private static readonly List<WeakReference> Roots = new List<WeakReference>();

        /// <summary>已绑定的菜单项，防止重复注册事件。</summary>
        private static readonly ConditionalWeakTable<ToolStripItem, List<TextBinding>> MenuBindings = new ConditionalWeakTable<ToolStripItem, List<TextBinding>>();

        /// <summary>初始化语言切换通知。</summary>
        static UiLocalization()
        {
            LanguageManager.LanguageChanged += OnLanguageChanged;
        }

        /// <summary>绑定窗体或用户控件；设计器环境不执行翻译。</summary>
        /// <param name="root">需要翻译的控件树根节点。</param>
        public static void Attach(Control root)
        {
            if (root == null || root.IsDisposed || LicenseManager.UsageMode == LicenseUsageMode.Designtime)
                return;
            lock (Roots)
            {
                Roots.RemoveAll(reference => !reference.IsAlive);
                if (!Roots.Any(reference => ReferenceEquals(reference.Target, root)))
                    Roots.Add(new WeakReference(root));
            }
            AttachControl(root);
        }

        /// <summary>为控件及后续加入的子控件安装事件绑定。</summary>
        /// <param name="control">当前控件。</param>
        private static void AttachControl(Control control)
        {
            if (Bindings.TryGetValue(control, out var existing))
            {
                existing.Refresh();
                return;
            }
            var binding = new ControlBinding(control);
            Bindings.Add(control, binding);
            control.ControlAdded += OnControlAdded;
            foreach (Control child in control.Controls)
                AttachControl(child);
            binding.Refresh();
        }

        /// <summary>绑定运行时新建的子控件。</summary>
        private static void OnControlAdded(object sender, ControlEventArgs e)
        {
            AttachControl(e.Control);
        }

        /// <summary>语言变更时在各窗口的界面线程刷新，不在采图或推理线程遍历控件。</summary>
        private static void OnLanguageChanged(object sender, EventArgs e)
        {
            Control[] roots;
            lock (Roots)
            {
                Roots.RemoveAll(reference => !(reference.Target is Control control) || control.IsDisposed);
                roots = Roots.Select(reference => reference.Target).OfType<Control>().ToArray();
            }
            foreach (Control root in roots)
            {
                if (root.IsDisposed) continue;
                if (root.IsHandleCreated && root.InvokeRequired)
                {
                    try { root.BeginInvoke(new Action(() => RefreshTree(root))); }
                    catch (InvalidOperationException) { /* 窗口已关闭，无需再刷新。 */ }
                }
                else RefreshTree(root);
            }
        }

        /// <summary>刷新已绑定的控件树。</summary>
        private static void RefreshTree(Control control)
        {
            if (control.IsDisposed) return;
            if (Bindings.TryGetValue(control, out var binding)) binding.Refresh();
            foreach (Control child in control.Controls) RefreshTree(child);
        }

        /// <summary>维护一个显示属性的源文本，避免多次切换后丢失中文键。</summary>
        private sealed class TextBinding
        {
            /// <summary>读取显示值。</summary>
            private readonly Func<string> read;
            /// <summary>写入显示值。</summary>
            private readonly Action<string> write;
            /// <summary>最近一次由业务代码提供的源文字。</summary>
            private string source;
            /// <summary>最近一次写入的翻译。</summary>
            private string rendered;
            /// <summary>防止TextChanged递归。</summary>
            private bool updating;

            /// <summary>创建显示属性绑定。</summary>
            public TextBinding(Func<string> read, Action<string> write)
            {
                this.read = read;
                this.write = write;
                source = read();
            }

            /// <summary>保存业务代码的新文字，再应用当前语言。</summary>
            public void Refresh()
            {
                if (updating) return;
                string current = read();
                if (current != rendered) source = current;
                string translated = LanguageManager.TranslateDisplay(source);
                rendered = translated;
                if (current == translated) return;
                updating = true;
                try { write(translated); }
                finally { updating = false; }
            }
        }

        /// <summary>控件文本、菜单、表头和选项绘制的生命周期绑定。</summary>
        private sealed class ControlBinding
        {
            /// <summary>绑定的控件。</summary>
            private readonly Control control;
            /// <summary>控件拥有的显示属性。</summary>
            private readonly List<TextBinding> texts = new List<TextBinding>();
            /// <summary>已经绑定的表头。</summary>
            private readonly HashSet<DataGridViewColumn> columns = new HashSet<DataGridViewColumn>();
            /// <summary>未挂在Controls集合中的弹出菜单。</summary>
            private readonly List<ToolStrip> detachedMenus = new List<ToolStrip>();

            /// <summary>创建控件绑定，输入框和选项内部值不参与Text替换。</summary>
            public ControlBinding(Control control)
            {
                this.control = control;
                if (!PreservedTexts.TryGetValue(control, out var preserved) && !(control is TextBoxBase) && !(control is ComboBox) && !(control is ListBox) && !(control is UpDownBase))
                {
                    var text = new TextBinding(() => control.Text, value => control.Text = value);
                    texts.Add(text);
                    control.TextChanged += (sender, args) => text.Refresh();
                }
                if (control is ComboBox combo && combo.DrawMode == DrawMode.Normal)
                {
                    // 仅改变绘制文本，SelectedItem、Text及保存到方案中的值保持不变。
                    combo.DrawMode = DrawMode.OwnerDrawFixed;
                    combo.DrawItem += DrawComboItem;
                }
                if (control is DataGridView grid)
                {
                    grid.ColumnAdded += (sender, args) => BindColumn(args.Column);
                    foreach (DataGridViewColumn column in grid.Columns) BindColumn(column);
                }
                if (control is ListView list)
                    foreach (ColumnHeader column in list.Columns)
                        texts.Add(new TextBinding(() => column.Text, value => column.Text = value));
                if (control is ToolStrip strip)
                    strip.ItemAdded += (sender, args) => RefreshMenu(args.Item);
                control.ContextMenuStripChanged += (sender, args) => Refresh();
                // ToolTip不是Control，通过当前窗体字段读取一次，不修改Tag或业务对象。
                foreach (FieldInfo field in control.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public))
                {
                    object component = field.GetValue(control);
                    if (component is ToolTip toolTip)
                        BindToolTips(control, toolTip);
                    else if (component is ContextMenuStrip popup)
                    {
                        detachedMenus.Add(popup);
                        popup.ItemAdded += (sender, args) => RefreshMenu(args.Item);
                    }
                    else if (component is FileDialog dialog)
                    {
                        texts.Add(new TextBinding(() => dialog.Title, value => dialog.Title = value));
                        texts.Add(new TextBinding(() => dialog.Filter, value => dialog.Filter = value));
                    }
                    else if (component is FolderBrowserDialog folder)
                        texts.Add(new TextBinding(() => folder.Description, value => folder.Description = value));
                }
            }

            /// <summary>绑定表格列标题，数据单元格和数据源不作替换。</summary>
            private void BindColumn(DataGridViewColumn column)
            {
                if (!columns.Add(column)) return;
                var binding = new TextBinding(() => column.HeaderText, value => column.HeaderText = value);
                texts.Add(binding);
                binding.Refresh();
            }

            /// <summary>绑定设计器配置的提示文字。</summary>
            private void BindToolTips(Control parent, ToolTip toolTip)
            {
                foreach (Control child in parent.Controls)
                {
                    if (!string.IsNullOrEmpty(toolTip.GetToolTip(child)))
                        texts.Add(new TextBinding(() => toolTip.GetToolTip(child), value => toolTip.SetToolTip(child, value)));
                    BindToolTips(child, toolTip);
                }
            }

            /// <summary>刷新显示属性，并绑定新菜单项。</summary>
            public void Refresh()
            {
                foreach (TextBinding text in texts) text.Refresh();
                if (control is ToolStrip strip)
                    foreach (ToolStripItem item in strip.Items) RefreshMenu(item);
                if (control.ContextMenuStrip != null)
                    foreach (ToolStripItem item in control.ContextMenuStrip.Items) RefreshMenu(item);
                foreach (ToolStrip menu in detachedMenus)
                    if (!menu.IsDisposed)
                        foreach (ToolStripItem item in menu.Items) RefreshMenu(item);
                control.Invalidate();
            }

            /// <summary>绘制选项的本地化显示，不触发SelectedIndexChanged或设备写入。</summary>
            private void DrawComboItem(object sender, DrawItemEventArgs e)
            {
                var combo = (ComboBox)sender;
                e.DrawBackground();
                string value = e.Index >= 0 && e.Index < combo.Items.Count ? combo.GetItemText(combo.Items[e.Index]) : combo.Text;
                TextRenderer.DrawText(e.Graphics, LanguageManager.TranslateDisplay(value), e.Font,
                    e.Bounds, e.ForeColor, TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                e.DrawFocusRectangle();
            }
        }

        /// <summary>绑定菜单标题及悬停提示，递归覆盖下拉菜单。</summary>
        private static void RefreshMenu(ToolStripItem item)
        {
            if (!MenuBindings.TryGetValue(item, out var bindings))
            {
                var binding = new TextBinding(() => item.Text, value => item.Text = value);
                var tip = new TextBinding(() => item.ToolTipText, value => item.ToolTipText = value);
                bindings = new List<TextBinding> { binding, tip };
                MenuBindings.Add(item, bindings);
                TextBinding captured = binding;
                item.TextChanged += (sender, args) => captured.Refresh();
                item.OwnerChanged += (sender, args) => tip.Refresh();
                if (item is ToolStripDropDownItem parent)
                    parent.DropDownOpening += (sender, args) => RefreshMenu(parent);
            }
            foreach (TextBinding binding in bindings) binding.Refresh();
            if (item is ToolStripDropDownItem dropdown)
                foreach (ToolStripItem child in dropdown.DropDownItems) RefreshMenu(child);
        }
    }
}
