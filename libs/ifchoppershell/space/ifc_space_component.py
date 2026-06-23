import ifcopenshell

def ifc_space_component(
		model: ifcopenshell.file,
		relating_object_id: int,
		names: list[str] = ["Hopper Space"]
	) -> tuple[ifcopenshell.file, list[int]]:
	"""
	Creates IfcSpace components in the IFC model.

	Args:
		model (ifcopenshell.file): The IFC model.
		relating_object_id (int): The ID of the relating object.
		names (list[str]): A list of names for the spaces to create.

	Returns:
		tuple[ifcopenshell.file, list[int]]: A tuple containing the updated model and a list of the IDs of the created spaces.
	"""
    
	# Set default values
	if names == None:
		names = ["Hopper Space"]

	# Intialize model
	model = ifcopenshell.file.from_string(model.to_string())

	# Initialize empty arrays
	space_ids = []

	# Create spaces (one per name)
	for name in names:
		space = ifcopenshell.api.root.create_entity(model, ifc_class="IfcSpace", name=name)
		relating_object = model.by_id(relating_object_id)
		ifcopenshell.api.aggregate.assign_object(model, relating_object=relating_object, products=[space])

		space_ids.append(int(space.id()))

	return model, space_ids
