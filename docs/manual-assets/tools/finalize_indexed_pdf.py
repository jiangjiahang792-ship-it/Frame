"""设置客户说明书 PDF 的默认导航显示模式。

脚本保留原 PDF 的页面、内部链接、书签树和文档元数据，只将默认打开模式
设为显示书签侧栏，便于客户快速定位章节。
"""

from __future__ import annotations

import argparse
from pathlib import Path

from pypdf import PdfReader, PdfWriter
from pypdf.generic import NameObject


def finalize_pdf(source: Path, output: Path) -> None:
    """复制 PDF 全部对象，并启用书签侧栏作为默认打开模式。"""

    reader = PdfReader(source)
    writer = PdfWriter()
    writer.clone_document_from_reader(reader)
    writer._root_object[NameObject("/PageMode")] = NameObject("/UseOutlines")

    output.parent.mkdir(parents=True, exist_ok=True)
    with output.open("wb") as stream:
        writer.write(stream)


def parse_arguments() -> argparse.Namespace:
    """解析输入和输出 PDF 路径。"""

    parser = argparse.ArgumentParser(description="设置 PDF 默认显示书签侧栏。")
    parser.add_argument("source", type=Path, help="WPS 导出的源 PDF。")
    parser.add_argument("output", type=Path, help="最终 PDF 输出路径。")
    return parser.parse_args()


def main() -> None:
    """执行 PDF 导航模式设置。"""

    arguments = parse_arguments()
    finalize_pdf(arguments.source, arguments.output)
    print(f"已设置 PDF 默认书签侧栏：{arguments.output}")


if __name__ == "__main__":
    main()
