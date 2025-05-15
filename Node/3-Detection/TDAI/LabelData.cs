using System.Collections.Generic;
using Sunny.UI;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>
    /// 模型标签数据类
    /// </summary>
    internal class LabelData
    {
        private int _labelNum;
        /// <summary>
        /// 配置文件
        /// </summary>
        private static IniFile _ini;

        /// <summary>
        /// 标签数量
        /// </summary>
        public int LabelNum
        {
            get => _labelNum;
            set => _labelNum = value;
        }
        /// <summary>
        /// 模型所有标签列表
        /// </summary>
        public List<LabelItem> Labels { get; set; }
        /// <summary>
        /// 加载标签文件构造
        /// </summary>
        /// <param name="labelFile"></param>
        public LabelData(string labelFile)
        {
            _ini = new IniFile(labelFile, System.Text.Encoding.UTF8);
            _labelNum = _ini.Sections.Length;
            Labels = new List<LabelItem>();
            for (int i = 0; i < _labelNum; i++)
            {
                LabelItem label = new LabelItem();
                label.Name = _ini.Sections[i];
                label.ClassId = _ini.ReadInt(_ini.Sections[i], "ClassId");
                label.ClassName = _ini.Read(_ini.Sections[i], "ClassName", "");
                label.Form = _ini.Read(_ini.Sections[i], "JudgmentForm", "value").ToLower() == "value" ? LabelForm.Value : LabelForm.Percentage;
                label.MinValue = _ini.ReadInt(_ini.Sections[i], "MinValue");
                label.MaxValue = _ini.ReadInt(_ini.Sections[i], "MaxValue");
                label.Enable = _ini.ReadBool(_ini.Sections[i], "Enable");
                label.LearnNum = _ini.ReadInt(_ini.Sections[i], "LearnNum");
                Labels.Add(label);
            }
        }
    }
    /// <summary>
    /// 标签信息类
    /// </summary>
    internal class LabelItem
    {
        /// <summary>
        /// 标签标注时名称
        /// </summary>
        public string Name { get; set; }
        /// <summary>
        /// 标签ID
        /// </summary>
        public int ClassId { get; set; }
        /// <summary>
        /// 标签中文名称
        /// </summary>
        public string ClassName { get; set; }
        /// <summary>
        /// 标签结果判断形式
        /// </summary>
        public LabelForm Form { get; set; }
        /// <summary>
        /// 标签结果最小值
        /// </summary>
        public int MinValue { get; set; }
        /// <summary>
        /// 标签结果最大值
        /// </summary>
        public int MaxValue { get; set; }
        /// <summary>
        /// 当前标签是否启用
        /// </summary>
        public bool Enable { get; set; } = true;
        /// <summary>
        /// 学习次数（仅在Form为Percentage时有意义）
        /// </summary>
        public int LearnNum { get; set; }
    }

    /// <summary>
    /// 标签判断形式：Value是数值形式，Percentage是百分比形式，
    /// 用于判断标签结果OK/NG时的判断形式，数值形式直接比较，
    /// 百分比形式要先获取多个标签结果计算出平均值，然后再进行实测值偏离平均值的%比较
    /// </summary>
    internal enum LabelForm
    {
        Value,
        Percentage
    }
}
