using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.ImageDraw
{
    internal class NodeParamImageDraw : INodeParam
    {
        [JsonIgnore]
        public ResultViewData AiResultData;
        public string Text1;
        public string Text2;
        public string Text3;
        public string Text4;
        public int FontSize;
        public int LineWidth;
        [JsonConverter(typeof(StringEnumConverter))]
        public TextLoc TextLoc;
    }

    internal class DatashowData
    {
        public string TabPageName;
        public ResultViewData AiResultData;
        public DatashowData(string nodename, ResultViewData airesult)
        {
            TabPageName = nodename;
            AiResultData = airesult;
        }
    }
    /// <summary>
    /// 文字绘制位置
    /// </summary>
    internal enum TextLoc
    {
        LeftUp,//左上
        RightDown,//右下
        FollowBox//跟随结果矩形框
    }
}
