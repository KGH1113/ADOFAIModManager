"""Read the MIT-licensed WebXR hand asset for the bundled onboarding illustration."""

import hashlib
import json
from pathlib import Path
import struct
import sys


data = Path(sys.argv[1]).read_bytes()
json_size = struct.unpack_from("<I", data, 12)[0]
document = json.loads(data[20 : 20 + json_size])
binary = data[28 + json_size :]


def attribute(index):
    accessor = document["accessors"][index]
    view = document["bufferViews"][accessor["bufferView"]]
    components = {"SCALAR": 1, "VEC3": 3, "VEC4": 4}[accessor["type"]]
    value_format = {5121: "B", 5123: "H", 5126: "f"}[accessor["componentType"]]
    format_string = "<" + value_format * components
    stride = view.get("byteStride", struct.calcsize(format_string))
    offset = view.get("byteOffset", 0) + accessor.get("byteOffset", 0)
    return [
        value
        for item in range(accessor["count"])
        for value in struct.unpack_from(format_string, binary, offset + item * stride)
    ]


mesh = document["meshes"][0]["primitives"][0]
attributes = mesh["attributes"]
joints = [document["nodes"][index] for index in document["skins"][0]["joints"]]
geometry = {
    "source": "https://cdn.jsdelivr.net/npm/@webxr-input-profiles/assets@1.0/dist/profiles/generic-hand/left.glb",
    "sha256": hashlib.sha256(data).hexdigest(),
    "positionScale": 10000,
    "normalScale": 1000,
    "positions": [round(value * 10000) for value in attribute(attributes["POSITION"])],
    "normals": [round(value * 1000) for value in attribute(attributes["NORMAL"])],
    "skinJoints": attribute(attributes["JOINTS_0"]),
    "skinWeights": [round(value * 65535) for value in attribute(attributes["WEIGHTS_0"])],
    "indices": attribute(mesh["indices"]),
    "jointNames": [joint["name"] for joint in joints],
    "jointPositions": [joint["translation"] for joint in joints],
}
print(json.dumps(geometry, separators=(",", ":")))
