import copy
import json
import sys
import uuid
from pathlib import Path


IMAGE_SOURCE_PARAM = "TDJS_Vision.Node._1_Acquisition.ImageSource.NodeParamImageSoucre"
SHARED_VARIABLE_PARAM = "TDJS_Vision.Node._6_LogicTool.SharedVariable.NodeParamSharedVariable"
MULTI_CONDITION_PARAM = "TDJS_Vision.Node._6_LogicTool.MultiCondition.NodeParamMultiCondition"
MODBUS_READ_PARAM = "TDJS_Vision.Node._5_EquipmentCommunication.ModbusRead.NodeParamModbusRead"
PROCESS_TRIGGER_PARAM = "TDJS_Vision.Node._6_LogicTool.ProcessTrigger.NodeParamProcessTrigger"


def first_param(node):
    node_param = node.get("NodeParam")
    if not node_param:
        return None, None
    key = next(iter(node_param))
    return key, node_param[key]


def get_process(solution, process_id):
    for process in solution["ProcessInfos"]:
        if process["ID"] == process_id:
            return process
    raise KeyError(f"找不到流程ID：{process_id}")


def get_node(process, node_id):
    for node in process["NodeInfos"]:
        if node["ID"] == node_id:
            return node
    raise KeyError(f"流程{process['ProcessName']}找不到节点ID：{node_id}")


def set_image_source_to_shared(process, node_id, variable_name):
    node = get_node(process, node_id)
    _, param = first_param(node)
    if param is None:
        raise ValueError(f"节点{node_id}不是有效图像源参数")
    param["ImageSource"] = "共享变量"
    param["PathText"] = None
    param["ImagePath"] = None
    param["IsAutoLoop"] = False
    param["CameraName"] = ""
    param["TriggerModel"] = "On"
    param["TriggerSource"] = "SOFT"
    param["SharedVariableName"] = variable_name
    return node


def disable_nodes(process, node_ids):
    for node_id in node_ids:
        node = get_node(process, node_id)
        node["Active"] = False
        node["IsStartNode"] = False


def remove_connections_touching(process, node_ids):
    blocked = set(node_ids)
    process["ConnectionInfos"] = [
        connection for connection in process.get("ConnectionInfos", [])
        if connection["FromNodeId"] not in blocked and connection["ToNodeId"] not in blocked
    ]


def add_connection(process, from_id, to_id, from_anchor="Bottom", to_anchor="Top", branch="Default"):
    for connection in process.get("ConnectionInfos", []):
        if (
            connection["FromNodeId"] == from_id
            and connection["ToNodeId"] == to_id
            and connection.get("Branch", "Default") == branch
        ):
            return
    process.setdefault("ConnectionInfos", []).append({
        "ID": uuid.uuid4().hex,
        "FromNodeId": from_id,
        "ToNodeId": to_id,
        "FromAnchor": from_anchor,
        "ToAnchor": to_anchor,
        "Branch": branch,
    })


def build_node(node_id, node_type, node_name, x, y, param_type=None, param=None, active=True, start=False):
    return {
        "NodeType": node_type,
        "NodeName": node_name,
        "ID": node_id,
        "Active": active,
        "OutputLog": True,
        "Selected": False,
        "HasCanvasLayout": True,
        "CanvasX": x,
        "CanvasY": y,
        "CanvasWidth": 240,
        "CanvasHeight": 64,
        "IsStartNode": start,
        "NodeParam": None if param_type is None else {param_type: param},
    }


def build_connection(from_id, to_id, from_anchor="Bottom", to_anchor="Top", branch="Default"):
    return {
        "ID": uuid.uuid4().hex,
        "FromNodeId": from_id,
        "ToNodeId": to_id,
        "FromAnchor": from_anchor,
        "ToAnchor": to_anchor,
        "Branch": branch,
    }


def shared_write_param(source_node_id, variable_name):
    return {
        "IsRead": False,
        "ReadName": variable_name,
        "Type": "ImageSource",
        "Flag": False,
        "Index": 0,
        "Text1": f"{source_node_id}.图像源",
        "Text2": "输出图像",
        "WriteName": variable_name,
    }


def process_trigger_param(process_name):
    return {
        "UseOkOrNg": False,
        "Text1": "",
        "Text2": "",
        "OKProcessName": "",
        "NGProcessName": "",
        "ProcessName": process_name,
    }


def modbus_condition_param(source_node_id, address, original_condition):
    condition = copy.deepcopy(original_condition)
    condition["SourceNodeId"] = source_node_id
    condition["SourceNodeText"] = f"{source_node_id}.Modbus读取"
    condition["PropertyPath"] = f"$variable:值01(地址{address})"
    condition["PropertyDisplayName"] = f"变量.值01(地址{address})"
    return {
        "MatchMode": 0,
        "Conditions": [condition],
    }


def clone_modbus_read_param(source_process, node_id):
    _, param = first_param(get_node(source_process, node_id))
    return copy.deepcopy(param)


def clone_camera_param(source_process, node_id, trigger_source="LINE0"):
    _, param = first_param(get_node(source_process, node_id))
    cloned = copy.deepcopy(param)
    cloned["ImageSource"] = "相机"
    cloned["TriggerModel"] = "On"
    cloned["TriggerSource"] = trigger_source
    return cloned


def build_entry_process(process_id, name, group, level, nodes, connections):
    return {
        "ID": process_id,
        "ProcessName": name,
        "Enable": True,
        "ShowLog": True,
        "IsPassiveTriggered": False,
        "IsCameraCallbackTriggered": False,
        "Level": level,
        "Group": group,
        "NodeInfos": nodes,
        "HasCanvasGraph": True,
        "ConnectionInfos": connections,
        "OKNumber": 0,
        "NGNumber": 0,
    }


def main(input_path, output_path):
    source = Path(input_path)
    target = Path(output_path)
    solution = json.loads(source.read_text(encoding="utf-8"))

    p1 = get_process(solution, 1)
    p2 = get_process(solution, 2)
    p3 = get_process(solution, 3)
    p4 = get_process(solution, 4)

    top_camera_param = clone_camera_param(p2, 13, "LINE0")
    bottom_camera_param = clone_camera_param(p4, 34, "LINE0")

    for process in (p1, p2, p3, p4):
        process["IsPassiveTriggered"] = True

    set_image_source_to_shared(p1, 1, "上相机图像")
    disable_nodes(p1, [61, 62])
    remove_connections_touching(p1, [61, 62])
    for start_node_id in (2, 137, 7, 9):
        add_connection(p1, 1, start_node_id)

    set_image_source_to_shared(p2, 13, "上相机图像")
    disable_nodes(p2, [63, 65])
    remove_connections_touching(p2, [63, 65])
    for start_node_id in (14, 17):
        add_connection(p2, 13, start_node_id)

    set_image_source_to_shared(p3, 24, "上相机图像")
    disable_nodes(p3, [67, 66])
    remove_connections_touching(p3, [67, 66])
    add_connection(p3, 24, 25)

    set_image_source_to_shared(p4, 34, "下相机图像")
    disable_nodes(p4, [68, 69])
    remove_connections_touching(p4, [68, 69])
    add_connection(p4, 34, 35)

    _, flow1_modbus_condition = first_param(get_node(p1, 62))
    _, flow2_modbus_condition = first_param(get_node(p2, 65))
    _, flow3_modbus_condition = first_param(get_node(p3, 66))
    _, flow4_modbus_condition = first_param(get_node(p4, 69))

    top_nodes = [
        build_node(145, "ImageSource", "图像源", 80, 80, IMAGE_SOURCE_PARAM, top_camera_param, start=True),
        build_node(146, "SharedVariable", "写入上相机图像", 80, 190, SHARED_VARIABLE_PARAM, shared_write_param(145, "上相机图像")),
        build_node(147, "ModbusRead", "Modbus读取", 80, 300, MODBUS_READ_PARAM, clone_modbus_read_param(p1, 61)),
        build_node(148, "MultiCondition", "判断流程1", 80, 420, MULTI_CONDITION_PARAM, modbus_condition_param(147, 1, flow1_modbus_condition["Conditions"][0])),
        build_node(149, "ProcessTrigger", "触发流程1", 80, 540, PROCESS_TRIGGER_PARAM, process_trigger_param("流程1")),
        build_node(150, "MultiCondition", "判断流程2", 360, 420, MULTI_CONDITION_PARAM, modbus_condition_param(147, 1, flow2_modbus_condition["Conditions"][0])),
        build_node(151, "ProcessTrigger", "触发流程2", 360, 540, PROCESS_TRIGGER_PARAM, process_trigger_param("流程2")),
        build_node(152, "MultiCondition", "判断流程3", 640, 420, MULTI_CONDITION_PARAM, modbus_condition_param(147, 1, flow3_modbus_condition["Conditions"][0])),
        build_node(153, "ProcessTrigger", "触发流程3", 640, 540, PROCESS_TRIGGER_PARAM, process_trigger_param("流程3")),
    ]
    top_connections = [
        build_connection(145, 146),
        build_connection(146, 147),
        build_connection(147, 148),
        build_connection(148, 149, "Right", "Top", "True"),
        build_connection(147, 150),
        build_connection(150, 151, "Right", "Top", "True"),
        build_connection(147, 152),
        build_connection(152, 153, "Right", "Top", "True"),
    ]

    bottom_nodes = [
        build_node(154, "ImageSource", "图像源", 80, 80, IMAGE_SOURCE_PARAM, bottom_camera_param, start=True),
        build_node(155, "SharedVariable", "写入下相机图像", 80, 190, SHARED_VARIABLE_PARAM, shared_write_param(154, "下相机图像")),
        build_node(156, "ModbusRead", "Modbus读取", 80, 300, MODBUS_READ_PARAM, clone_modbus_read_param(p4, 68)),
        build_node(157, "MultiCondition", "判断流程4", 80, 420, MULTI_CONDITION_PARAM, modbus_condition_param(156, 2, flow4_modbus_condition["Conditions"][0])),
        build_node(158, "ProcessTrigger", "触发流程4", 80, 540, PROCESS_TRIGGER_PARAM, process_trigger_param("流程4")),
    ]
    bottom_connections = [
        build_connection(154, 155),
        build_connection(155, 156),
        build_connection(156, 157),
        build_connection(157, 158, "Right", "Top", "True"),
    ]

    existing_process_ids = [process["ID"] for process in solution["ProcessInfos"]]
    next_process_id = max(existing_process_ids) + 1
    solution["ProcessInfos"].append(build_entry_process(next_process_id, "上相机入口", "Group1", "Lv5", top_nodes, top_connections))
    solution["ProcessInfos"].append(build_entry_process(next_process_id + 1, "下相机入口", "Group1", "Lv5", bottom_nodes, bottom_connections))

    solution["ProcessCount"] = len(solution["ProcessInfos"])
    solution["NodeCount"] = sum(len(process.get("NodeInfos", [])) for process in solution["ProcessInfos"])
    solution["SolName"] = str(target)

    target.parent.mkdir(parents=True, exist_ok=True)
    target.write_text(json.dumps(solution, ensure_ascii=False, indent=2), encoding="utf-8")


if __name__ == "__main__":
    if len(sys.argv) != 3:
        raise SystemExit("用法：adjust_solution_one_camera_flow.py 输入.Sol 输出.Sol")
    main(sys.argv[1], sys.argv[2])
