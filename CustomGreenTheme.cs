using System.Drawing;
using WeifenLuo.WinFormsUI.Docking;
using WeifenLuo.WinFormsUI.ThemeVS2012;

namespace TDJS_Vision
{
    public class GreenTheme : ThemeBase
    {
        VS2015LightTheme vs2015theme = new VS2015LightTheme();
        public GreenTheme()
        {
            base.ColorPalette = new DockPanelColorPalette(new GreenPaletteFactory());
            base.Skin = new DockPanelSkin();
            base.PaintingService = new PaintingService();
            base.ImageService = new ImageService(this);
            base.ToolStripRenderer = new VisualStudioToolStripRenderer(base.ColorPalette)
            {
                UseGlassOnMenuStrip = false
            };
            base.Measures.SplitterSize = 6;
            base.Measures.AutoHideSplitterSize = 3;
            base.Measures.DockPadding = 6;
            base.ShowAutoHideContentOnHover = false;
            base.Extender.AutoHideStripFactory = vs2015theme.Extender.AutoHideStripFactory;
            base.Extender.AutoHideWindowFactory = vs2015theme.Extender.AutoHideWindowFactory;
            base.Extender.DockPaneFactory = vs2015theme.Extender.DockPaneFactory;
            base.Extender.DockPaneCaptionFactory = vs2015theme.Extender.DockPaneCaptionFactory;
            base.Extender.DockPaneStripFactory = vs2015theme.Extender.DockPaneStripFactory;
            base.Extender.DockPaneSplitterControlFactory = vs2015theme.Extender.DockPaneSplitterControlFactory;
            base.Extender.WindowSplitterControlFactory = vs2015theme.Extender.WindowSplitterControlFactory;
            base.Extender.DockWindowFactory = vs2015theme.Extender.DockWindowFactory;
            base.Extender.PaneIndicatorFactory = vs2015theme.Extender.PaneIndicatorFactory;
            base.Extender.PanelIndicatorFactory = vs2015theme.Extender.PanelIndicatorFactory;
            base.Extender.DockOutlineFactory = vs2015theme.Extender.DockOutlineFactory;
            base.Extender.DockIndicatorFactory = vs2015theme.Extender.DockIndicatorFactory;
        }

        public override void CleanUp(DockPanel dockPanel)
        {
            base.PaintingService.CleanUp();
            base.CleanUp(dockPanel);
        }
    }

    public class GreenPaletteFactory : IPaletteFactory
    {
        public void Initialize(DockPanelColorPalette palette)
        {
            // 工具提示/自动隐藏条
            palette.AutoHideStripDefault.Background = Color.FromArgb(40, 120, 120);
            palette.AutoHideStripDefault.Border = Color.FromArgb(39, 174, 96);
            palette.AutoHideStripDefault.Text = Color.White;

            palette.AutoHideStripHovered.Background = Color.FromArgb(50, 220, 120);
            palette.AutoHideStripHovered.Border = Color.FromArgb(40, 180, 100);
            palette.AutoHideStripHovered.Text = Color.White;

            // 标签页 - 未选中
            palette.TabUnselected.Background = Color.MediumTurquoise;
            palette.TabUnselected.Text = Color.White;

            palette.TabUnselectedHovered.Background = Color.LightSeaGreen;
            palette.TabUnselectedHovered.Text = Color.White;

            // 标签页 - 选中（激活状态）
            palette.TabSelectedActive.Background = Color.LightSeaGreen;
            palette.TabSelectedActive.Text = Color.White;
            palette.TabSelectedActive.Button = Color.White;

            // 标签页 - 选中（非激活状态）
            palette.TabSelectedInactive.Background = Color.MediumTurquoise;
            palette.TabSelectedInactive.Text = Color.White;
            palette.TabSelectedInactive.Button = Color.White;

            // 主窗口背景
            palette.MainWindowActive.Background = SystemColors.Control;

            // 状态栏
            palette.MainWindowStatusBarDefault.Background = Color.MediumTurquoise;
            palette.MainWindowStatusBarDefault.Text = Color.White;
            palette.MainWindowStatusBarDefault.Highlight = Color.MediumTurquoise;
            palette.MainWindowStatusBarDefault.HighlightText = Color.White;

            // 工具窗口标题栏 - 激活
            palette.ToolWindowCaptionActive.Background = Color.LightSeaGreen;
            palette.ToolWindowCaptionActive.Text = Color.White;
            palette.ToolWindowCaptionActive.Button = Color.White;

            // 工具窗口标题栏 - 非激活
            palette.ToolWindowCaptionInactive.Background = Color.MediumTurquoise;
            palette.ToolWindowCaptionInactive.Text = Color.White;
            palette.ToolWindowCaptionInactive.Button = Color.White;

            // 工具窗口标签页 - 选中
            palette.ToolWindowTabSelectedActive.Background = Color.LightSeaGreen;
            palette.ToolWindowTabSelectedActive.Text = Color.White;

            // 工具窗口标签页 - 未选中
            palette.ToolWindowTabUnselected.Background = Color.MediumTurquoise;
            palette.ToolWindowTabUnselected.Text = Color.White;

            palette.ToolWindowTabUnselectedHovered.Background = Color.LightSeaGreen;
            palette.ToolWindowTabUnselectedHovered.Text = Color.White;

            // 分隔线与边框
            palette.ToolWindowSeparator = Color.FromArgb(40, 40, 40);
            palette.ToolWindowBorder = Color.FromArgb(30, 30, 30);

            // 命令栏 / 菜单
            palette.CommandBarMenuDefault.Background = Color.FromArgb(39, 130, 96);
            palette.CommandBarMenuDefault.Text = Color.White;

            palette.CommandBarMenuPopupDefault.BackgroundTop = Color.FromArgb(39, 130, 96);
            palette.CommandBarMenuPopupDefault.BackgroundBottom = Color.FromArgb(30, 30, 30);
            palette.CommandBarMenuPopupDefault.Border = Color.FromArgb(60, 60, 60);
            palette.CommandBarMenuPopupDefault.Separator = Color.FromArgb(60, 60, 60);
            palette.CommandBarMenuPopupDefault.CheckmarkBackground = Color.FromArgb(46, 204, 113);

            palette.CommandBarMenuPopupHovered.ItemBackground = Color.FromArgb(39, 174, 96);
            palette.CommandBarMenuPopupHovered.Text = Color.White;

            // 溢出按钮
            palette.OverflowButtonDefault.Glyph = Color.White;
            palette.OverflowButtonHovered.Background = Color.FromArgb(40, 180, 100);
            palette.OverflowButtonHovered.Glyph = Color.White;

            // 拖拽目标
            palette.DockTarget.Background = Color.FromArgb(39, 130, 96);
            palette.DockTarget.Border = Color.FromArgb(46, 204, 113);
            palette.DockTarget.GlyphArrow = Color.White;
        }
    }
}
