using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Reflection;
using System.Threading;
using TDJS_Vision.Node._1_Acquisition.ImageSource;

namespace TDJS_Vision.Node._3_Detection.ContourMatch
{
    /// <summary>节点即时参数快照、上游订阅及多模板生产会话。</summary>
    public partial class NodeParamFormContourMatch
    {
        /// <summary>所属流程节点。</summary>
        private NodeBase _node;
        /// <summary>当前不可变生产参数快照，与工程是否落盘无关。</summary>
        private NodeParamContourMatch _savedParameters;
        /// <summary>生产缓存，与预览和编辑完全隔离。</summary>
        private readonly ContourMatchSession _runtimeSession = new ContourMatchSession();
        /// <summary>参数通过深复制对外交换，防止外部引用修改在途运行。</summary>
        public INodeParam Params
        {
            get { return SavedParameters?.Copy(); }
            set { if (value != null && !(value is NodeParamContourMatch)) throw new ArgumentException("参数类型不正确。"); Volatile.Write(ref _savedParameters, (value as NodeParamContourMatch)?.Copy()); }
        }
        /// <summary>生产路径只读取引用，不每帧复制模板图像。</summary>
        internal NodeParamContourMatch SavedParameters { get { return Volatile.Read(ref _savedParameters); } }
        /// <summary>注册输入图像类型订阅。</summary>
        public void SetNodeBelong(NodeBase node)
        {
            _node = node; imageSubscription.SetExpectedValueType<OutputImage>(); imageSubscription.Init(node);
        }
        /// <summary>恢复三页控件，并兼容旧单模板方案。</summary>
        public void SetParam2Form()
        {
            _parametersReady = false;
            _draft = SavedParameters?.Copy() ?? new NodeParamContourMatch { Templates = new List<ContourTemplateDefinition>() };
            _draft.Templates = _draft.GetTemplates();
            _populating = true;
            try
            {
                imageSubscription.SetText(_draft.Text1 ?? "", _draft.Text2 ?? "");
                allSearchCheckBox.Checked = _draft.AllSearch;
                var find = _draft.FindOptions ?? new FindOptions();
                findAngleStartNumeric.Value = (decimal)find.AngleStartDegrees; findAngleEndNumeric.Value = (decimal)find.AngleEndDegrees;
                minimumScoreNumeric.Value = (decimal)find.MinimumScore; maximumMatchesNumeric.Value = find.MaximumMatches;
                maximumOverlapNumeric.Value = (decimal)find.MaximumOverlap; findLevelsNumeric.Value = find.PyramidLevels;
                modeComboBox.SelectedIndex = (int)find.Mode; subPixelCheckBox.Checked = find.SubPixel;
            }
            finally { _populating = false; }
            RefreshTemplateList(); ShowSearchRegion();
            _parametersReady = true;
            // 只在补齐空订阅时发布新快照，恢复已有方案时保留原模型和数值精度。
            var saved = SavedParameters;
            if (saved == null || saved.Text1 != imageSubscription.GetText1() || saved.Text2 != imageSubscription.GetText2())
                ApplyParameters();
        }
        /// <summary>读取并验证当前页面参数，仅在执行时检查配置是否完整。</summary>
        internal NodeParamContourMatch ReadDraft()
        {
            if (imageCanvas.EditorMode != CanvasEditorMode.查看) throw new InvalidOperationException("请先确认或取消搜索区域绘制。");
            templatesGrid.EndEdit();
            var value = CaptureParameters();
            NativeShapeMatcher.ValidateFind(value.FindOptions);
            if (!value.AllSearch && (value.SearchRegion.Width < 8 || value.SearchRegion.Height < 8)) throw new InvalidOperationException("请先绘制并确认搜索区域。");
            return value;
        }
        /// <summary>读取当前配置，不把填写过程中的暂时无效值误当成旧的有效参数。</summary>
        private NodeParamContourMatch CaptureParameters()
        {
            var value = _draft.Copy(); value.Text1 = imageSubscription.GetText1(); value.Text2 = imageSubscription.GetText2();
            int separator = (value.Text1 ?? "").IndexOf('.'); int sourceId;
            value.SourceNodeId = separator > 0 && int.TryParse(value.Text1.Substring(0, separator), out sourceId) ? sourceId : -1;
            value.AllSearch = allSearchCheckBox.Checked;
            value.FindOptions = new FindOptions { AngleStartDegrees = (double)findAngleStartNumeric.Value, AngleEndDegrees = (double)findAngleEndNumeric.Value,
                MinimumScore = (double)minimumScoreNumeric.Value, MaximumMatches = (int)maximumMatchesNumeric.Value,
                MaximumOverlap = (double)maximumOverlapNumeric.Value, PyramidLevels = (int)findLevelsNumeric.Value,
                Mode = (ShapeMatchMode)modeComboBox.SelectedIndex, SubPixel = subPixelCheckBox.Checked };
            // 新版只保存模板列表；清理旧单模板字段以免重复源图或删除后恢复旧模板。
            value.ModelImageBytes = null; value.ModelOptions = null; value.EraseMasks.Clear(); value.ModelRoi = System.Drawing.Rectangle.Empty;
            value.ModelRegions = null;
            return value;
        }
        /// <summary>控件已完成恢复，避免初始化过程覆盖节点参数。</summary>
        private bool _parametersReady;
        /// <summary>读取数值控件时可能触发值变化，防止重入发布。</summary>
        private bool _applyingParameters;
        /// <summary>控件变更立即发布线程安全快照，无需关闭窗口或保存工程。</summary>
        private void ParameterValue_Changed(object sender, EventArgs e) { ApplyParameters(); }
        /// <summary>复制一次后原子替换快照；在途运行继续使用旧快照，下次运行读取新值。</summary>
        private void ApplyParameters()
        {
            if (!_parametersReady || _populating || _applyingParameters || IsDisposed) return;
            _applyingParameters = true;
            try
            {
                var previous = SavedParameters;
                var current = CaptureParameters();
                Volatile.Write(ref _savedParameters, current);
                if (previous?.FindOptions?.MaximumMatches != current.FindOptions.MaximumMatches)
                    _node?.NotifyOutputDefinitionChanged();
            }
            finally { _applyingParameters = false; }
        }
        /// <summary>按当前上游引用读取值，禁止使用本轮未执行的历史结果。</summary>
        private object ResolveValue(string text1, string text2, int sourceId = -1)
        {
            if (_node?.Process == null) throw new InvalidOperationException("节点尚未初始化。");
            if (sourceId < 0)
            {
                int separator = (text1 ?? "").IndexOf('.');
                if (separator < 1 || !int.TryParse(text1.Substring(0, separator), out sourceId)) throw new InvalidOperationException("请选择有效的上游订阅。");
            }
            var source = _node.Process.GetUpstreamNodes(_node).Find(item => item.ID == sourceId);
            if (source == null) throw new InvalidOperationException("订阅源不在当前节点上游，请检查连线。");
            if (_node.Process.IsRuning && !source.HasSuccessfulResultForRun(_node.Process.CurrentRunId)) throw new InvalidOperationException("上游本轮未成功运行，不能使用历史结果。");
            var result = source.Result;
            var property = result?.GetType().GetProperties().FirstOrDefault(item => item.Name == text2 || ((DisplayNameAttribute)Attribute.GetCustomAttribute(item, typeof(DisplayNameAttribute)))?.DisplayName == text2);
            return property?.GetValue(result);
        }
        /// <summary>生产输入图像来自当前快照，不读取编辑中的控件。</summary>
        internal OutputImage ResolveInput(NodeParamContourMatch parameters)
        {
            return ResolveValue(parameters.Text1, parameters.Text2, parameters.SourceNodeId) as OutputImage ?? throw new InvalidOperationException("订阅的图像输出不存在或为空。");
        }
        /// <summary>使用生产模型缓存执行当前参数快照。</summary>
        internal ContourMatchExecution Execute(OpenCvSharp.Mat source, NodeParamContourMatch parameters, CancellationToken token)
        {
            return _runtimeSession.Execute(source, parameters, token);
        }
    }
}
