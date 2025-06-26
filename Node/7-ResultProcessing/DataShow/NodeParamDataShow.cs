using System.Collections.Generic;
using Newtonsoft.Json;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._7_ResultProcessing.DataShow
{
    internal class NodeParamDataShow : INodeParam
    {
        public string Text1;
        public string Text2;
    }

    internal class DataShowData
    {
        public string TabPageName;
        public AiResult AiResultData;
        public DataShowData(string nodename, AiResult airesult)
        {
            TabPageName = nodename;
            AiResultData = airesult;
        }
    }
}
