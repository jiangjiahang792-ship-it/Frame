using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.FlowDirectionPanelContrls
{
    public enum AddDirection
    {
        /// <summary>添加到视觉底部，新控件在最下面</summary>
        Down,
        /// <summary>添加到视觉顶部，新控件在最上面</summary>
        Up
    }

    public class FlowDirectionPanel : Panel
    {
        private AddDirection _direction = AddDirection.Down;
        private Control _selectedControl = null;
        public int SelectedIndex { get; set; } = -1;

        private Color _selectedColor = Color.LightBlue;
        private Color _defaultColor = SystemColors.Control;

        /// <summary>
        /// 控件选中下标发生变化后事件
        /// </summary>
        public event Action<Control, int> SelectedControlChanged;

        /// <summary>控件移动事件（包含原索引和目标索引）</summary>
        public event EventHandler<ControlMovedEventArgs> ControlMoved;

        public FlowDirectionPanel()
        {
            AutoScroll = true;
            SetStyle(ControlStyles.UserPaint |
                      ControlStyles.AllPaintingInWmPaint |
                      ControlStyles.OptimizedDoubleBuffer, true);
            UpdateStyles();
            // 关键：取消控件的锚定，避免布局错乱
            this.ControlAdded += (s, e) =>
            {
                e.Control.Anchor = AnchorStyles.Left | AnchorStyles.Top | AnchorStyles.Right;
                e.Control.Margin = new Padding(0); // 取消边距，避免间隙
            };
        }

        [Description("设置子控件添加方向：Up = 添加到顶部；Down = 添加到底部")]
        [Category("Layout")]
        public AddDirection AddDirection
        {
            get => _direction;
            set
            {
                _direction = value;
                ReLayout(); // 切换方向时重新布局
            }
        }

        public Control GetSelectedControl() => _selectedControl;
        public int GetSelectedIndex() => SelectedIndex;

        public void SelectControl(Control ctrl)
        {
            if (ctrl != null && Controls.Contains(ctrl))
            {
                SelectedIndex = Controls.IndexOf(ctrl); // Down模式下，Index=0是顶部，Count-1是底部
                ApplySelectState(ctrl);
            }
            else
            {
                ResetSelectState();
            }
        }

        public void SelectControl(int index)
        {
            if (index >= 0 && index < Controls.Count)
            {
                ApplySelectState(Controls[index]);
                SelectedIndex = index;
            }
            else
            {
                ResetSelectState();
            }
        }

        /// <summary>核心修复：AddDirection=Down时，严格添加到视觉底部</summary>
        public void AddControl(Control ctrl)
        {
            if (ctrl == null) return;

            SuspendLayout();

            if (_direction == AddDirection.Down)
            {
                // Down模式：添加到Controls末尾 → 视觉底部
                Controls.Add(ctrl);
                // 布局时让新控件在最下面，无需BringToFront
            }
            else
            {
                // Up模式：添加到Controls开头 → 视觉顶部
                Controls.Add(ctrl);
                ctrl.BringToFront(); // 仅Up模式需要置顶
            }

            RegisterClickEvent(ctrl);
            ReLayout(); // 重新布局，确保位置正确

            // 选中新增控件（Down模式下，新控件Index=Count-1；Up模式下Index=0）
            SelectedIndex = Controls.IndexOf(ctrl);
            ApplySelectState(ctrl);

            ResumeLayout(true); // 强制刷新布局
        }

        /// <summary>
        /// 触发控件移动事件（私有辅助方法）
        /// </summary>
        /// <param name="movedControl">被移动的控件</param>
        /// <param name="originalIndex">移动前的原索引</param>
        /// <param name="targetIndex">移动后的目标索引</param>
        private void OnControlMoved(Control movedControl, int originalIndex, int targetIndex)
        {
            // 触发事件（空值判断避免空引用）
            ControlMoved?.Invoke(this, new ControlMovedEventArgs(movedControl, originalIndex, targetIndex));
        }

        public void RemoveControl(Control ctrl)
        {
            if (ctrl == null || !Controls.Contains(ctrl))
                return;

            SuspendLayout();

            bool wasSelected = (_selectedControl == ctrl);
            int oldIndex = Controls.IndexOf(ctrl);

            UnRegisterClickEvent(ctrl);
            Controls.Remove(ctrl);
            ctrl.Dispose();

            // 自动选中逻辑（Down模式：删除后选中上一个=oldIndex-1）
            if (wasSelected && Controls.Count > 0)
            {
                int newIndex = Math.Max(0, oldIndex - 1); // 避免负数
                if (newIndex >= Controls.Count) newIndex = Controls.Count - 1;
                SelectControl(newIndex);
            }
            else if (wasSelected)
            {
                ResetSelectState(); // 无控件时重置
            }
            else
            {
                ValidateSelectedIndex();
            }

            ReLayout();
            ResumeLayout(true);
        }

        private void RegisterClickEvent(Control ctrl)
        {
            ctrl.MouseDown += AnyControl_MouseDown;
            foreach (Control child in ctrl.Controls)
                RegisterClickEvent(child);
        }

        private void UnRegisterClickEvent(Control ctrl)
        {
            ctrl.MouseDown -= AnyControl_MouseDown;
            foreach (Control child in ctrl.Controls)
                UnRegisterClickEvent(child);
        }

        private void AnyControl_MouseDown(object sender, MouseEventArgs e)
        {
            Control clicked = sender as Control;
            // 找到当前面板下的最外层控件
            while (clicked.Parent != this && clicked.Parent != null)
                clicked = clicked.Parent;

            if (Controls.Contains(clicked))
            {
                SelectControl(clicked); // 触发选中逻辑
            }
        }

        private void ApplySelectState(Control newSelected)
        {
            if (_selectedControl == newSelected) return;

            // 重置上一个选中控件的颜色
            if (_selectedControl != null)
            {
                _selectedControl.BackColor = _defaultColor;
            }

            // 设置新选中控件的颜色
            newSelected.BackColor = _selectedColor;
            _selectedControl = newSelected;

            // 触发选中变更事件
            SelectedControlChanged?.Invoke(newSelected, SelectedIndex);
        }

        private void ResetSelectState()
        {
            if (_selectedControl != null)
            {
                _selectedControl.BackColor = _defaultColor;
            }
            _selectedControl = null;
            SelectedIndex = -1;
            SelectedControlChanged?.Invoke(null, -1);
        }

        private void ValidateSelectedIndex()
        {
            if (SelectedIndex < 0 || SelectedIndex >= Controls.Count)
            {
                SelectedIndex = -1;
                _selectedControl = null;
            }
        }

        /// <summary>最终修复：布局逻辑严格对应视觉顺序 + 支持滚动</summary>
        private void ReLayout()
        {
            if (Controls.Count == 0)
            {
                AutoScrollMinSize = Size.Empty;
                return;
            }

            int currentY = 0; // 从顶部开始布局
            int totalHeight = 0; // 记录所有子控件的总高度
            // Down模式：遍历Controls正序 → 0=顶部，Count-1=底部
            // Up模式：遍历Controls倒序 → 新增的控件在顶部
            IEnumerable<Control> layoutControls = _direction == AddDirection.Down
                ? Controls.Cast<Control>()
                : Controls.Cast<Control>().Reverse();

            foreach (Control c in layoutControls)
            {
                // 计算有效宽度：减去滚动条宽度（如果显示）
                int controlWidth = ClientSize.Width - (AutoScroll ? SystemInformation.VerticalScrollBarWidth : 0);
                // 避免宽度为负数（面板尺寸过小时）
                controlWidth = Math.Max(0, controlWidth);

                // 强制设置位置：X=0，Y=currentY，宽度=面板客户区宽度
                c.SetBounds(0, currentY, controlWidth, c.Height);
                currentY += c.Height; // Y轴累加，下一个控件在当前控件下方
            }

            // 关键修复：设置AutoScrollMinSize，让面板感知滚动范围
            totalHeight = currentY;
            AutoScrollMinSize = new Size(ClientSize.Width, totalHeight);

            // 调整滚动条
            AutoScrollPosition = new Point(0, 0);
        }

        // 面板尺寸变化时重新布局
        protected override void OnSizeChanged(EventArgs e)
        {
            base.OnSizeChanged(e);
            ReLayout();
        }

        // 确保滚动条不影响布局
        protected override void OnClientSizeChanged(EventArgs e)
        {
            base.OnClientSizeChanged(e);
            ReLayout();
        }

        /// <summary>
        /// 将选中的控件上移一位（视觉上）
        /// </summary>
        /// <returns>是否移动成功</returns>
        public bool MoveSelectedControlUp()
        {
            // 无选中控件 或 已经是第一个 → 移动失败
            if (_selectedControl == null || Controls.IndexOf(_selectedControl) <= 0)
                return false;

            SuspendLayout();

            // 1. 记录移动前的原索引
            int originalIndex = Controls.IndexOf(_selectedControl);
            int targetIndex = originalIndex - 1;

            // 2. 调整控件索引实现上移
            Controls.SetChildIndex(_selectedControl, targetIndex);

            // 3. 同步选中索引 + 重新布局
            SelectedIndex = targetIndex;
            ReLayout();
            ApplySelectState(_selectedControl); // 保持选中状态

            // 4. 触发移动事件（核心：传递原索引和目标索引）
            OnControlMoved(_selectedControl, originalIndex, targetIndex);

            ResumeLayout(true);
            return true;
        }

        /// <summary>
        /// 将选中的控件下移一位（视觉上）
        /// </summary>
        /// <returns>是否移动成功</returns>
        public bool MoveSelectedControlDown()
        {
            // 无选中控件 或 已经是最后一个 → 移动失败
            if (_selectedControl == null || Controls.IndexOf(_selectedControl) >= Controls.Count - 1)
                return false;

            SuspendLayout();

            // 1. 记录移动前的原索引
            int originalIndex = Controls.IndexOf(_selectedControl);
            int targetIndex = originalIndex + 1;

            // 2. 调整控件索引实现下移
            Controls.SetChildIndex(_selectedControl, targetIndex);

            // 3. 同步选中索引 + 重新布局
            SelectedIndex = targetIndex;
            ReLayout();
            ApplySelectState(_selectedControl); // 保持选中状态

            // 4. 触发移动事件（核心：传递原索引和目标索引）
            OnControlMoved(_selectedControl, originalIndex, targetIndex);

            ResumeLayout(true);
            return true;
        }
    }

    /// <summary>
    /// 控件移动事件参数（包含原索引和目标索引）
    /// </summary>
    public class ControlMovedEventArgs : EventArgs
    {
        /// <summary>移动前的原索引</summary>
        public int OriginalIndex { get; }

        /// <summary>移动后的目标索引</summary>
        public int TargetIndex { get; }

        /// <summary>被移动的控件</summary>
        public Control MovedControl { get; }

        public ControlMovedEventArgs(Control movedControl, int originalIndex, int targetIndex)
        {
            MovedControl = movedControl;
            OriginalIndex = originalIndex;
            TargetIndex = targetIndex;
        }
    }
}