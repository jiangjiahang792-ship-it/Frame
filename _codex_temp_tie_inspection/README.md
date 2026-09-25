# 扎带端头截取 Demo

这是一个独立的 C#/.NET 8 WinForms 示例，仅使用 OpenCvSharp，不引用 TDJS-Vision 当前框架代码。

## 功能

- 加载 PNG、JPG、BMP、TIF 等图像，支持中文路径。
- 原图、检测标注图、黑色二值图和端头提取图都支持 10%～2000% 缩放。
- 鼠标滚轮以光标位置为中心缩放，右键拖动平移，双击恢复适应窗口。
- 顶部“全部放大”“全部缩小”“全部适应”按钮可以同时操作四块图像。
- 在原图上按住鼠标左键拖动，框选扎带右端检测区。
- ROI 左边界就是切割线，只保留右侧端头，从而打断长条连通域。
- 用三个颜色通道的最大值做黑色二值化，红色标注框不会被误当成黑色目标。
- 通过连通域取得每根扎带，按从上到下编号。
- 每个连通域先按主轴旋正并自动统一为圆头朝右，再用自适应纹理能量寻找“齿纹结束、光滑端头开始”的结构边界。
- 单根纹理不明显时，使用同一张图中其他扎带的稳健边界趋势补齐；从最终边界裁剪到外轮廓最右端。
- 使用同图宽高统计和填充率过滤金属细线、画面边缘残片等伪区域。
- 纹理灵敏度可控制弱末纹的识别能力；起点修正可在自动结果上按像素左右微调。
- 本 Demo 不计算圆度、不执行缺角判定，只负责稳定输出端头截取区域。

## 运行

1. 用 Visual Studio 2022 打开 `TieCornerInspection.sln`，选择 x64 后运行 `TieCornerInspection.WinForms`。
2. 程序会自动加载 `samples/扎带右端样图.png`；也可以点击“打开图片”。
3. 样图已经预置 ROI：`X=810, Y=50, 宽=147, 高=1125`。
4. 点击“执行检测”，程序会自动切换到“端头提取图”，底部表格会输出每个区域的端头起点、裁剪宽度和定位来源。
5. 将鼠标移到任意图像上滚动滚轮即可放大或缩小；按住右键拖动可平移；双击图像可恢复完整显示。原图左键仍用于框选 ROI。

也可以在项目目录执行：

```powershell
dotnet run --project .\src\TieCornerInspection.WinForms\TieCornerInspection.WinForms.csproj -c Release
```

## 默认参数

| 参数 | 默认值 | 说明 |
|---|---:|---|
| 黑色阈值 | 180 | B、G、R 三通道最大值小于等于该值时视为黑色 |
| 最小面积 | 120 | 过滤小噪声 |
| 最小宽度 | 30 | 过滤非扎带黑点 |
| 高度范围 | 6～60 | 兼容高分辨率图中的倾斜扎带，同时过滤过细或过高区域 |
| 闭运算核 | 3 | 填补细小断裂，必须是正奇数 |
| 纹理灵敏度 | 60 | 0～100；越高越容易识别较弱的最后几条纹路 |
| 起点修正(px) | 0 | -100～100；正数向端头方向移动，负数向齿纹方向移动 |

## 结构边界与端头提取原理

程序先用黑色二值图提取并打断每根扎带连通域，再把每根扎带及对应彩色区域用同一个变换旋正。若齿纹位于右侧，则同步水平翻转，使光滑圆头始终位于右侧；结果回映时会自动撤销翻转。

默认定位器在扎带中间 50% 高度计算逐列亮度中位数和相邻列差分能量，使用当前扎带自身的中位数与中位绝对偏差归一化，因此不依赖固定亮度差。它优先选择最靠右的可信纹理转折，再回查最后一个真实纹理沿，同时要求右侧保留足够长的光滑端头，以排除端部高亮和渐变。

单根没有足够纹理时，检测器用同图高置信边界拟合稳健直线，在预测位置附近再次细化。表格中的“局部纹理”表示单根直接定位，“整体补齐”表示由同图趋势补齐；只有整图证据不足时才显示“未找到”。自动定位后统一应用“起点修正(px)”，裁剪区域从修正后的起点到旋正后扎带最右端，并保留完整高度。

## 样图实测结论

对 `C:\Users\34652\Desktop\20260728\20260728\NG` 的 45 张 3072×2048 JPG 批量回归，共保留 1094 个有效扎带区域：1054 个由局部纹理定位，40 个由同图趋势补齐，最终未找到 0 个；端头裁剪宽度为 `159～235` 像素。

## 项目结构

- `src/TieCornerInspection.WinForms/MainForm.Designer.cs`：全部窗体控件与布局。
- `src/TieCornerInspection.WinForms/MainForm.cs`：交互、ROI 坐标换算和结果展示。
- `src/TieCornerInspection.WinForms/Controls/ImageViewport.cs`：缩放、平移和坐标换算模型。
- `src/TieCornerInspection.WinForms/Controls/ZoomableImageBox.cs`：可缩放 WinForms 图像查看控件。
- `src/TieCornerInspection.WinForms/Services/IRightEndInspector.cs`：可替换检测接口。
- `src/TieCornerInspection.WinForms/Services/IRightEndBoundaryLocator.cs`：可替换端头结构边界定位接口。
- `src/TieCornerInspection.WinForms/Services/TextureTransitionEndBoundaryLocator.cs`：自适应纹理转折定位实现。
- `src/TieCornerInspection.WinForms/Services/OpenCvRightEndInspector.cs`：OpenCV 检测实现。
- `tests/TieCornerInspection.Tests`：合成图与现场样图自动化测试。
- `任务记录.md`：本次开发与验证记录。
