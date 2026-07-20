using System.Collections.Generic;
using TDJS_Vision.Node._3_Detection.TDAI;

namespace TDJS_Vision
{
    public static class DetectItemLanguage
    {
        private static readonly Dictionary<string, string> LegacyNameToKey = new Dictionary<string, string>
        {
            { "胶皮长度", "DetectItem.RubberLength" },
            { "露线芯长度", "DetectItem.ExposedCoreLength" },
            { "压接长度", "DetectItem.CrimpLength" },
            { "飞丝", "DetectItem.FlyingWire" },
            { "注塑状态脱落", "DetectItem.InjectionDetached" },
            { "缺胶", "DetectItem.GlueMissing" },
            { "脏污", "DetectItem.Dirty" },
            { "咬胶长度", "DetectItem.BiteGlueLength" },
            { "Type-C金属数量", "DetectItem.TypeCMetalCount" },
            { "Type-C金属上边界", "DetectItem.TypeCMetalTop" },
            { "Type-C金属宽度", "DetectItem.TypeCMetalWidth" },
            { "Type-C金属高度", "DetectItem.TypeCMetalHeight" },
            { "Type-C焊锡数量", "DetectItem.TypeCSolderCount" },
            { "Type-C焊锡上边界", "DetectItem.TypeCSolderTop" },
            { "Type-A金属数量", "DetectItem.TypeAMetalCount" },
            { "Type-A金属上边界", "DetectItem.TypeAMetalTop" },
            { "Type-A金属宽度", "DetectItem.TypeAMetalWidth" },
            { "Type-A金属高度", "DetectItem.TypeAMetalHeight" },
            { "Type-A焊锡数量", "DetectItem.TypeASolderCount" },
            { "Type-A焊锡上边界", "DetectItem.TypeASolderTop" },
            { "线在槽", "DetectItem.WireInSlot" },
            { "线不在槽", "DetectItem.WireOutOfSlot" },
            { "端子数量", "DetectItem.TerminalCount" },
            { "线芯聚拢度", "DetectItem.CoreGathering" },
            { "A_C插头数量", "DetectItem.ACPlugCount" },
            { "A_C插尾数量", "DetectItem.ACTailCount" },
            { "A_C插尾到位", "DetectItem.ACTailInPlace" },
            { "A_C插头到位", "DetectItem.ACPlugInPlace" },
            { "定位", "DetectItem.Positioning" },
            { "前铆脚露线芯长度", "DetectItem.FrontRivetCoreLength" },
            { "前铆脚露线芯高度", "DetectItem.FrontRivetCoreHeight" },
            { "前铆脚面积", "DetectItem.FrontRivetArea" },
            { "后铆脚露线芯长度", "DetectItem.BackRivetCoreLength" },
            { "后铆脚面积", "DetectItem.BackRivetArea" },
            { "正插防水栓面积", "DetectItem.ForwardWaterproofPlugArea" },
            { "反插防水栓", "DetectItem.ReverseWaterproofPlug" },
            { "防水栓破损", "DetectItem.WaterproofPlugDamaged" },
            { "线芯长度", "DetectItem.CoreLength" },
            { "线芯高度", "DetectItem.CoreHeight" },
            { "正插防水栓长度", "DetectItem.ForwardWaterproofPlugLength" },
            { "正插防水栓高度", "DetectItem.ForwardWaterproofPlugHeight" },
            { "线弯曲", "DetectItem.WireBent" },
            { "少焊", "DetectItem.InsufficientSolder" },
            { "假焊", "DetectItem.FalseSolder" },
            { "线破损", "DetectItem.WireDamaged" },
            { "焊盘破损", "DetectItem.PadDamaged" },
            { "咬胶占比", "DetectItem.BiteGlueRatio" },
            { "间距", "DetectItem.Spacing" },
            { "注塑个数", "DetectItem.InjectionCount" },
            { "注塑面积", "DetectItem.InjectionArea" },
            { "注塑状态存在", "DetectItem.InjectionExists" },
            { "水口", "DetectItem.Gate" },
            { "端子头", "DetectItem.TerminalHead" },
            { "红线数量", "DetectItem.RedWireCount" },
            { "黑线数量", "DetectItem.BlackWireCount" },
            { "红黑线序", "DetectItem.RedBlackWireOrder" },
            { "线芯数量", "DetectItem.CoreCount" },
            { "焊锡面积", "DetectItem.SolderArea" }
        };

        public static string NormalizeName(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
                return name;

            return LegacyNameToKey.TryGetValue(name, out var key) ? key : name;
        }

        public static string GetDisplayName(string name)
        {
            return LanguageManager.T(NormalizeName(name));
        }

        public static void NormalizeItems(IEnumerable<DetectItemInfo> items)
        {
            if (items == null)
                return;

            foreach (var item in items)
            {
                if (item != null)
                    item.Name = NormalizeName(item.Name);
            }
        }
    }
}
