"""汇总离线角度诊断，只有整条依赖链通过才记录有效角度，排除失败后的旧结果。"""
import collections
import csv
import json
from pathlib import Path

root = Path("D:/MyCode/PublicWook/TDJS-Vision/outputs/station2-angle-audit")
rows = [json.loads(line) for line in (root / "samples.jsonl").read_text(encoding="utf-8").splitlines()]
assert len(rows) == 305
chains = {
    "0°": ["382", "383", "384", "385", "387", "388", "386", "389", "390"],
    "90°": ["417", "418", "419", "420", "422", "423", "421", "424", "425"],
}


def valid(row, chain):
    """节点异常或任一上游判定失败时，不把下游残留值作为本张结果。"""
    return all(row[key].get("IsOk") is True for key in chain)


with (root / "逐图角度与失败原因.csv").open("w", encoding="utf-8-sig", newline="") as stream:
    writer = csv.writer(stream)
    writer.writerow(["测试序号", "图片", "测试类型", "视角", "有效角度（度）", "状态", "模板得分", "失败节点及原因"])
    for row in rows:
        for view, chain in chains.items():
            errors = []
            for key in chain:
                node = row[key]
                if node.get("IsOk") is not True:
                    reason = node.get("error") or "；".join(
                        item.get("ErrorMessage", "") for item in node.get("Items", []) if not item.get("IsOk")
                    )
                    errors.append(f"{key}：{reason or '未通过'}")
            good = valid(row, chain)
            writer.writerow([row["index"] + 1, Path(row["file"]).name,
                             "重复首图" if row["index"] >= 300 else "逐图",
                             view, row[chain[-1]].get("Angle") if good else "",
                             "有效" if good else "失败", row[chain[0]].get("Score"), "；".join(errors)])

stats = {}
for view, chain in chains.items():
    values = [row[chain[-1]]["Angle"] for row in rows[:300] if valid(row, chain)]
    assert len(values) == (297 if view == "0°" else 295)
    assert all(row[chain[0]]["MatchCount"] == 1 for row in rows[:300])
    stats[view] = {"通过": len(values), "失败": 300-len(values), "最小角度": min(values), "最大角度": max(values)}
for row in rows[300:]:
    assert row["390"]["Angle"] == 0.218 and row["425"]["Angle"] == 4.891

text = """工位2角度离线诊断

来源：D:/项目/AST-Fakra/软件数据/离线测试/ProjConfigs/Sol/9_21号方案_组合模块版_默认发OK.Sol
图片：D:/项目/AST-Fakra/采图/111，共300张；另将首图重复5次。
方法：使用当前Release实际程序集，复制并仅执行图像裁剪、旋转、模板匹配、位置修正、卡尺、组合拟合及夹角节点。没有执行PLC/相机/AI节点，没有修改源方案。

结果：
两个视角模板各300/300匹配到一个目标。
0°：297张有效，3张卡尺失败；有效角度0.019～1.305度。
90°：295张有效，5张卡尺失败；有效角度0.006～8.424度。
首图重复5次均为0.218度/4.891度。不同图片的角度范围不等同于同一工件的重复性误差。

明确的失败环节：
384：1张有效点不足0/15。
388：2张实际找齐15点，但与预设方向的偏差5.237度、5.169度超过5度，被质量检查拒绝。
420：5张有效点仅0/15或1/15，无法拟合。
历史日志17:52:41也记录了模板及位置修正成功后385、387、388卡尺NG，继而386、389组合拟合失败。
客户截图只显示390夹角缺失，没有原图与对应时间日志，不能断言截图那一次与本地失败完全相同。

角度偏大的证据：
90°视角419/420测的是下段两侧，422/423测的是上段两侧，再分别组合中线求夹角。
sample-135-edges.png对应1.5DS-QEHBE-F26102010484857-A.jpg，角度8.424度。
蓝色420边缘点落在右侧短小翻边/凸起上，红色419落在另一侧较长边缘；这两段不是同一段稳定的平行轮廓。
首图420仅7/15点，覆盖率0.431，仍返回OK；其方向80.364度，而419为84.760度。
419～423四个卡尺均关闭质量检查；420搜索高度14.61像素、边缘阈值2，窄区及弱边缘选择使短翻边对拟合方向影响明显。
平行中线模式按对应端点取中点，并非自动检查两侧平行：不同有效线长与倾斜翻边会改变中线角度。

建议调整顺序：
先明确客户的角度定义及真实基准段；将419/420 ROI放在同一高度范围、稳定的左右外轮廓，避开翻边和压接变形过渡。
检查模板定位后卡尺是否覆盖目标边，统一合理边缘极性和搜索方向；再逐图确认有效点和覆盖率。
开启适合现场的拟合质量检查，低质量结果应报告测量失败，不直接作为有效角度。不能只放大公差或取消388的方向检查来掩盖问题。
本次只诊断，没有更改现场参数。逐图CSV将上游失败帧角度留空，避免把下游旧值当成本张有效测量。
"""
(root / "诊断结论.txt").write_text(text, encoding="utf-8")
print(json.dumps(stats, ensure_ascii=False))
