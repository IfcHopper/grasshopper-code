import ifcopenshell
import Grasshopper.Kernel as gh

# Shortcut aliases for Grasshopper runtime message levels
e = gh.GH_RuntimeMessageLevel.Error
w = gh.GH_RuntimeMessageLevel.Warning

def ifc_facility_part_component(
		component: gh.GH_Component,
		model: ifcopenshell.file,
		relating_object_id: int,
		names: list[str] = ["Hopper Facility Part"],
		types: list[int] = [0]
	) -> tuple[ifcopenshell.file, list[int]]:
	"""
	Creates IfcFacilityPart components in the IFC model.

	Args:
		component (gh.GH_Component): The Grasshopper component.
		model (ifcopenshell.file): The IFC model.
		relating_object_id (int): The ID of the relating object.
		names (list[str]): A list of names for the facility parts to create.
		types (list[int]): A list of types for the facility parts to create (0: common part, 1: road part, 2: bridge part, 3: railway part, 4: marine part).

	Returns:
		tuple[ifcopenshell.file, list[int]]: A tuple containing the updated model and a list of the IDs of the created facility parts.
	"""

	# Set default values
	if names == None:
		names = ["Hopper Facility Part"]

	if types == None:
		types = [0] * len(names)

	elif len(types) != len(names):
		types = [0] * len(names)

		component.AddRuntimeMessage(w, "The length of 'Types' list should match the length of the 'Names' list. Defaulting all types to 0 (common part).")
	else:
		for i in range(len(types)):
			if types[i] not in [0, 1, 2, 3, 4]:
				types[i] = 0

				component.AddRuntimeMessage(w, f"Invalid type value at index {i}. Defaulting to 0 (common part).")

	# Intialize model
	model = ifcopenshell.file.from_string(model.to_string())

	# Initialize empty arrays
	facility_part_ids = []

	facility_classes = {
		0: "IfcFacilityPartCommon",
		1: "IfcRoadPart",
		2: "IfcBridgePart",
		3: "IfcRailwayPart",
		4: "IfcMarinePart"
	}

	# Create facility parts
	for i, name in enumerate(names):
		facility_part = ifcopenshell.api.root.create_entity(model, ifc_class=facility_classes.get(types[i]), name=name)
		relating_object = model.by_id(relating_object_id)
		ifcopenshell.api.aggregate.assign_object(model, relating_object=relating_object, products=[facility_part])

		facility_part_ids.append(int(facility_part.id()))

	return model, facility_part_ids
