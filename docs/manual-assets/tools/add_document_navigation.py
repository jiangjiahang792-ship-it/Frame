"""为 TDJS-Vision 客户说明书补充静态目录内部跳转。

脚本只在临时 DOCX 中添加书签和内部超链接，不修改标题、正文、图片、
分页参数或现有样式。PDF 导出时，目录条目可点击跳转到对应一级章节。
"""

from __future__ import annotations

import argparse
import re
import zipfile
from copy import copy
from pathlib import Path

from lxml import etree


WORD_NAMESPACE = "http://schemas.openxmlformats.org/wordprocessingml/2006/main"
NAMESPACES = {"w": WORD_NAMESPACE}
DOCUMENT_XML_PATH = "word/document.xml"


def qn(local_name: str) -> str:
    """返回 WordprocessingML 属性或元素的完整限定名。"""

    return f"{{{WORD_NAMESPACE}}}{local_name}"


def paragraph_text(paragraph: etree._Element) -> str:
    """读取段落中的全部可见文字。"""

    return "".join(paragraph.xpath(".//w:t/text()", namespaces=NAMESPACES)).strip()


def normalize_heading_text(text: str) -> str:
    """去除章节序号、空白和冒号，便于匹配显示格式略有差异的标题。"""

    without_number = re.sub(r"^\d+[\s\u3000]+", "", text).strip()
    return re.sub(r"[\s\u3000：:]+", "", without_number)


def next_bookmark_id(root: etree._Element) -> int:
    """计算不会与文档现有书签冲突的新书签编号。"""

    ids: list[int] = []
    for element in root.xpath(".//w:bookmarkStart", namespaces=NAMESPACES):
        raw_id = element.get(qn("id"))
        if raw_id and raw_id.isdigit():
            ids.append(int(raw_id))
    return max(ids, default=-1) + 1


def add_bookmark(paragraph: etree._Element, bookmark_id: int, name: str) -> None:
    """在目标标题段落首尾添加一个 Word 内部书签。"""

    bookmark_start = etree.Element(qn("bookmarkStart"))
    bookmark_start.set(qn("id"), str(bookmark_id))
    bookmark_start.set(qn("name"), name)

    bookmark_end = etree.Element(qn("bookmarkEnd"))
    bookmark_end.set(qn("id"), str(bookmark_id))

    paragraph_properties = paragraph.find(qn("pPr"))
    insertion_index = 1 if paragraph_properties is not None else 0
    paragraph.insert(insertion_index, bookmark_start)
    paragraph.append(bookmark_end)


def wrap_paragraph_with_hyperlink(paragraph: etree._Element, anchor: str) -> None:
    """将目录段落的可见内容包装为内部超链接，同时保留原有段落格式。"""

    hyperlink = etree.Element(qn("hyperlink"))
    hyperlink.set(qn("anchor"), anchor)
    hyperlink.set(qn("history"), "1")

    movable_children = [child for child in paragraph if child.tag != qn("pPr")]
    if not movable_children:
        raise ValueError(f"目录段落没有可链接内容：{paragraph_text(paragraph)}")

    for child in movable_children:
        paragraph.remove(child)
        hyperlink.append(child)
    paragraph.append(hyperlink)


def add_navigation(document_xml: bytes) -> tuple[bytes, int]:
    """为静态目录添加书签与内部超链接，并返回修改后的 XML。"""

    parser = etree.XMLParser(remove_blank_text=False)
    root = etree.fromstring(document_xml, parser)
    body_paragraphs = root.xpath("./w:body/w:p", namespaces=NAMESPACES)
    texts = [paragraph_text(paragraph) for paragraph in body_paragraphs]

    try:
        navigation_index = texts.index("内容导航")
    except ValueError as exc:
        raise ValueError("文档中未找到“内容导航”标题。") from exc

    first_numbered_heading_index = next(
        index
        for index in range(navigation_index + 1, len(texts))
        if re.match(r"^1[\s\u3000]+", texts[index])
    )

    navigation_paragraphs = [
        paragraph
        for paragraph in body_paragraphs[navigation_index + 1 : first_numbered_heading_index]
        if paragraph_text(paragraph)
    ]
    if len(navigation_paragraphs) != 16:
        raise ValueError(f"目录条目数量异常，预期 16，实际 {len(navigation_paragraphs)}。")

    bookmark_id = next_bookmark_id(root)
    linked_count = 0
    for item_index, navigation_paragraph in enumerate(navigation_paragraphs, start=1):
        navigation_text = paragraph_text(navigation_paragraph)
        target_paragraph = next(
            (
                paragraph
                for paragraph in body_paragraphs[first_numbered_heading_index:]
                if normalize_heading_text(paragraph_text(paragraph))
                == normalize_heading_text(navigation_text)
            ),
            None,
        )
        if target_paragraph is None:
            raise ValueError(f"未找到目录条目对应章节：{navigation_text}")

        bookmark_name = f"TDJS_SECTION_{item_index:02d}"
        add_bookmark(target_paragraph, bookmark_id, bookmark_name)
        wrap_paragraph_with_hyperlink(navigation_paragraph, bookmark_name)
        bookmark_id += 1
        linked_count += 1

    output = etree.tostring(root, xml_declaration=True, encoding="UTF-8", standalone="yes")
    return output, linked_count


def build_navigation_docx(source: Path, output: Path) -> int:
    """复制源 DOCX，并仅替换已补充导航的 document.xml。"""

    output.parent.mkdir(parents=True, exist_ok=True)
    with zipfile.ZipFile(source, "r") as source_archive:
        document_xml, linked_count = add_navigation(source_archive.read(DOCUMENT_XML_PATH))
        with zipfile.ZipFile(output, "w") as output_archive:
            for source_info in source_archive.infolist():
                output_info = copy(source_info)
                payload = document_xml if source_info.filename == DOCUMENT_XML_PATH else source_archive.read(source_info)
                output_archive.writestr(output_info, payload)
    return linked_count


def parse_arguments() -> argparse.Namespace:
    """解析命令行参数。"""

    parser = argparse.ArgumentParser(description="为说明书静态目录添加内部跳转。")
    parser.add_argument("source", type=Path, help="源 DOCX 文件路径。")
    parser.add_argument("output", type=Path, help="带目录跳转的临时 DOCX 输出路径。")
    return parser.parse_args()


def main() -> None:
    """执行目录跳转补充并输出处理结果。"""

    arguments = parse_arguments()
    linked_count = build_navigation_docx(arguments.source, arguments.output)
    print(f"已添加 {linked_count} 个目录内部跳转：{arguments.output}")


if __name__ == "__main__":
    main()
