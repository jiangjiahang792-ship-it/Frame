# VM 共同来源模板基准

本目录保存复现程序，现场图像和生成结果保留在本机 `artifacts/VMComparison`，不提交用户图像。

先构建 Release/x64。输入为用户测试方案和“工位3”目录，程序只读取数据，不运行方案设备节点。准备程序固定比较范围为全图、−45°～45°、0.5 分数和 1 个目标；它保留已选组合区域、建模极性、原图和擦除记录。

```powershell
.\Tests\VMComparison\ContourDatasetRunner.ps1 -RunnerArguments @('prepare', 'C:/Users/34652/Desktop/测试模板匹配.Sol', 'C:/Users/34652/Desktop/工位3', 'artifacts/VMComparison')
.\Tests\VMComparison\ContourDatasetRunner.ps1 -RunnerArguments @('controls', 'artifacts/VMComparison')
.\Tests\VMComparison\ContourDatasetRunner.ps1 -RunnerArguments @('run', 'artifacts/VMComparison', 'artifacts/VMComparison/ours-final.json', '0', '5')
.\Tests\VMComparison\VMReferenceRunner.ps1 -SolutionPath 'artifacts/VMComparison/VM共同模板.sol' -ImagePath 'D:/MyCode/PublicWook/TDJS-Vision/artifacts/VMComparison' -ResultName 'vm-final.json'
```

两套计时必须串行。VM 副本需要事先在 VM 4.4 中用 `images/template.png` 和相同三个部位创建；保留原方案另存副本。官方 SDK 每次执行重新交付 Mono8 图像，任何错误码都会终止基准，不能将错误当漏检。

使用 Python 3 运行 `CreateReport.py artifacts/VMComparison bin/x64/Release/ShapeMatchNative.dll`，输出中文 Markdown、逐图 CSV、JSON 汇总和可离线双击打开的 HTML。报告明确列出未检出文件、重复图像数和比较边界。当前姿态误差计算对应本次模板外接框，修改选区需要同步重建清单及参考坐标，程序会拒绝不匹配的外接框。

`ContourLivePreview.ps1` 编译一个复用生产节点 Designer 的预览入口。默认加载本次基准参数及第一张原图，启动时自动运行一次；可在真实界面继续加载其他图片、执行和编辑模板。它不连接设备，不改写用户原方案。

正式报告仅使用 `ours-final.json` 和 `vm-final.json`。带 `probe`、`v1`～`v5`、`head`、`dark` 或 `ignore` 的文件是探索记录，不作为交付配置或正式结论。
