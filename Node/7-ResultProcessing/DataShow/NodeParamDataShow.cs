using Newtonsoft.Json;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.DataShow
{
    internal class NodeParamDataShow : INodeParam
    {
        public string Text1;
        public string Text2;
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
}
