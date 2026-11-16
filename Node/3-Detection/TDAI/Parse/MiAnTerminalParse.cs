using System.Collections.Generic;
using System.Linq;
using TDJS_Vision.Node._3_Detection.TDAI.Yolo8;

namespace TDJS_Vision.Node._3_Detection.TDAI.Parse
{
    /// <summary>
    /// 米岸端子模型解析器。
    /// 当前检测项、类别映射和判定规则与RL_12类线芯模型完全一致，保留独立入口便于后续扩展米岸专用规则。
    /// </summary>
    public static class MiAnTerminalParse
    {
        /// <summary>
        /// 米岸端子中只保留最高得分检测结果的模型类别。
        /// 类别0为前铆脚露线芯、类别1为前铆脚、类别2为后铆脚露线芯、类别4为后铆脚。
        /// </summary>
        private static readonly HashSet<int> LargestRegionClassIds = new HashSet<int> { 0, 1, 2, 4 };

        /// <summary>
        /// 使用RL_12类线芯模型的既有规则解析米岸端子检测结果。
        /// </summary>
        /// <param name="results">模型输出的目标检测结果。</param>
        /// <param name="resultCount">模型输出的结果数量。</param>
        /// <param name="param">当前AI节点参数。</param>
        /// <param name="process">当前流程。</param>
        /// <param name="nodeResult">待写入的AI节点结果。</param>
        public static void Parse(
            List<DetResult> results,
            int resultCount,
            NodeParamTDAI param,
            Process process,
            ref NodeResultTDAI nodeResult)
        {
            List<DetResult> filteredResults = FilterHighestScores(results);

            // 筛选后复用成熟解析逻辑，未纳入筛选的类别仍完整保留全部模型结果。
            RL12Parse.Parse(filteredResults, filteredResults.Count, param, process, ref nodeResult);
        }

        /// <summary>
        /// 对四个端子主体类别分别保留模型置信度得分最高的结果，其它类别保持原顺序和数量。
        /// </summary>
        /// <param name="results">模型原始检测结果。</param>
        /// <returns>完成最高得分筛选的新结果集合，不修改模型原始集合。</returns>
        private static List<DetResult> FilterHighestScores(IEnumerable<DetResult> results)
        {
            List<DetResult> source = (results ?? Enumerable.Empty<DetResult>()).ToList();
            Dictionary<int, float> highestScoreByClass = new Dictionary<int, float>();

            // 单次遍历选出每个目标类别的最高置信度，降低多框场景的筛选开销。
            foreach (DetResult result in source)
            {
                if (!LargestRegionClassIds.Contains(result.ClassId))
                    continue;

                if (!highestScoreByClass.TryGetValue(result.ClassId, out float highestScore)
                    || result.Score > highestScore)
                {
                    highestScoreByClass[result.ClassId] = result.Score;
                }
            }

            var filtered = new List<DetResult>(source.Count);
            var emittedHighestScoreClasses = new HashSet<int>();
            foreach (DetResult result in source)
            {
                if (!LargestRegionClassIds.Contains(result.ClassId))
                {
                    filtered.Add(result);
                    continue;
                }

                // 相同最高分并列时保留模型输出顺序中的第一个，保证结果稳定且每类只有一个框。
                if (!emittedHighestScoreClasses.Contains(result.ClassId)
                    && highestScoreByClass.TryGetValue(result.ClassId, out float highestScore)
                    && result.Score == highestScore)
                {
                    filtered.Add(result);
                    emittedHighestScoreClasses.Add(result.ClassId);
                }
            }

            return filtered;
        }
    }
}
