using System.ComponentModel;
using System.Collections.Generic;
using System;
using TDJS_Vision.Node._1_Acquisition.ImageSource;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision.Node._3_Detection.MatchTemplate
{
    /// <summary>
    /// 模板匹配节点运行结果。
    /// </summary>
    public class NodeResultMatchTemplate : INodeResult, IDynamicResultVariables, IDynamicResultVariableTypeProvider
    {
        public int RunTime { get; set; }

        [SubscriptionOutput]
        [DisplayName("输出图像")]
        public OutputImage OutputImage { get; set; } = new OutputImage();

        [SubscriptionOutput]
        [DisplayName("算法结果")]
        public AlgorithmResult Result { get; set; } = new AlgorithmResult();

        [SubscriptionOutput]
        [DisplayName("是否OK")]
        public bool IsOk { get; set; }

        [SubscriptionOutput]
        [DisplayName("匹配数量")]
        public int MatchCount { get; set; }

        /// <summary>
        /// 获取或设置本次模板匹配得到的全部目标位姿。
        /// </summary>
        [SubscriptionOutput(SubscriptionDataCategory.Pose, Multiplicity = SubscriptionValueMultiplicity.MultiTarget, Visibility = SubscriptionOutputVisibility.Advanced)]
        [DisplayName("匹配位姿列表")]
        public List<TemplateMatchPose> Poses { get; set; } = new List<TemplateMatchPose>();

        [SubscriptionOutput]
        [DisplayName("匹配点X")]
        public double? MatchX { get; set; }

        [SubscriptionOutput]
        [DisplayName("匹配点Y")]
        public double? MatchY { get; set; }

        /// <summary>
        /// 获取或设置首个匹配目标相对基准模板的角度偏差，单位为度。
        /// </summary>
        [SubscriptionOutput]
        [DisplayName("角度偏差")]
        public double? Angle { get; set; }

        [SubscriptionOutput]
        [DisplayName("得分")]
        public double? Score { get; set; }

        /// <summary>
        /// 获取排序后目标中心点动态变量名称，供四则运算等节点订阅。
        /// </summary>
        public IEnumerable<string> GetDynamicVariableNames()
        {
            return MatchTemplateTargetVariableNames.BuildTargetVariableNames(Poses == null ? 0 : Poses.Count);
        }

        /// <summary>
        /// 尝试读取排序后目标中心点动态变量。
        /// </summary>
        public bool TryGetDynamicVariable(string variableName, out object value)
        {
            value = null;
            if (Poses == null || string.IsNullOrWhiteSpace(variableName))
                return false;

            int targetIndex;
            MatchTemplateTargetCoordinate coordinate;
            if (!MatchTemplateTargetVariableNames.TryParse(variableName, out targetIndex, out coordinate))
                return false;

            TemplateMatchPose pose = null;
            for (int i = 0; i < Poses.Count; i++)
            {
                if (Poses[i] != null && Poses[i].TargetIndex == targetIndex)
                {
                    pose = Poses[i];
                    break;
                }
            }

            if (pose == null || !pose.IsValid)
                return false;

            value = coordinate == MatchTemplateTargetCoordinate.CenterX
                ? (object)pose.CenterX
                : pose.CenterY;
            return true;
        }

        /// <summary>
        /// 获取动态变量的真实类型。
        /// </summary>
        public bool TryGetDynamicResultVariableType(string variableName, out Type valueType)
        {
            valueType = null;
            int targetIndex;
            MatchTemplateTargetCoordinate coordinate;
            if (!MatchTemplateTargetVariableNames.TryParse(variableName, out targetIndex, out coordinate))
                return false;

            valueType = typeof(double);
            return true;
        }
    }

    /// <summary>
    /// 模板目标中心坐标类型。
    /// </summary>
    internal enum MatchTemplateTargetCoordinate
    {
        /// <summary>
        /// 目标中心 X 坐标。
        /// </summary>
        CenterX,

        /// <summary>
        /// 目标中心 Y 坐标。
        /// </summary>
        CenterY
    }

    /// <summary>
    /// 模板匹配多目标动态变量命名工具。
    /// </summary>
    internal static class MatchTemplateTargetVariableNames
    {
        /// <summary>
        /// 生成目标中心 X 变量名。
        /// </summary>
        public static string TargetCenterXName(int targetIndex)
        {
            return "目标" + targetIndex + "中心X";
        }

        /// <summary>
        /// 生成目标中心 Y 变量名。
        /// </summary>
        public static string TargetCenterYName(int targetIndex)
        {
            return "目标" + targetIndex + "中心Y";
        }

        /// <summary>
        /// 按目标数量生成全部中心点变量名。
        /// </summary>
        public static IEnumerable<string> BuildTargetVariableNames(int targetCount)
        {
            int normalizedCount = Math.Max(0, targetCount);
            for (int index = 1; index <= normalizedCount; index++)
            {
                yield return TargetCenterXName(index);
                yield return TargetCenterYName(index);
            }
        }

        /// <summary>
        /// 从动态变量名解析目标编号和坐标类型。
        /// </summary>
        public static bool TryParse(
            string variableName,
            out int targetIndex,
            out MatchTemplateTargetCoordinate coordinate)
        {
            targetIndex = 0;
            coordinate = MatchTemplateTargetCoordinate.CenterX;
            string text = DynamicResultVariableResolver.ExtractVariableName(variableName);
            if (string.IsNullOrWhiteSpace(text) || !text.StartsWith("目标", StringComparison.Ordinal))
                return false;

            string remainder = text.Substring("目标".Length);
            string suffix;
            if (remainder.EndsWith("中心X", StringComparison.Ordinal))
            {
                suffix = "中心X";
                coordinate = MatchTemplateTargetCoordinate.CenterX;
            }
            else if (remainder.EndsWith("中心Y", StringComparison.Ordinal))
            {
                suffix = "中心Y";
                coordinate = MatchTemplateTargetCoordinate.CenterY;
            }
            else
            {
                return false;
            }

            string numberText = remainder.Substring(0, remainder.Length - suffix.Length);
            return int.TryParse(numberText, out targetIndex) && targetIndex > 0;
        }
    }
}
