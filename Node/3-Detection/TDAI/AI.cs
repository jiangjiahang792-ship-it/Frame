using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

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
        public static void ResultParse(List<DetResult> results, int resultCount, ref NodeResultTDAI nodeResult)
        {
            Dictionary<int, List<DetResult>> ResMap = new Dictionary<int, List<DetResult>>();
            // 遍历每一个检出结果，根据class_id将结果保存到对应列表中
            for (int i = 0; i < resultCount; ++i)
            {
                DetResult result = results[i];
                // 判断该id是否存在,不存在需要先创建列表才能添加，防止nul异常
                if(!ResMap.ContainsKey(result.ClassId))
                {
                    ResMap[result.ClassId] = new List<DetResult>();
                }
                ResMap[result.ClassId].Add(result);
            }
            // 使用LINQ按键class_id从小到大进行排序
            var sortedDictionary = new SortedDictionary<int, List<DetResult>>(ResMap);
            var rect = new List<Rectangle>();
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
                        rect = ParseTypeC(pair, minV, maxV, ref nodeResult);
                        break;
                    case 1:  // "solder", 和type_c一样只用根据数量判断OK/NG
                        ParseTypeC(pair, minV, maxV, ref nodeResult);
                        break;
                    case 2:  // "solder_ng", 需要判断数量和面积的OK/NG
                        ParseSolderNg(pair, minV, maxV, rect, ref nodeResult);
                        break;
                    case 3:  // "line"
                        ParseLineLen(pair, minV, maxV, ref nodeResult);
                        break;
                    case 4:  // "black", 和type_c一样只用根据数量判断OK/NG
                        ParseTypeC(pair, minV, maxV, ref nodeResult);
                        break;
                    case 5:  // "red", 和type_c一样只用根据数量判断OK/NG
                        ParseTypeC(pair, minV, maxV, ref nodeResult);
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
        private static List<Rectangle> ParseTypeC(KeyValuePair<int, List<DetResult>> pair, double minV, double maxV, ref NodeResultTDAI nodeResult)
        {
            List<Rectangle> rects = new List<Rectangle>();
            // type_c当前检出数量
            double curV = double.Parse(pair.Value.Count.ToString());

            // 构建检测项结果
            var deteItem = new DetectItemResult();
            deteItem.Name = nodeResult.AIInputInfo.DetectItems[pair.Key].Name;

            foreach (var result in pair.Value)
            {
                // 构建单个检测项结果
                var itemRes = new SingleDetectItemResult();
                Rectangle rect = new Rectangle(result.Box.Left, result.Box.Top, result.Box.Width, result.Box.Height);
                rects.Add(rect);
                itemRes.Value = "1"; // 检测项只考虑计数时此项默认"1"
                itemRes.IsOk = true; // 只要检测到即为OK
                itemRes.Rects.Add(rect);
                deteItem.Results.Add(itemRes);
            }
            deteItem.IsOk = deteItem.Results.TrueForAll(item => item.IsOk);
            deteItem.IsCountOk = IsValueOk(curV, minV, maxV); // 设置检测项内的结果数量是否OK
            nodeResult.ResultList.Add(deteItem);
            nodeResult.AIInputInfo.DetectItems[pair.Key].CurValue = curV.ToString();
            return rects;
        }
        /// <summary>
        /// 解析焊锡NG的数量以及面积是否OK/NG
        /// </summary>
        /// <param name="pair"></param>
        /// <param name="minV"></param>
        /// <param name="maxV"></param>
        /// <param name="IsOkDetectItem"></param>
        /// <param name="nodeResult"></param>
        private static void ParseSolderNg(KeyValuePair<int, List<DetResult>> pair, double minV, double maxV, List<Rectangle> rects, ref NodeResultTDAI nodeResult)
        {
            // 构建“焊锡不良”结果数据
            var solderNgRes = new DetectItemResult();
            solderNgRes.Name = nodeResult.AIInputInfo.DetectItems[pair.Key].Name;
            // 当前检出焊锡不良结果个数
            double curV = double.Parse(pair.Value.Count.ToString());
            // 没有检测到焊锡不良，直接返回结果
            if(curV == 0)
            {
                solderNgRes.IsOk = true;
                solderNgRes.IsCountOk = true; // 设置焊锡不良的结果数量是否OK
                nodeResult.ResultList.Add(solderNgRes);
                nodeResult.AIInputInfo.DetectItems[pair.Key].CurValue = curV.ToString();
                return;
            }
            // 检测到焊锡不良结果，需要逐一添加单个结果
            string valueStr = "";
            foreach (var result in pair.Value)
            {
                // 构建单个检测项结果
                var itemRes = new SingleDetectItemResult();
                Rectangle rect = new Rectangle(result.Box.Left, result.Box.Top, result.Box.Width, result.Box.Height);
                var area = result.Box.Width * result.Box.Height;
                itemRes.Value = area.ToString(); // 检测项的值是面积

                // 该不良焊点是否在TypeC区域内，不在的话直接NG, 在的话要根据面积判断OK/NG
                //var resultRect = rects.Find(rectTmp => rectTmp.Contains(rect));
                //itemRes.IsOk = (resultRect == null || resultRect == default(Rectangle)) ? false : IsValueOk(area, minV, maxV);

                //var resultRect = rects.Find(rectTmp => rectTmp.Contains(rect));
                itemRes.IsOk = IsValueOk(area, minV, maxV);

                itemRes.Rects.Add(rect);
                solderNgRes.Results.Add(itemRes);
                valueStr += (itemRes.Value + ",");
            }
            solderNgRes.IsOk = solderNgRes.Results.TrueForAll(item => item.IsOk);
            //Results中不良个数为0，该项的IsCountOk为true
            solderNgRes.IsCountOk = solderNgRes.Results.Count(item => !item.IsOk) == 0 ? true : false;
            nodeResult.ResultList.Add(solderNgRes);
            nodeResult.AIInputInfo.DetectItems[pair.Key].CurValue = valueStr.TrimEnd(',');;
        }
        /// <summary>
        /// 解析露线芯长度
        /// </summary>
        private static void ParseLineLen(KeyValuePair<int, List<DetResult>> pair, double minV, double maxV, ref NodeResultTDAI nodeResult)
        {
            // 构建检测项结果
            var deteItem = new DetectItemResult();
            deteItem.Name = nodeResult.AIInputInfo.DetectItems[pair.Key].Name;
            string valueStr = "";
            foreach (var result in pair.Value)
            {
                // 构建单个检测项结果
                var itemRes = new SingleDetectItemResult();
                Rectangle rect = new Rectangle(result.Box.Left, result.Box.Top, result.Box.Width, result.Box.Height);
                itemRes.Rects.Add(rect);
                itemRes.Value = result.Box.Height.ToString(); // 把矩形框的高当做露线芯长度
                itemRes.IsOk = IsValueOk(double.Parse(result.Box.Height.ToString()), minV, maxV);
                // 添加到检测项结果中
                deteItem.Results.Add(itemRes);
                valueStr += (itemRes.Value + ",");
            }
            deteItem.IsOk = deteItem.Results.TrueForAll(item => item.IsOk);
            deteItem.IsCountOk = true; // 检测项不需要检测结果个数的情况下，IsCountOk默认设置true表数量OK
            nodeResult.ResultList.Add(deteItem);
            nodeResult.AIInputInfo.DetectItems[pair.Key].CurValue = valueStr.TrimEnd(',');
        }
    }
}
