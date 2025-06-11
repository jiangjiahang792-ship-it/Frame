using System.Collections.Generic;
using System.Drawing;
using System.Web.UI;
using OpenCvSharp.Aruco;
using Sunny.UI;
using static TDJS_Vision.Node._3_Detection.TDAI.Yolo8.YoloV8;

namespace TDJS_Vision.Node._3_Detection.TDAI
{
    /// <summary>
    /// AI结果解析类
    /// 作用：将AI检测出来的标签对应结果，解析成客户需要的检测项结果
    /// 比如说，客户需要检测出一款线芯的长度，模型训练有一个line_core标签，
    /// 通过调用模型推理得到这个标签结果，我们要把矩形框的长当做客户
    /// 提的检测项“线芯的长度”的实测值，检测项的OK范围会通过一个配置文件提前定义好
    /// 这个配置文件就是“AI检测节点”参数设置界面要选择的输入文件
    /// </summary>
    internal class AI
    {
        /// <summary>
        /// 执行AI结果解析的逻辑，不同项目根据实际检测要求再次进行不同的解析逻辑
        /// </summary>
        /// <param name="results">AI检测原始结果数据</param>
        /// <param name="resultCount">AI检测出来的结果个数</param>
        /// <param name="nodeResult">解析之后的结果数据</param>
        public static void ResultParse(DetectionResult[] results, int resultCount, ref NodeResultTDAI nodeResult)
        {
            Dictionary<int, List<DetectionResult>> ResMap = new Dictionary<int, List<DetectionResult>>();
            // 遍历每一个检出结果，根据class_id将结果保存到对应列表中
            for (int i = 0; i < resultCount; ++i)
            {
                DetectionResult result = results[i];
                // 判断该id是否存在,不存在需要先创建列表才能添加，防止nul异常
                if(!ResMap.ContainsKey(result.class_id))
                {
                    ResMap[result.class_id] = new List<DetectionResult>();
                }
                ResMap[result.class_id].Add(result);
            }
            // 使用LINQ按键进行排序
            var sortedDictionary = new SortedDictionary<int, List<DetectionResult>>(ResMap);
            foreach (var pair in sortedDictionary)
            {
                // 如果当前检测项不测,直接跳过
                if (!nodeResult.AIInputInfo.DetectItems[pair.Key].Enable)
                    continue;

                double minV = double.Parse(nodeResult.AIInputInfo.DetectItems[pair.Key].MinValue);
                double maxV = double.Parse(nodeResult.AIInputInfo.DetectItems[pair.Key].MaxValue);

                switch (pair.Key)
                {
                    case 0:  // "type-c"
                        ParseTypeC(pair, minV, maxV, true, ref nodeResult);
                        break;
                    case 1:  // "solder", 和type_c一样只用根据数量判断OK/NG
                        ParseTypeC(pair, minV, maxV, true, ref nodeResult);
                        break;
                    case 2:  // "solder_ng", 和type_c一样只用根据数量判断OK/NG, 但是该项子在范围内即为NG
                        ParseTypeC(pair, minV, maxV, false, ref nodeResult);
                        break;
                    case 3:  // "line"
                        ParseLineLen(pair, minV, maxV, ref nodeResult);
                        break;
                    case 4:  // "black", 和type_c一样只用根据数量判断OK/NG
                        ParseTypeC(pair, minV, maxV, true, ref nodeResult);
                        break;
                    case 5:  // "red", 和type_c一样只用根据数量判断OK/NG
                        ParseTypeC(pair, minV, maxV, true, ref nodeResult);
                        break;
                    default:
                        break;
                }
            }
        }
        /// <summary>
        /// 判断当前值是否OK
        /// </summary>
        /// <param name="curV"></param>
        /// <param name="minV"></param>
        /// <param name="maxV"></param>
        /// <returns></returns>
        private static bool IsValueOk(double curV, double minV, double maxV)
        {
            if (curV >= minV && curV <= maxV)
                return true;
            return false;
        }
        /// <summary>
        /// 解析TypeC头结果
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="minV"></param>
        /// <param name="maxV"></param>
        /// <param name="IsOkDetectItem">标记该项是不是OK类型的，是IsOk默认赋值true，否IsOk默认赋值fals</param>
        /// <param name="nodeResult"></param>
        private static void ParseTypeC(KeyValuePair<int, List<DetectionResult>> pair, double minV, double maxV, bool IsOkDetectItem, ref NodeResultTDAI nodeResult)
        {
            // type_c当前检出数量
            double curV = double.Parse(pair.Value.Count.ToString());

            // 构建检测项结果
            var deteItem = new DetectItemResult();
            deteItem.Name = nodeResult.AIInputInfo.DetectItems[pair.Key].Name;

            foreach (var result in pair.Value)
            {
                // 构建单个检测项结果
                var itemRes = new SingleDetectItemResult();
                Rectangle rect = new Rectangle(result.left, result.top, result.width, result.height);
                itemRes.Value = "1"; // 检测项只考虑计数时此项默认"1"
                itemRes.IsOk = IsOkDetectItem;
                itemRes.Rects.Add(rect);
                deteItem.Results.Add(itemRes);
            }
            deteItem.IsCountOk = IsValueOk(curV, minV, maxV); // 设置检测项内的结果数量是否OK
            nodeResult.ResultList.Add(deteItem);
            nodeResult.AIInputInfo.DetectItems[pair.Key].CurValue = curV.ToString();
        }        
        /// <summary>
        /// 解析露线芯长度
        /// </summary>
        private static void ParseLineLen(KeyValuePair<int, List<DetectionResult>> pair, double minV, double maxV, ref NodeResultTDAI nodeResult)
        {
            // 构建检测项结果
            var deteItem = new DetectItemResult();
            string valueStr = "";
            foreach (var result in pair.Value)
            {
                // 构建单个检测项结果
                var itemRes = new SingleDetectItemResult();
                Rectangle rect = new Rectangle(result.left, result.top, result.width, result.height);
                itemRes.Rects.Add(rect);
                itemRes.Value = result.height.ToString(); // 把矩形框的高当做露线芯长度
                itemRes.IsOk = IsValueOk(double.Parse(result.height.ToString()), minV, maxV);
                // 添加到检测项结果中
                deteItem.Results.Add(itemRes);
                valueStr += (itemRes.Value + ",");
            }
            deteItem.IsCountOk = true; // 检测项不需要检测结果个数的情况下，IsCountOk默认设置true表数量OK
            deteItem.Name = nodeResult.AIInputInfo.DetectItems[pair.Key].Name;
            nodeResult.ResultList.Add(deteItem);

            nodeResult.AIInputInfo.DetectItems[pair.Key].CurValue = valueStr.TrimEnd(',');
        }
    }
}
