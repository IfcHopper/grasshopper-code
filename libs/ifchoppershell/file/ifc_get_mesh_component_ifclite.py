# This is the temporary source code for sharing
import ifclite_geom
import numpy as np
import Rhino.Geometry
import System.Drawing as sd

with open(file_path, "rb") as f:
	ifc_bytes = f.read()

data = ifclite_geom.geometry_data_buffers(ifc_bytes)

mesh = []
color = []
step_id = []

for single_step_id, el in data["elements"].items():

    if(el["ifc_type"] in exclude):
        continue

    grouped_verts = np.frombuffer(el["vertices"], dtype=np.float64).reshape(-1, 3)
    grouped_faces = np.frombuffer(el["faces"],    dtype=np.uint32).reshape(-1, 3)
    
    single_color = sd.Color.FromArgb(el["color"][3]*255, el["color"][0]*255, el["color"][1]*255, el["color"][2]*255)

    color.append(single_color)

    step_id.append(single_step_id)

    element_mesh = Rhino.Geometry.Mesh()

    vertices = []
    for vertex in grouped_verts:
        vertices.append(Rhino.Geometry.Point3d(vertex[0], vertex[1], vertex[2]))
        element_mesh.Vertices.Add(vertex[0], vertex[1], vertex[2])

    for face in grouped_faces:
        if len(face) == 3:
            element_mesh.Faces.AddFace(int(face[0]), int(face[1]), int(face[2]))
        elif len(face) == 4:
            element_mesh.Faces.AddFace(int(face[0]), int(face[1]), int(face[2]), int(face[3]))

    # Prepare Rhino transformation matrix
    rtsmatrix = Rhino.Geometry.Transform(1.0)

    element_mesh.Normals.ComputeNormals()
    element_mesh.Compact()
    element_mesh.Unweld(0, True)

    element_mesh.Transform(rtsmatrix)

    # Append geometry and property set info for output
    mesh.append(element_mesh)