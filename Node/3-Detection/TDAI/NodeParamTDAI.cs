using System.Collections.Generic;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    internal class NodeParamTDAI : INodeParam
    {
        /// <summary>
        /// 订阅得节点文本（反序列化用）
        /// </summary>
        public string Text1 { get; set; }
        /// <summary>
        /// 订阅得节点属性文本（反序列化用）
        /// </summary>
        public string Text2 { get; set; }
        /// <summary>
        /// 配置文件路径
        /// </summary>
        public string ConfigPath { get; set; }
        /// <summary>
        /// AI模型配置列表
        /// </summary>
        public List<AIInputInfo> AIInputInfoList { get; set; }
        /// <summary>
        /// 选择的模型索引（AI模型配置列表会有多个模型信息，需要指定使用哪个模型）
        /// </summary>
        public int SelectedModelIndex { get; set; }
        /// <summary>
        /// 是否一键学习
        /// </summary>
        public bool IsAutoStudy { get; set; }
        /// <summary>
        /// 给指定的检测项设置当前值
        /// </summary>
        /// <param name="value"></param>
        public void SetCurValue(string name, string value)
        {
            foreach (var info in AIInputInfoList)
            {
                for (int i = 0; i < info.DetectItems.Count; i++)
                {
                    if (info.DetectItems[i].Name == name)
                    {
                        var item = info.DetectItems[i];
                        item.CurValue = value;
                        info.DetectItems[i] = item;
                        break;
                    }
                }
            }
        }
    }
}
