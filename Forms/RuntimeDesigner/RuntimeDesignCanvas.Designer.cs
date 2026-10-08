using System.ComponentModel;
using System.Windows.Forms;

namespace TDJS_Vision.Forms.RuntimeDesigner
{
    /// <summary>画布固定菜单放在设计器文件，图像交互逻辑位于独立分部文件。</summary>
    partial class RuntimeDesignCanvas
    {
        /// <summary>原生菜单组件生命周期。</summary>
        private IContainer components;
        /// <summary>只在命中图像控件时显示的菜单。</summary>
        private ContextMenuStrip imageMenu;
        /// <summary>清除当前显示图像，不移除控件或订阅。</summary>
        private ToolStripMenuItem clearImageMenuItem;

        /// <summary>创建固定菜单和中文命令。</summary>
        private void InitializeComponent()
        {
            components = new Container();
            imageMenu = new ContextMenuStrip(components);
            clearImageMenuItem = new ToolStripMenuItem();
            clearImageMenuItem.Name = "clearImageMenuItem";
            clearImageMenuItem.Text = "清除图像";
            clearImageMenuItem.Click += ClearImageMenuClicked;
            imageMenu.Items.Add(clearImageMenuItem);
            imageMenu.Closed += ImageMenuClosed;
        }
    }
}
