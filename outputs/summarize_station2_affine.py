"""用已知图像变换核验模板位姿和卡尺端点，区分匹配误差与坐标变换错误。"""
import csv
import json
import math
import sys
from pathlib import Path

root = Path("outputs/station2-angle-audit")
input_name = sys.argv[1] if len(sys.argv) > 1 else "affine"
rows = [json.loads(line) for line in (root / (input_name + ".jsonl")).read_text(encoding="utf-8").splitlines()]
plan = json.loads(Path("D:/项目/AST-Fakra/软件数据/离线测试/ProjConfigs/Sol/9_21号方案_组合模块版_默认发OK.Sol").read_text(encoding="utf-8-sig"))
flow = next(x for x in plan["ProcessInfos"] if x["ProcessName"] == "工位2主流程")
module = next(x for x in flow["NodeInfos"] if x["NodeType"] == "CompositeModule" and x["Active"])
nodes = {str(x["ID"]): next(iter(x["NodeParam"].values())) for x in next(iter(module["NodeParam"].values()))["NodeInfos"] if x.get("NodeParam")}


def transform(point, correction):
    """复算源码中的单位尺度位置修正；本批实际尺度均为1。"""
    assert correction["BaseScaleX"] == correction["BaseScaleY"] == correction["CurrentScaleX"] == correction["CurrentScaleY"] == 1
    x, y = point[0] - correction["BaseX"], point[1] - correction["BaseY"]
    angle = math.radians(correction["DeltaAngle"])
    return (correction["CurrentX"] + x * math.cos(angle) - y * math.sin(angle),
            correction["CurrentY"] + x * math.sin(angle) + y * math.cos(angle))


def warp(point, matrix):
    """独立使用施加给图像的OpenCV矩阵计算理想端点。"""
    x, y = point
    return matrix[0] * x + matrix[1] * y + matrix[2], matrix[3] * x + matrix[4] * y + matrix[5]


records = []
for index, row in enumerate(rows):
    baseline = rows[index // 7 * 7]
    for match, calipers, matrix_key in [("382", ["384", "385", "387", "388"], "matrix299"),
                                       ("417", ["419", "420", "422", "423"], "matrix302")]:
        pose, old_pose = row[match]["Poses"][0], baseline[match]["Poses"][0]
        center_error = math.dist(warp((old_pose["CenterX"], old_pose["CenterY"]), row[matrix_key]),
                                 (pose["CenterX"], pose["CenterY"]))
        angle_error = (pose["Angle"] - old_pose["Angle"] - row["knownAngle"] + 180) % 360 - 180
        for node in calipers:
            correction = row[node]["Items"][0]["Correction"]
            old_correction = baseline[node]["Items"][0]["Correction"]
            errors = []
            for end in ["Start", "End"]:
                point = (nodes[node][end + "X"], nodes[node][end + "Y"])
                errors.append(math.dist(transform(point, correction),
                                        warp(transform(point, old_correction), row[matrix_key])))
            records.append([index, Path(row["file"]).name, row["knownAngle"], row["dx"], row["dy"],
                            match, row[match]["TemplateNames"][0], center_error, angle_error,
                            node, max(errors), row[node].get("IsOk"), row[node].get("EdgePointCount")])
with (root / (input_name + "-端点核验.csv")).open("w", encoding="utf-8-sig", newline="") as stream:
    writer = csv.writer(stream)
    writer.writerow(["序号", "图片", "施加角度", "施加X平移", "施加Y平移", "匹配节点", "模板",
                     "中心偏离理想位置像素", "角度偏离理想值度", "卡尺节点", "端点最大偏离像素", "卡尺成功", "边缘点数"])
    writer.writerows(records)
for record in records:
    if record[0] in [2, 8, 9, 16] and record[9] in ["384", "420"]:
        print(record)
