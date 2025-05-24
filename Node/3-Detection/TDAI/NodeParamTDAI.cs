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
        /// AI输入信息
        /// </summary>
        public AIInputInfo AIInputInfo { get; set; }
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
            for (int i = 0; i < AIInputInfo.DetectItems.Count; i++)
            {
                if (AIInputInfo.DetectItems[i].Name == name)
                { 
                    var item = AIInputInfo.DetectItems[i];
                    item.CurValue = value;
                    AIInputInfo.DetectItems[i] = item;
                    break;
                }
            }
        }
    }
}
