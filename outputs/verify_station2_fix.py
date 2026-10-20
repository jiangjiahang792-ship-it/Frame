"""验证最终隔离构建的已知位移回归并汇总现场回放；不修改方案或公差。"""
import collections
import csv
import json
import statistics
from pathlib import Path

root = Path("outputs/station2-angle-audit")
affine = list(csv.DictReader((root / "affine-final-端点核验.csv").open(encoding="utf-8-sig")))
assert len(affine) == 168, "已知变换的21组×8个卡尺结果不完整"
translations = [float(row["端点最大偏离像素"]) for row in affine if float(row["施加角度"]) == 0]
rotations = [float(row["端点最大偏离像素"]) for row in affine if float(row["施加角度"]) != 0]
assert max(translations) < 0.01, "整数平移后仍发生卡尺跳动"
assert max(rotations) < 5, "旋转重采样后的卡尺端点偏离超过5像素"
rows = [json.loads(line) for line in (root / "samples-final.jsonl").read_text(encoding="utf-8").splitlines()]
assert len(rows) == 305, "300张现场图与5次首图重复结果不完整"
chains = {"0度": ["382", "383", "384", "385", "387", "388", "386", "389", "390"],
          "90度": ["417", "418", "419", "420", "422", "423", "421", "424", "425"]}
summary = {"整数平移端点最大误差像素": max(translations),
           "旋转端点最大误差像素": max(rotations)}
for name, chain in chains.items():
    good = [row[chain[-1]]["Angle"] for row in rows[:300] if all(row[node].get("IsOk") is True for node in chain)]
    failures = collections.Counter(node for row in rows[:300] for node in chain if row[node].get("IsOk") is not True)
    repeats = [row[chain[-1]]["Angle"] for row in rows[300:]]
    assert len(set(repeats)) == 1, "相同图像重复运行角度不一致"
    assert all(row[chain[1]].get("IsOk") for row in rows), "现场方案出现未兼容基准"
    summary[name] = {"有效张数": len(good), "有效角度范围": [min(good), max(good)],
                     "失败节点计数": dict(failures), "首图重复角度": repeats[0],
                     "匹配平均耗时毫秒": statistics.mean(row[chain[0]]["MatchingTime"] for row in rows[:300])}
assert summary["90度"]["有效张数"] == 300, "90度现场样本仍有测量失败"
(root / "修复验证汇总.json").write_text(json.dumps(summary, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(summary, ensure_ascii=False, indent=2))
