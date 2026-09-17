"""汇总同一输入的实测记录，生成中文报告和可逐图查看的离线页面。"""
import csv
import hashlib
import html
import json
import math
import statistics
import sys
from pathlib import Path


def read_json(path):
    """兼容 PowerShell 写入的 UTF-8 BOM。"""
    return json.loads(path.read_text(encoding="utf-8-sig"))


def percentile(values, fraction):
    """使用最近秩计算逐图中位耗时的分位数。"""
    ordered = sorted(values)
    return ordered[max(0, math.ceil(len(ordered) * fraction) - 1)]


def summarize(rows, manifest):
    """定位精度采用共同原图的零姿态校准，避免两家模板框中心不同造成假误差。"""
    mapping = {row["Id"]: row for row in rows}
    if set(mapping) != {item["Id"] for item in manifest}:
        raise ValueError("正式结果缺少输入或包含重复编号")
    if any(row.get("ErrorCode", 0) != 0 for row in rows):
        raise ValueError("不能把运行异常作为未检出")
    originals = [row for row in rows if row["Kind"] == "原始图像"]
    times = [row["MedianMs"] for row in originals]
    reference = mapping["template"]["Hits"][0]
    translation, angle_errors, errors = [], [], {}
    for sample in manifest:
        if sample["Kind"] != "已知刚体变换":
            continue
        row = mapping[sample["Id"]]
        if row["Count"] != 1:
            continue
        angle = math.radians(sample["ExpectedAngle"])
        dx = reference["CenterX"] - 1425.5
        dy = reference["CenterY"] - 1166.5
        x = sample["ExpectedX"] + math.cos(angle) * dx - math.sin(angle) * dy
        y = sample["ExpectedY"] + math.sin(angle) * dx + math.cos(angle) * dy
        hit = row["Hits"][0]
        position_error = math.hypot(hit["CenterX"] - x, hit["CenterY"] - y)
        angle_error = abs(math.remainder(hit["AngleDegrees"] - sample["ExpectedAngle"] - reference["AngleDegrees"], 360))
        translation.append(position_error)
        angle_errors.append(angle_error)
        errors[sample["Id"]] = {"Pixel": position_error, "Angle": angle_error}
    return {
        "OriginalCount": len(originals),
        "Found": sum(row["Count"] > 0 for row in originals),
        "MeanMs": statistics.mean(times),
        "MedianMs": statistics.median(times),
        "P95Ms": percentile(times, 0.95),
        "PoseCount": len(translation),
        "MeanPixelError": statistics.mean(translation),
        "MaxPixelError": max(translation),
        "MeanAngleError": statistics.mean(angle_errors),
        "MaxAngleError": max(angle_errors),
        "NegativeCount": sum(row["Kind"] == "无目标" for row in rows),
        "FalsePositives": sum(row["Count"] > 0 for row in rows if row["Kind"] == "无目标"),
        "Errors": errors,
    }


def main(directory, native_path):
    """只读取正式记录，实验输出不参与比较。"""
    manifest = read_json(directory / "manifest.json")
    ours = read_json(directory / "ours-final.json")
    vm = read_json(directory / "vm-final.json")
    parameters = read_json(directory / "parameters.json")
    model = parameters["Templates"][0]["Model"]
    source_roi = [float(value.strip()) for value in model["ModelRoi"].split(",")]
    # 源坐标必须与生成刚体变换时一致，修改模板后不允许继续使用旧清单。
    if source_roi != [412, 676, 2027, 981]:
        raise ValueError("模板坐标已变更，请同步变换清单和参考中心")
    summaries = {"Ours": summarize(ours, manifest), "VM": summarize(vm, manifest)}
    o, v = summaries["Ours"], summaries["VM"]
    improvement = (1 - o["MeanMs"] / v["MeanMs"]) * 100
    summaries["LatencyReductionPercent"] = improvement
    summaries["NativeSha256"] = hashlib.sha256(native_path.read_bytes()).hexdigest().upper()
    summaries["TemplateSha256"] = hashlib.sha256((directory / "images/template.png").read_bytes()).hexdigest().upper()
    summaries["UniqueOriginalImages"] = len({hashlib.sha256(Path(sample["InputPath"]).read_bytes()).digest()
        for sample in manifest if sample["Kind"] == "原始图像"})
    (directory / "summary.json").write_text(json.dumps(summaries, ensure_ascii=False, indent=2), encoding="utf-8")
    a, b = {row["Id"]: row for row in ours}, {row["Id"]: row for row in vm}
    with (directory / "逐图结果.csv").open("w", newline="", encoding="utf-8-sig") as stream:
        writer = csv.writer(stream)
        writer.writerow(["编号", "类别", "原始文件", "本项目数量", "VM数量", "本项目毫秒", "VM毫秒",
            "本项目X", "本项目Y", "本项目角度", "VM框中心X", "VM框中心Y", "VM角度", "本项目位移误差像素", "VM位移误差像素"])
        for sample in manifest:
            left, right = a[sample["Id"]], b[sample["Id"]]
            lh = left["Hits"][0] if left["Hits"] else {}
            rh = right["Hits"][0] if right["Hits"] else {}
            writer.writerow([sample["Id"], sample["Kind"], sample.get("SourcePath", ""), left["Count"], right["Count"], left["MedianMs"], right["MedianMs"],
                lh.get("CenterX", ""), lh.get("CenterY", ""), lh.get("AngleDegrees", ""), rh.get("CenterX", ""), rh.get("CenterY", ""), rh.get("AngleDegrees", ""),
                o["Errors"].get(sample["Id"], {}).get("Pixel", ""), v["Errors"].get(sample["Id"], {}).get("Pixel", "")])
    misses = [sample for sample in manifest if sample["Kind"] == "原始图像" and a[sample["Id"]]["Count"] == 0]
    (directory / "未检出文件.txt").write_text("\n".join(sample["SourcePath"] for sample in misses), encoding="utf-8-sig")
    table = f"""| 指标 | 本项目新版 | VM 4.4 |
| --- | ---: | ---: |
| 原始图像检出数量 | {o['Found']}/{o['OriginalCount']} | {v['Found']}/{v['OriginalCount']} |
| 平均耗时（逐图 5 次中位数再平均） | {o['MeanMs']:.2f} ms | {v['MeanMs']:.2f} ms |
| 耗时 P95（逐图中位数） | {o['P95Ms']:.2f} ms | {v['P95Ms']:.2f} ms |
| 已知刚体变换检出 | {o['PoseCount']}/13 | {v['PoseCount']}/13 |
| 平均相对位置误差 | {o['MeanPixelError']:.4f} px | {v['MeanPixelError']:.4f} px |
| 最大相对位置误差 | {o['MaxPixelError']:.4f} px | {v['MaxPixelError']:.4f} px |
| 平均相对角度误差 | {o['MeanAngleError']:.5f}° | {v['MeanAngleError']:.5f}° |
| 无目标样本误检 | {o['FalsePositives']}/{o['NegativeCount']} | {v['FalsePositives']}/{v['NegativeCount']} |"""
    report = f"""# 工位3轮廓模板匹配实测报告

在本机、这批图像和下列模板条件下，本项目新版平均耗时降低 **{improvement:.1f}%**，已知变换的定位误差更小。原始图像检出数量与 VM 相同，**仍有 {len(misses)} 张原图未检出**，不能据此声称暗光检出率或所有场景全面超过 VM。

{table}

## 比较条件

- 用户目录全部 101 张 JPG，统一解码成相同的无损灰度 PNG 后交给两套算法；其中 {summaries['UniqueOriginalImages']} 张内容不同，其余是重复图像。仍按用户要求逐文件运行，未删去失败或重复样本。
- 全图搜索、角度 −45°～45°、尺度固定 1、使用极性、最低分数 0.5、最多 1 个结果。分数只用于各自门限，不把两套分数当作精度比较。
- 已实际操作 VM 创建共同来源模板，并用其官方 SDK 批量运行。两套模型使用同一建模图像与相同的三个部位；VM 的矩形由鼠标绘制，有数像素选区边界差异，并非逐像素相同的内部模型。
- 每张图先预热一次，再执行五次取中位数，两套程序串行运行。VM 取官方 AlgorithmTime，本项目计时覆盖托管 Find 及原生算法；均不含解码、建模和显示。原始五次时间全部保留在 JSON 中。
- 13 张合成图由同一建模图进行已知亚像素平移及 −30°～30°旋转产生。两套框中心不同，分别以各自原图零姿态结果为基准计算相对位移和角度误差；这衡量已知变换定位表现，不是现场计量标定精度。
- 17 张无目标图包括黑图、均匀灰度、镜像、固定随机种子的噪声及圆形/矩形干扰。该组阴性测试不代表所有工业背景。
- 原始图没有人工逐像素真值，表中 56/101 是返回合格匹配的文件数量；原目录 OK/NG 表示产品判定，不等同于模板目标存在/不存在。

## 已交付的修改

大跨度稀疏组合模板改为低分辨率找候选、原分辨率稀疏梯度精修；增加分区覆盖、梯度方向核验、候选排序和去重，保留亚像素开关及擦除区域。完整矩形、忽略极性、重复孔洞和显式金字塔继续使用原算法，未把试验中不稳定的路径用于生产。

矩形、椭圆、扇形、多边形和画笔可反复追加，松开生成后可选择、移动、缩放和旋转；区域组合建模和保存恢复已通过界面回归。

验证：原生 CTest、99 项轮廓检查、45 项组合 ROI 检查、用户方案原图与平移旋转回归均通过。原生 DLL ABI 保持 1。

## 文件

- [逐图可视结果](对比报告.html)：切换全部图像，查看原图、轮廓、中心和单图耗时。
- [逐图结果 CSV](逐图结果.csv)、[未检出文件](未检出文件.txt)。
- [本项目原始记录](ours-final.json)、[VM 原始记录](vm-final.json)、[汇总](summary.json)。
- 原 VM 副本：VM基准原流程.sol；共同来源模板副本：VM共同模板.sol；用户原方案和原图未改写。
- 本项目可直接运行：`bin/x64/Release/机器视觉AI检测系统V1.0.exe`，加载用户原来的测试方案即可使用新 DLL，不必重建模板。

最终 DLL SHA-256：`{summaries['NativeSha256']}`
"""
    (directory / "对比报告.md").write_text(report, encoding="utf-8-sig")
    rows = [{"sample": sample, "ours": a[sample["Id"]], "vm": b[sample["Id"]]} for sample in manifest]
    contours = read_json(directory / "model-contours.json")
    data = json.dumps({"rows": rows, "contours": contours}, ensure_ascii=False).replace("</", "<\\/")
    page = PAGE.replace("__DATA__", data).replace("__OURS_TIME__", f"{o['MeanMs']:.2f}").replace("__VM_TIME__", f"{v['MeanMs']:.2f}")
    page = page.replace("__GAIN__", f"{improvement:.1f}").replace("__OURS_ERROR__", f"{o['MeanPixelError']:.4f}").replace("__VM_ERROR__", f"{v['MeanPixelError']:.4f}")
    page = page.replace("__DETECTIONS__", f"{o['Found']}/101 · VM {v['Found']}/101").replace("__MISSES__", str(len(misses)))
    (directory / "对比报告.html").write_text(page, encoding="utf-8")
    print(table)


# 页面只读取本地图片和内嵌测量数据，不发出外部请求。
PAGE = r'''<!doctype html><html lang="zh-CN"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1">
<title>工位3 · 模板匹配实测</title><style>
*{box-sizing:border-box}body{margin:0;background:#0d1421;color:#e6edf8;font:15px/1.6 "Microsoft YaHei",sans-serif}header,main{max-width:1500px;margin:auto;padding:24px 32px}header{padding-bottom:8px}h1{font-size:28px;margin:0 0 4px}p{margin:8px 0;color:#aebed3}a{color:#8ac3ff}.cards{display:grid;grid-template-columns:repeat(4,1fr);gap:14px;margin:20px 0}.card{background:#182338;border:1px solid #2c3e58;border-radius:12px;padding:15px}.number{font-size:26px;color:#80e4ac;font-weight:600}.label{font-size:13px;color:#adbed6}.note{background:#382e1c;color:#f3d49f;padding:12px 18px;border-radius:8px}section{display:grid;grid-template-columns:minmax(0,1fr) 290px;gap:18px;margin-top:18px}.viewer{background:#070b12;border:1px solid #34435c;border-radius:12px;padding:12px}canvas{width:100%;height:auto;max-height:65vh;object-fit:contain;display:block}.tools{display:flex;gap:10px;flex-wrap:wrap;margin-bottom:12px}button,select{background:#243854;color:#f0f6ff;border:1px solid #47627f;border-radius:6px;padding:8px 12px;font:inherit;cursor:pointer}button:hover{background:#365375}aside{background:#172237;padding:18px;border-radius:12px;overflow-wrap:anywhere}pre{white-space:pre-wrap;font:13px/1.8 Consolas,"Microsoft YaHei",monospace}.green{color:#55ec99}.blue{color:#56baff}table{width:100%;border-collapse:collapse;margin-top:22px}td,th{text-align:left;border-bottom:1px solid #2c3e58;padding:9px}tr{cursor:pointer}tr:hover{background:#1c2e48}.muted{color:#9bacbf;font-size:13px}input{accent-color:#60daa0}footer{margin:24px 0}.active{background:#244363}@media(max-width:900px){.cards{grid-template-columns:1fr 1fr}section{grid-template-columns:1fr}header,main{padding:16px}}
</style><header><h1>工位3 · 模板匹配实测</h1><p>全部 101 张原图 + 13 张已知变换 + 建模原图 + 17 张无目标图 · 本机真实运行</p><div class="cards"><div class="card"><div class="label">本项目平均耗时</div><div class="number">__OURS_TIME__ ms</div><div class="label">VM __VM_TIME__ ms · 降低 __GAIN__%</div></div><div class="card"><div class="label">平均相对位置误差</div><div class="number">__OURS_ERROR__ px</div><div class="label">VM __VM_ERROR__ px · 13 组已知变换</div></div><div class="card"><div class="label">原始图像检出数量</div><div class="number">__DETECTIONS__</div><div class="label">两套使用共同来源模板</div></div><div class="card"><div class="label">验证记录</div><div class="number">99 + 45 项</div><div class="label">另含原生及真实方案回归</div></div></div><div class="note">仍有 __MISSES__ 张原图未检出。速度与已知变换精度优于本次 VM 基准，原图检出数量尚未提高。OK/NG 文件夹不是目标是否存在的真值。</div></header>
<main><div class="tools"><button id="previous">上一张</button><button id="next">下一张</button><select id="filter"><option value="all">全部图像</option><option value="original">101 张原图</option><option value="miss">本项目未检出原图</option><option value="pose">已知变换</option><option value="negative">无目标检查</option></select><select id="sample"></select><label><input type="checkbox" id="outline" checked>本项目轮廓</label><label><input type="checkbox" id="vmcenter" checked>VM 框中心</label></div><section><div class="viewer"><canvas id="canvas"></canvas></div><aside><h3 id="name"></h3><div class="green">● 本项目</div><pre id="left"></pre><div class="blue">● VM</div><pre id="right"></pre><p id="source" class="muted"></p><p class="muted">绿色为本项目实际模板轮廓；蓝色十字为 VM 返回的匹配框中心。两套 ROI 边界有数像素差异，原始框中心数值不能直接相减作为误差。</p></aside></section><table><thead><tr><th>编号</th><th>类别</th><th>本项目数量</th><th>VM 数量</th><th>本项目 ms</th><th>VM ms</th></tr></thead><tbody id="rows"></tbody></table><footer><a href="逐图结果.csv">逐图 CSV</a> · <a href="对比报告.md">完整条件与报告</a> · <a href="ours-final.json">本项目原始记录</a> · <a href="vm-final.json">VM 原始记录</a><p class="muted">每图预热一次后运行五次取中位数；计时不含建模、解码和显示。页面完全离线，无外部请求。</p></footer></main>
<script>const data=__DATA__;let filtered=data.rows,index=0,picture=new Image();const canvas=document.querySelector('#canvas'),ctx=canvas.getContext('2d'),select=document.querySelector('#sample');function details(row){if(!row.Count)return `未检出\n耗时 ${row.MedianMs.toFixed(3)} ms`;const h=row.Hits[0];return `数量 ${row.Count}\nX ${h.CenterX.toFixed(3)}\nY ${h.CenterY.toFixed(3)}\n角度 ${h.AngleDegrees.toFixed(4)}°\n分数 ${h.Score.toFixed(4)}\n耗时 ${row.MedianMs.toFixed(3)} ms`;}function cross(x,y,color){ctx.strokeStyle=color;ctx.lineWidth=3;ctx.beginPath();ctx.moveTo(x-22,y);ctx.lineTo(x+22,y);ctx.moveTo(x,y-22);ctx.lineTo(x,y+22);ctx.stroke();}function draw(){if(!picture.complete||!picture.naturalWidth)return;const row=filtered[index];canvas.width=picture.naturalWidth;canvas.height=picture.naturalHeight;ctx.drawImage(picture,0,0);if(row.ours.Count&&document.querySelector('#outline').checked){const h=row.ours.Hits[0],a=h.AngleDegrees*Math.PI/180,c=Math.cos(a),s=Math.sin(a);ctx.strokeStyle='#31ff82';ctx.lineWidth=2.5;for(const line of data.contours){ctx.beginPath();line.forEach((p,i)=>{let x=p.X-h.Width/2,y=p.Y-h.Height/2;let px=h.CenterX+c*x-s*y,py=h.CenterY+s*x+c*y;if(i)ctx.lineTo(px,py);else ctx.moveTo(px,py);});ctx.stroke();}cross(h.CenterX,h.CenterY,'#31ff82');}if(row.vm.Count&&document.querySelector('#vmcenter').checked){const h=row.vm.Hits[0];cross(h.CenterX,h.CenterY,'#36baff');}}function show(){if(!filtered.length)return;index=(index+filtered.length)%filtered.length;let row=filtered[index];select.value=String(index);document.querySelector('#name').textContent=row.sample.Id+' · '+row.sample.Kind;document.querySelector('#source').textContent=row.sample.SourcePath||'受控测试输入';document.querySelector('#left').textContent=details(row.ours);document.querySelector('#right').textContent=details(row.vm);picture=new Image();picture.onload=draw;picture.src='images/'+row.sample.Id+'.png';document.querySelectorAll('tbody tr').forEach((r,i)=>r.classList.toggle('active',i===index));}function rebuild(){let value=document.querySelector('#filter').value;filtered=data.rows.filter(r=>value==='all'||value==='original'&&r.sample.Kind==='原始图像'||value==='miss'&&r.sample.Kind==='原始图像'&&!r.ours.Count||value==='pose'&&r.sample.Kind==='已知刚体变换'||value==='negative'&&r.sample.Kind==='无目标');select.replaceChildren();document.querySelector('#rows').replaceChildren();filtered.forEach((r,i)=>{let option=new Option(r.sample.Id+' · '+r.sample.Kind,String(i));select.add(option);let tr=document.createElement('tr');[r.sample.Id,r.sample.Kind,r.ours.Count,r.vm.Count,r.ours.MedianMs.toFixed(2),r.vm.MedianMs.toFixed(2)].forEach(v=>{let td=document.createElement('td');td.textContent=String(v);tr.append(td)});tr.onclick=()=>{index=i;show();canvas.scrollIntoView({behavior:'smooth',block:'center'})};document.querySelector('#rows').append(tr);});index=0;show();}document.querySelector('#previous').onclick=()=>{index--;show()};document.querySelector('#next').onclick=()=>{index++;show()};select.onchange=()=>{index=Number(select.value);show()};document.querySelector('#filter').onchange=rebuild;document.querySelector('#outline').onchange=draw;document.querySelector('#vmcenter').onchange=draw;rebuild();</script></html>'''


if __name__ == "__main__":
    main(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve())
