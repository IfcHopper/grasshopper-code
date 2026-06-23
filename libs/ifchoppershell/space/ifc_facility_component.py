import ifcopenshell
import Grasshopper.Kernel as gh

# Shortcut aliases for Grasshopper runtime message levels
e = gh.GH_RuntimeMessageLevel.Error
w = gh.GH_RuntimeMessageLevel.Warning

def ifc_facility_component(
		component: gh.GH_Component,
		model: ifcopenshell.file,
		relating_object_id: int,
		names: list[str] = ["Hopper Facility"],
		types: list[int] = [0]
	) -> tuple[ifcopenshell.file, list[int]]:
	"""
	Creates IfcFacility components in the IFC model.

	Args:
		component (gh.GH_Component): The Grasshopper component.
		model (ifcopenshell.file): The IFC model.
		relating_object_id (int): The ID of the relating object.
		names (list[str]): A list of names for the facilities to create.
		types (list[int]): A list of types for the facilities to create (0: facility, 1: road, 2: bridge, 3: railway, 4: marine facility).

	Returns:
		tuple[ifcopenshell.file, list[int]]: A tuple containing the updated model and a list of the IDs of the created facilities.
	"""

	# Set default values
	if names == None:
		names = ["Hopper Facility"]

	if types == None:
		types = [0] * len(names)

	elif len(types) != len(names):
		types = [0] * len(names)

		component.AddRuntimeMessage(w, "The length of 'Types' list should match the length of the 'Names' list. Defaulting all types to 0 (facility).")
	else:
		for i in range(len(types)):
			if types[i] not in [0, 1, 2, 3, 4]:
				types[i] = 0

				component.AddRuntimeMessage(w, f"Invalid type value at index {i}. Defaulting to 0 (facility).")

	# Intialize model
	model = ifcopenshell.file.from_string(model.to_string())

	# Initialize empty arrays
	facility_ids = []

	facility_classes = {
		0: "IfcFacility",
		1: "IfcRoad",
		2: "IfcBridge",
		3: "IfcRailway",
		4: "IfcMarineFacility"
	}

	# Create facilities
	for i, name in enumerate(names):
		facility = ifcopenshell.api.root.create_entity(model, ifc_class=facility_classes.get(types[i]), name=name)
		relating_object = model.by_id(relating_object_id)
		ifcopenshell.api.aggregate.assign_object(model, relating_object=relating_object, products=[facility])

		facility_ids.append(int(facility.id()))

	return model, facility_ids
