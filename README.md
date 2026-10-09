# IfcHopper

**IfcHopper** (formerly **IfcHopperShell**) is a Grasshopper plugin for Rhino 8 for openBIM: it creates, reads, updates and deletes (CRUD) data directly in native IFC files, without converting them to another format.

> **Status: 1.0 beta.** IfcHopper 1.0 is in public testing. Feedback is welcome (see [Contributing](#contributing)); component inputs and outputs may still change until 1.0.

## Features

- **Read** IFC2X3, IFC4 and IFC4X3 files, loading only what you look at, and explore them in Grasshopper: spatial tree, elements, geometry, types, materials, property and quantity sets, classifications, openings.
- **Create** new IFC models from Grasshopper: projects, sites, buildings and IFC 4.3 infrastructure facilities (bridges, roads, railways, marine facilities), storeys, facility parts, spaces and any IFC element class, with geometry, types (as Rhino blocks), materials, property sets and classifications.
- **Update** existing files in place: edit names, types, placements, geometry, properties, materials and more; everything IfcHopper does not touch stays as it was, matched by GlobalId.
- **Delete** objects with everything under them.
- **Write** IFC4X3_ADD2, IFC4 or IFC2X3, with clear warnings for what an older schema cannot hold.
- **Find** objects by class, name, GlobalId or property values, and summarise models.

What IFC 4.3 covers and IfcHopper does not yet is listed in [IFC 4.3 coverage](#ifc-43-coverage) and scheduled in the [Release plan](#release-plan).

## Technology

| Part | What |
| --- | --- |
| IfcHopper Core | IfcHopper's own IFC model, its rules and algorithms (`IfcHopper.Core`, no Rhino dependency) |
| IFC read/write | [xBIM Essentials](https://github.com/xBimTeam/XbimEssentials), native .NET |
| Geometry and Grasshopper | [RhinoCommon](https://developer.rhino3d.com/api/rhinocommon/) and the Grasshopper SDK |

The IFC library sits behind IfcHopper's own interfaces, so other backends ([IfcOpenShell](https://ifcopenshell.org/), [ifc-lite](https://github.com/louistrue/ifc-lite)) can be added later.

## Installation

Requirements: Rhino 8.20 or later (Windows or Mac).

1. Download the latest zip from [Releases](https://github.com/IfcHopper/grasshopper-code/releases).
2. On Windows, unblock the zip (right-click > Properties > Unblock).
3. Unzip it into the Grasshopper Libraries folder (in Grasshopper: File > Special Folders > Components Folder).
4. Restart Rhino. The components are in the **IfcHopper** tab.

**Breaking change:** IfcHopper 1.0 is not compatible with IfcHopperShell 0.1. Definitions made with 0.1 do not work with 1.0 and have to be rebuilt with the 1.0 components.

## Documentation

- User documentation: https://ifchopper.github.io/grasshopper-documentation/
- [Samples](samples/README.md): one Grasshopper definition per workflow, with the IFC files they write.

## Roadmap and feature discussion

- [Release plan](#release-plan): what comes after 1.0, in order.
- Feature discussion: _link coming soon_.

## Contributing

During the beta, the best way to help is to use IfcHopper on real models and tell us what happens:

- **Bugs:** open an [issue](https://github.com/IfcHopper/grasshopper-code/issues) with your Rhino version, the steps to reproduce, the message on the component and, if you can share it, the IFC file or a small definition.
- **Ideas and missing IFC features:** use the feature discussion, so others can join.
- **Code:** pull requests are welcome. For larger changes, open an issue or discussion first, so we can agree on the approach.

For code contributions:

- Read the [Developer reference](#developer-reference) below, especially [Architecture](#architecture) and [Compatibility after 1.0](#compatibility-after-10).
- Keep Grasshopper components to input, output and validation; put algorithms in `IfcHopper.Core`, with xUnit tests in `IfcHopper.Tests`.
- After changing component inputs or outputs, regenerate the samples (see the [samples README](samples/README.md)).
- Icons follow [docs/ICONS.md](docs/ICONS.md).

Build and test (needs the .NET 8 SDK):

```sh
dotnet build IfcHopper.sln
dotnet test IfcHopper.sln
```

The build produces `IfcHopper.gha` in `IfcHopper/bin/<Configuration>/net8.0/`; add that folder to the Grasshopper library paths to load it. The first build downloads the IFC 4.3 property set templates from buildingSMART (see [Reference](#reference-ifc-43-add2-specification)).

Contributions are licensed under the same licence as the project.

## Authors

IfcHopper is developed by **Mattia Bressanelli** and **Luca Florio**.

## Licence

IfcHopper is licensed under the [GNU LGPL-3.0](LICENSE).

Third-party components:

- [xBIM Essentials](https://github.com/xBimTeam/XbimEssentials), shipped as DLLs under its own licence (CDDL-1.0).
- IFC 4.3 property and quantity set templates (Annex A) by [buildingSMART International](https://www.buildingsmart.org/), embedded unchanged in `IfcHopper.Core`.

## Background

IfcHopper is the successor of **IfcHopperShell** ([v0.1 documentation](https://ifchopper.github.io/grasshopper-documentation/0.1/), [v0.1 code](https://github.com/IfcHopper/grasshopper-code/tree/v0.1.2)), a Python/IfcOpenShell based Grasshopper plugin. IfcHopper is a C# rewrite, renamed because it no longer builds on IfcOpenShell. The old code is a loose reference only; it is not ported. The main plugin icon is kept from IfcHopperShell (`icons/ICON.svg` and `misc/icon.png` in the old repo).

---

# Developer reference

How the code is organised and what it implements.

## Architecture

Solution layout:

- `IfcHopper.Core/` – class library (`IfcHopper.Core.dll`) with no Grasshopper or Rhino dependency: IFC CRUD, conversions, utilities.
  - `Model/` – IfcHopper's own model: Model > Project > a tree of Sites, Facilities (Building, Road, Bridge, Railway, Marine Facility, Facility), Storeys, Facility Parts, Spaces and Elements. Every object keeps one `Children` list (for elements: their parts); typed views such as `Site.Facilities` or `Storey.Elements` filter it. `SpatialRules` holds which children each object accepts, following IFC: Project > Site or Facility; Site > Site, Facility, Space or Element; Facility > Facility of its kind, Facility Part of its kind or common, Space or Element (buildings also Storey); Storey > Storey, Space or Element; Facility Part > Facility Part of its kind or common, Space or Element; Space > Space or Element; Element > Element. Independent of any IFC library; built just before writing. Lengths are in metres; components convert from Rhino document units.
  - `Geometry/` – backend-neutral geometry algorithms (polygon triangulation, affine transforms, alignment curves).
  - `IO/` – backend-neutral contracts (`IIfcReader`, `IIfcWriter`, `IfcHeader`), the active backend (`IfcBackend`) and file path rules.
  - `Backends/Xbim/` – xBIM implementation. The only place that references xBIM.
- `IfcHopper/` – the Grasshopper plugin (`IfcHopper.gha`), referencing `IfcHopper.Core`.
  - `Components/` – Grasshopper components. Input/output and validation only; delegate work to `IfcHopper.Core`. One subfolder per toolbar panel: `File/`, `Project/`, `Facility/`, `FacilityPart/`, `Object/`, `Utilities/`.
  - `Types/` – Grasshopper goo/param types wrapping IFC entities.
- `IfcHopper.Tests/` – xUnit tests for `IfcHopper.Core`. Tests using `SampleModels/` are skipped when the folder is missing.
- Rhino-dependent logic (e.g. geometry conversion) goes in the plugin or a separate Rhino-dependent library, never in `IfcHopper.Core`.
- Other folders only when a concern doesn't fit the above.

### Toolbar layout

Planned components per panel (not all implemented yet). In each panel, create components go in the primary row, then deconstruct components, then modify components.

| Panel | Primary | Secondary | Tertiary | Quaternary |
| --- | --- | --- | --- | --- |
| 1 - File | Read IFC, Read IFC Header, Write IFC | Model, Deconstruct Model, Apply Edits | | |
| 2 - Project | Project, Site | Context, Units, Georeference | Deconstruct Project, Deconstruct Site, Deconstruct Context, Deconstruct Units, Deconstruct Georeference | Modify Project, Modify Site |
| 3 - Facility | Building, Bridge, Road, Railway, Marine Facility, Facility | Deconstruct Facility | Modify Facility | |
| 4 - Facility Part | Storey, Bridge Part, Road Part, Railway Part, Marine Part, Facility Part, Space | Deconstruct Storey, Deconstruct Facility Part, Deconstruct Space | Modify Storey, Modify Facility Part, Modify Space | |
| 5 - Object | Object, Opening, Element Type, Material, Layer Set, Constituent Set, Pset, Qto, Classification | Deconstruct Object, Deconstruct Element Type, Deconstruct Material, Deconstruct Pset, Deconstruct Qto, Deconstruct Classification | Modify Object, Modify Element Type, Modify Material | |
| 6 - Utilities | Find Objects, Model Info | | | |
| 7 - Params | Model, Project, Site, Facility, Facility Part, Storey, Space | Context, Units, Georeference (internalisable), Element, Element Type, Material, Property Set, Classification | | |

### Roadmap: IFC backends

xBIM is the first and, for now, only IFC backend because it runs natively in .NET. Support for [IfcOpenShell](https://ifcopenshell.org/) and [ifc-lite](https://github.com/louistrue/ifc-lite) is a planned goal; both are not C# native, so they are deferred. Keep backend-specific code isolated in `Core` behind backend-neutral abstractions so additional backends can be plugged in later without changing components or types.

### Data flow

- **Create:** components build an IfcHopper model (element > storey/part > facility > site > project > model) just before writing; the backend writes it as a new file in the schema chosen on Write IFC (IFC4X3_ADD2 by default, IFC4 or IFC2X3). Spatial children and element parts are written with IfcRelAggregates, elements in spatial objects with IfcRelContainedInSpatialStructure; a child its parent does not accept is an error.
- **Read (lazy):** the backend opens the file once and caches it (reopened only when the file changes). Only the project is converted; the children of an object (aggregated sites, facilities, storeys, parts, spaces and element parts, then contained elements) are converted the first time they are asked for. An element that is a part of another element is listed only under that element. Other entities (e.g. alignments) are not mapped and are kept when writing back. Every read object keeps a link to its source entity and its GlobalId.
- **Editing:** components never change their inputs. Modify components output an edited copy that keeps the GlobalId and source; unconnected inputs keep their value, and a connected child list replaces the children. Apply Edits puts edited objects back into a read model by GlobalId, loading and copying only the branches that lead to them.
- **Write back (in place):** a model whose project was read from a file is written by opening a fresh copy of the source file and applying only the differences, matched by GlobalId: changed names, descriptions, types, usage, placements and elevations are updated; new children are created; children left out of a replaced list are deleted with everything under them. Child lists that were never loaded are unchanged. Not supported in place (clear error): changing units, changing or removing contexts, changing an object's class, moving an object to another parent.
- **Schemas:** Write IFC takes the schema to write: IFC4X3_ADD2 (default for new models), IFC4 or IFC2X3. A model read from a file is written back in place in its own schema unless another one is chosen; it is then rebuilt as a new file from the IfcHopper model, with a warning that data IfcHopper does not model is not carried over. Entities are created through the xBIM IFC4 interfaces in the target schema, and what the schema lacks is downgraded and listed as a warning on the component: IFC4X3 facilities as IfcBuilding and facility parts as IfcBuildingStorey (class and type in ObjectType, part usage lost), element classes the schema lacks as IfcBuildingElementProxy (class and type in ObjectType), predefined types the entity lacks as ObjectType, value types the schema lacks as IfcLabel, IfcReal, IfcInteger, IfcBoolean or IfcLogical, number quantities as counts. IFC2X3 additionally gets meshes as IfcFacetedBrep (IfcFaceBasedSurfaceModel when open), transparent colours as rendering styles, the georeference as the ePSet_MapConversion and ePSet_ProjectedCRS property sets of the project (also read back), an IfcOwnerHistory for new entities, and loses quantity formulas. On read, entities without a PredefinedType in their schema take their type from ObjectType.
- **Units:** the Core model stores lengths in metres. Components convert from and to Rhino document units, and the writer converts to the file units (project Units, defaulting to the Rhino length unit with m², m³ and radians). On read, a file in different units is converted automatically; Read IFC shows a remark and offers "Match Rhino units to file" in its context menu.
- **GlobalIds:** create components take an optional GlobalId; when empty, the id is derived from the component instance and data iteration, so writing again keeps the same ids. Relationship ids are derived from their parent. Read objects keep the ids from the file. A GlobalId used twice in a model is an error.
- **Placements:** Site, facilities, parts, spaces, objects and openings take an optional world Placement plane; empty means the parent placement. The Placement inputs have their own parameter type (`PlacementParam`, hidden from the toolbar) that takes planes and anything that converts to one, so linear placements can later use the same inputs. The Core model stores world placements in metres; the writer writes IFC local placements relative to the parent and the reader composes them back to world. Storeys are placed at their elevation in the building. Deconstruct components output world planes.
- **Geometry:** element geometry is meshes only. The Core model stores meshes in world coordinates (metres); the writer writes each as an `IfcPolygonalFaceSet` in a `Body` representation relative to the element placement, adding a `Body` subcontext to the 3D model context when missing. The Object component keeps Rhino meshes and facets Breps, extrusions, surfaces and SubDs. On read, the Body representation is faceted in Core when a deconstruct component asks for it: face sets, faceted Breps, surface models, extruded area solids (arbitrary, rectangle, circle, ellipse, I, L, T, U, C, derived and composite profiles; profile curves of polylines, indexed poly curves, circles, ellipses and trimmed lines, circles and ellipses; fillet radii ignored), half space clippings (`IfcBooleanClippingResult` with plain `IfcHalfSpaceSolid`s, cut faces capped), mapped items and `IfcSectionedSolidHorizontal` (profiles across the horizontal tangent, profile X to the left and Y up, interpolated between cross sections; directrix an alignment curve or a polyline). Closed meshes are turned to face outwards. Other items (e.g. other booleans, polygonal bounded half spaces, revolved or swept solids, advanced Breps, alignment-based solids) are reported as skipped. Modify Object replaces the geometry, or moves it with a new placement. On write back, loaded geometry is compared with the file relative to the element placement: unchanged geometry (also when moved with its element) keeps the original items, changed geometry replaces the Body representation with face sets. Each mesh may have a surface colour, written as an `IfcStyledItem` with an `IfcSurfaceStyleShading` (one style per colour) and read from the item's style or, for mapped items, the style of the mapped item; meshes without a style are shown (preview, bake, Deconstruct Object) in the colour of the element's material without storing it on the mesh. A changed colour replaces the body on write back. Element params preview their geometry, including the geometry of their parts, in the viewport in its colours and bake one mesh per element (with its parts), named after it, with IfcClass and GlobalId as user text.
- **Materials:** an element's material (IfcRelAssociatesMaterial), or its type's when it has none, is read on first access as a single material (name, description, category, colour from its IfcMaterialDefinitionRepresentation style), a layer set (layers with thickness in metres), a constituent set (IFC2X3 material lists are read as constituent sets without names) or a profile set (profile shapes not modelled). Layer and profile set usages give their set; how the layers sit on the element is kept in the file and not modelled. Each IFC material or set is converted once, so elements sharing it share one Core object. Deconstruct Object outputs the material; Deconstruct Material lists the parts of a set. The Material, Layer Set and Constituent Set components create materials (layer thicknesses in document units); the Material input of Object and Element Type assigns one (an object without its own shows its type's). On write, each definition is written once; a single material reuses an existing IfcMaterial with the same name, description, category and colour, so equal materials made by different components are one IfcMaterial; colours are written as an IfcMaterialDefinitionRepresentation style. Elements take layer sets through an IfcMaterialLayerSetUsage with default values (layers along the thickness of slabs, plates, coverings and roofs, across that of other elements; positive sense, no offset), types take the set itself; one IfcRelAssociatesMaterial per material. IFC2X3 has no descriptions or categories, and writes constituent sets as IfcMaterialList; profile sets are not written (their profiles are not modelled). Written in place: materials read from the file are matched by their entity (materials have no GlobalId); Modify Object's Material input (or Element Type's for types) moves an object to another material, leaving its old relationship (deleted when empty, with its usage when unused); Modify Material outputs an edited copy which, passed to Apply Edits or reached as the material of a written object or edited type, is written onto the file's material, so it changes for every element using it (name, description, category and colour of materials; name and description of sets). Materials left without elements stay in the file, like types. Materials and sets keep property sets (`IfcMaterialProperties`, IFC4 and later; e.g. Pset_MaterialCommon) from the Property Sets input of Material and Modify Material (merged by name; a set without properties removes it), output by Deconstruct Material; an existing material is reused only with the same property sets; edited sets replace the material's property sets in place. IFC2X3 only has typed material properties, which are neither read nor written. On Modify Object and Modify Element Type, a connected but empty Material or Element Type input removes the material or type.
- **Tag and attributes:** elements and types keep their Tag and the attributes of their class beyond those IfcHopper models, when the value is text, a number, a boolean or an enumeration (e.g. OverallHeight, OverallWidth and OperationType of IfcDoor; listed from the IFC4X3 class of the same name; lengths, areas and volumes in metres, m² and m³). Object, Element Type and their Modify components take them by name (Tag, Attribute Names and Attribute Values inputs, last; names are checked against the class, values converted; empty values remove an attribute on Modify); the Deconstruct components output them. On write each attribute is set on the class in the target schema; attributes or enumeration values the schema lacks (e.g. IfcDoor.OperationType in IFC2X3) are left out with a warning. In place, changed and removed attributes are written; mandatory attributes cannot be removed.
- **Types:** an element's type (IfcRelDefinesByType to an `IfcTypeProduct`, e.g. `IfcWallType` or an IFC2X3 `IfcDoorStyle`) is read on first access with its class, predefined type, own property sets, material and the Body geometry of its representation maps, in type coordinates (metres). Each type is converted once, so its occurrences share one Core object. When an element's Body maps each Body map of its type once, with the same target and no style of its own, the element keeps that transform (`TypeTransform`, type coordinates to world): it is an instance of its type, like a Rhino block instance. Moving the element moves the transform; replacing or recolouring its geometry drops it. Deconstruct Object outputs the type; Deconstruct Element Type outputs its geometry in type coordinates. Element params bake instances of a type as Rhino block instances (context menu "Bake as blocks", on by default; elements with parts bake as meshes): one block definition per type, named after it, with IfcClass and GlobalId as user text; an existing definition with the same GlobalId is reused, and a name already taken by another block gets a number. The Element Type component creates a type (class given as type or element class) from geometry or a block definition; the Object component takes it as Element Type: without geometry the object is an instance of the type at its placement, with geometry it keeps its own and is only typed. A single block instance as Object geometry makes the object an instance of the block's type (class derived from the object class, e.g. IfcWall: IfcWallType), placed at the insertion plane unless a placement is given; other block instances are exploded. Types made from a block take the GlobalId baked on it, else one derived from the definition, so all objects and Element Type components using one block write one type. On write, each type (by GlobalId) is written once with its own property sets and one Body representation map; instances get an `IfcMappedItem` with an `IfcCartesianTransformationOperator3D` relative to the element placement (non-uniform scales as `…3DnonUniform`; shears cannot be mapped and are written as own geometry with a warning), and each type gets one IfcRelDefinesByType. IFC2X3 writes door and window types as styles; type classes a schema lacks become `IfcBuildingElementProxyType` with the class in ElementType. Written in place, new elements use the file's type with the same GlobalId and join its relationship; deleting elements keeps their types. Modify Element Type outputs an edited copy of a type (same GlobalId); passed to Apply Edits, or reached as the type of a written object, an edited type read from the file is written onto the file's type, so it changes for all its occurrences like a block definition: name, description, predefined type, its own property sets (in HasPropertySets, edited like object sets) and, when changed, its geometry, put in its first Body representation map (further Body maps are deleted with the mapped items using them), so every instance shows the new geometry. Modify Object's Element Type input changes an object's type: an instance becomes an instance of the new type in the same place, like replacing a block; other objects keep their geometry. In place, the object leaves the old type's relationship (deleted when empty) and joins the new type's. Changing the class of a type in place is not supported.
- **Property and quantity sets:** every object (project, sites, facilities, parts, storeys, spaces, elements) reads its sets (IfcRelDefinesByProperties) on first access, then the sets of its type unless it has its own set with the same name (marked From Type, never written). Property sets keep single, enumerated, list, bounded (lower, upper and set point), table (defining and defined values) and complex (nested) properties; reference properties are read as text and kept in the file; quantity sets keep length, area, volume, count, weight, time and number quantities. Lengths, areas and volumes are stored in metres, m² and m³ and shown in document units; other measures are in SI units (weights in kg). The Pset component takes the value type of each property from the standard set template (IFC 4.3 Annex A, embedded in `IfcHopper.Core`, see [Reference](#reference-ifc-43-add2-specification)), else from the Types input, else from the value (IfcBoolean, IfcInteger, IfcReal, IfcLabel), and warns about properties or enumeration values the template does not have; the Qto component takes quantity kinds the same way. Create components add sets; Modify components put a set in place of the one with the same name (a set without properties deletes it). On write back, sets are matched by name: unchanged sets are kept, a changed set is edited in place (unchanged properties keep their entities) or, when other objects or types share it, replaced by a copy for this object only; removed sets are detached and deleted when nothing else uses them. All kinds but reference properties can be written: the Pset component takes a Kinds input (else the kind follows the value: a domain is bounded, a Pset is complex; else the standard set), and text gives lists ("a; b"), bounds ("lower .. upper") and tables ("defining: defined; …"). IFC2X3 has no set points.
- **Classification:** every object (project, sites, facilities, parts, storeys, spaces, elements) and type reads its classification references (IfcRelAssociatesClassification) on first access, then those of its type in systems it has no reference in (marked From Type, never written). A reference has the system (name of the `IfcClassification` at the top of its parent references) and edition, the code (Identification; ItemReference in IFC2X3), name and location; an association with a whole classification is read as a reference without code. Each IFC reference is converted once, so objects sharing it share one Core object. The Classification component creates a reference; the Classifications input (after Property Sets on create and Modify components) puts each reference in place of the references in the same system, and a reference without a code removes them; the Deconstruct components output the references, Deconstruct Classification their values. On write, each reference is written once per value (an existing one with the same values is reused) in one `IfcClassification` per system and edition, with one IfcRelAssociatesClassification per reference. IFC2X3 gets the mandatory source and edition as empty labels and cannot associate whole classifications (left out with a warning). In place, references are matched by value: removed ones leave their relationship (deleted when empty), new ones join the relationship of an existing equal reference; references and classifications stay in the file, like a library.
- **Openings:** an element reads the openings and recesses voiding it (IfcRelVoidsElement to an `IfcOpeningElement` or another `IfcFeatureElementSubtraction`) on first access, as objects of their own with placement, Body geometry, property sets and classifications; an opening reads the elements filling it (IfcRelFillsElement, e.g. doors and windows) as separate objects with the GlobalId of the element in the spatial structure. Openings voiding an element are not listed as children of spatial objects. The element's body stays uncut, as IFC stores it; the plugin subtracts the openings with Rhino mesh booleans for preview, bake and the Geometry output of Deconstruct Object (a mesh whose boolean fails stays uncut, with a warning on Deconstruct Object), which also outputs the openings and, for an opening, its fills. Elements with openings bake as meshes, not as block instances. The Opening component creates an opening (IfcOpeningElement; type OPENING or RECESS, geometry, the elements filling it, property sets, classifications, an optional world placement defaulting to its host's); the Openings input of Object (after Parts) gives an object its openings, and that of Modify Object replaces them. On write, openings are written with their host, placed relative to it, and void it through IfcRelVoidsElement; each fill is related through IfcRelFillsElement to the element with its GlobalId written elsewhere in the model (or, in place, already in the file), so doors and windows must also be in a storey or space: fills outside the model are left out with a warning. IFC2X3 openings keep their type in ObjectType. Moving an object with Modify Object moves its openings with it. In place, openings are matched by GlobalId and edited like objects (placed relative to their host), removed ones are deleted and new ones written; fills are matched by GlobalId. When its host moves, an opening the file places relative to something else (e.g. the storey) is placed relative to the host. Deleting an element deletes its openings; relationships left without an opening, host or fill are deleted.
- **Utilities:** Find Objects searches a model, or below any object, depth first through spatial objects, elements, element parts and openings, loading objects read from a file as it reaches them (not their geometry). It matches IFC classes with their subclasses (e.g. IfcBuiltElement; Exact Class for the class only), name patterns with * and ?, GlobalIds or GUIDs, and property filters `Set.Property` (exists) or `Set.Property op value` with =, !=, <, <=, >, >= (numbers compared numerically, lengths, areas and volumes in document units; text and booleans ignoring case; sets of the type count). It outputs the objects, the elements among them (previewed), their parents, paths and classes; found objects keep their GlobalId and source, so Modify components and Apply Edits work on them. Model Info gives the schema of the source file, the units, the number of objects per class and the spatial tree as text (elements counted per class, or listed).
- **Alignments (IFC4X3):** `IfcLinearPlacement` and sectioned solids are evaluated on alignment curves: `IfcGradientCurve` (heights by horizontal distance), `IfcSegmentedReferenceCurve` (cant ignored) and `IfcCompositeCurve` of `IfcCurveSegment` with line, circle, clothoid and polynomial parent curves. Each segment is its parent curve from SegmentStart, moved so its start point and tangent match the segment placement. Distances along are measured on the horizontal curve; linear placement axes default to the horizontal tangent (X), left (Y) and up (Z). Other spirals (cosine, sine, polynomial spirals) are reported as skipped.
- **Georeference:** the project's map conversion (IfcMapConversion to an IfcProjectedCRS) is attached to the 3D model context. Eastings, northings and height are in metres; the IFC scale includes the file length unit (e.g. 0.001 for a millimetre model).

## IFC 4.3 coverage

Gap analysis against IFC 4.3 ADD2 (876 entities, 437 types, 51 relationships, 645 Psets, 115 Qto sets). Measured by concept, not entity count. See [Data flow](#data-flow) for details of what is implemented.

| Area | Implemented | Not implemented |
| --- | --- | --- |
| File | Read IFC2X3, IFC4, IFC4X3; header; write IFC4X3_ADD2, IFC4 or IFC2X3 with downgrades and warnings; in-place write back by GlobalId; lazy loading | Changing class, parent, units or contexts in place; schema (WHERE rule) or IDS validation |
| Project | Name, description, contexts and subcontexts, map conversion with projected CRS | Project library, documents, actors, owner history editing, approvals, constraints |
| Units | Length, area, volume, angle (other measures in properties and quantities are taken as SI) | Time, mass, force, pressure, temperature, derived units, currency |
| Spatial structure | Site, Building, Bridge, Road, Railway, Marine Facility, Facility, Storey, Facility Parts, Space; IFC aggregation rules; containment | `IfcSpatialZone`, `IfcExternalSpatialElement`, `IfcZone`; site reference latitude/longitude/elevation, land title, postal addresses |
| Elements | Any instantiable `IfcElement` subclass, PredefinedType, ObjectType, element parts (aggregation); type objects (`IfcElementType`, `IfcRelDefinesByType`) with representation maps and mapped instances, read, written and edited in place, linked to Rhino blocks; Tag and the simple class-specific attributes (e.g. door height, width and operation) | nesting (`IfcRelNests`); class-specific attributes holding entities or lists |
| Properties | Property sets and quantity sets (`IfcRelDefinesByProperties`) on every object, type sets inherited; single, enumerated, list, bounded, table and complex values read and written (reference values read); all simple quantity kinds; Annex A templates for value types and checks; in-place edits | Authoring reference properties; property units (`IfcPropertySingleValue.Unit`), quantity units other than the project units; `IfcPropertySetTemplate`; predefined property sets (e.g. door lining properties) |
| Materials | Single material, layer set, constituent set: read, created, assigned to elements and types, edited in place; material property sets (`IfcMaterialProperties`); profile sets read | Writing profile sets; layer and profile set usages (direction, offset, cardinal point) as editable data; changing the layers or constituents of a set in place; profile shapes; typed IFC2X3 material properties |
| Classification | Classification references (`IfcClassificationReference` in an `IfcClassification`, `IfcRelAssociatesClassification`) on every object and type, type references inherited: read, written and edited in place; nested references read with the system at the top | Authoring reference hierarchies; classifying materials and property sets; classification edition dates, sources and reference tokens; bSDD lookup |
| Openings | Openings and recesses voiding elements (`IfcRelVoidsElement`) and the elements filling them (`IfcRelFillsElement`), read; subtracted from host geometry in the plugin (preview, bake, Deconstruct Object); created, written and edited in place | Projection elements (`IfcRelProjectsElement`) |
| Placement | Local placements; `IfcLinearPlacement` on read | Writing linear placements; grids (`IfcGrid`) |
| Geometry read | Face sets, faceted Breps, surface models, extrusions (most parametric profiles), half space clippings, mapped items, `IfcSectionedSolidHorizontal` | Other booleans, polygonal bounded half spaces, revolved and swept solids, advanced Breps and NURBS, `IfcSectionedSolid`/`IfcSectionedSurface`, alignment-based solids, profile fillet radii |
| Geometry write | Meshes as `IfcPolygonalFaceSet` in a Body representation | Extrusions, swept solids, Breps or NURBS; representations other than Body (Axis, FootPrint, Box, Annotation, Clearance), 2D plan geometry |
| Presentation | Surface colour (`IfcSurfaceStyleShading`) | Transparency, rendering styles, textures, curve and fill styles, presentation layers |
| Alignment | Evaluated for placements and sectioned solids: line, circle, clothoid and polynomial segments; gradient and segmented reference curves | Alignment as objects (`IfcAlignment` horizontal, vertical, cant) to read or author; layout segments; cant; cosine, sine and polynomial spirals; referents and stationing (`IfcReferent`, `IfcRelPositions`) |
| Infrastructure domains | Facilities and parts; their elements (e.g. `IfcCourse`, `IfcKerb`, `IfcBorehole`, `IfcEarthworksCut`) as generic elements with mesh geometry | Domain-specific attributes and geotechnical models beyond generic elements |
| Groups and systems | – | `IfcGroup`, `IfcSystem`, `IfcDistributionSystem`, `IfcBuildingSystem`, `IfcRelAssignsToGroup`, `IfcRelServicesBuildings` |
| Connectivity | – | `IfcRelConnectsElements`, `IfcRelConnectsPathElements`, `IfcRelSpaceBoundary`, ports (`IfcDistributionPort`, `IfcRelConnectsPorts`) |
| Process, cost, structural | – | Tasks and schedules (4D), cost items (5D), resources, structural analysis models and loads |

The gaps are scheduled in the [Release plan](#release-plan).

## Release plan

**1.0** is the current feature set (see [Data flow](#data-flow) and [IFC 4.3 coverage](#ifc-43-coverage)). Later releases close the gaps in this order, most important for IFC 4.3 first:

| Release | Theme | Scope |
| --- | --- | --- |
| 1.0 | Current release | Everything listed as implemented in [IFC 4.3 coverage](#ifc-43-coverage) |
| 1.1 | Alignment and linear referencing | `IfcAlignment` as objects (horizontal, vertical, cant layouts and segments, `IfcRelNests`), read and authored from Rhino curves; writing `IfcLinearPlacement`; referents and stationing (`IfcReferent`, `IfcRelPositions`); cant; cosine, sine and polynomial spirals |
| 1.2 | Parametric geometry write | Extrusions and swept solids (e.g. cross sections along an alignment, `IfcSectionedSolidHorizontal`); Axis, FootPrint and Box representations |
| 1.3 | Geometry read and presentation | Other booleans, polygonal bounded half spaces, revolved and swept solids, advanced Breps and NURBS, `IfcSectionedSolid`/`IfcSectionedSurface`, alignment-based solids, profile fillet radii; transparency, presentation layers, curve and fill styles |
| 1.4 | Groups, systems and connectivity | `IfcGroup`, `IfcZone`, `IfcSystem`, `IfcDistributionSystem`, `IfcBuildingSystem` (`IfcRelAssignsToGroup`, `IfcRelServicesBuildings`); `IfcRelConnectsElements`, `IfcRelConnectsPathElements`, `IfcRelSpaceBoundary`, ports; `IfcRelProjectsElement`, `IfcRelInterferesElements`, `IfcRelAdheresToElement` |
| 1.5 | Validation | IDS checking as a Utilities component; schema WHERE rules; Reference View and Alignment-based Reference View checks |
| 1.6 | Spatial elements and project data | `IfcSpatialZone`, `IfcExternalSpatialElement`, `IfcGrid`; site latitude, longitude, elevation, land title and addresses; documents, actors, approvals, constraints, project libraries, owner history; time, mass, force, pressure, temperature, derived units and currency; property units |
| 1.7 | Deeper data | Authoring reference properties, `IfcPropertySetTemplate`, predefined property sets; writing profile sets, editable layer and profile set usages, changing set layers in place; classifying materials, reference hierarchies, edition dates and sources, bSDD lookup; entity- and list-valued class attributes; changing class, parent, units or contexts in place |
| 2.0 | Domains and backends | Infrastructure domain attributes and geotechnical models; 4D tasks and schedules, 5D costs and resources, structural analysis; IfcOpenShell and ifc-lite backends (see [Roadmap: IFC backends](#roadmap-ifc-backends)) |

The order can change with user needs; a release ships when its scope is implemented, tested and has a sample definition.

### Compatibility after 1.0

Saved Grasshopper definitions restore wires by parameter position and internalised values by parameter type, so from 1.0 on:

- New inputs and outputs are added only at the end of a component; existing ones are never inserted, reordered, removed or renamed, and never change type.
- A change that cannot follow this rule gets a new component with a new GUID. The old one stays, hidden (`GH_Exposure.hidden`), with an `IGH_UpgradeObject` that replaces it in open definitions.
- Placement inputs take placement values (`PlacementParam`): linear placements (1.1) are added to them. Deconstruct components keep the world Placement plane and get a Linear Placement output.
- The Geometry outputs of Deconstruct Object and Deconstruct Element Type stay meshes (with openings subtracted); parametric geometry (1.2, 1.3) gets new outputs, e.g. Solids. Writing parametric geometry from the Geometry inputs is a per-component context menu option, not a new input.
- Groups, systems, zones, connections and documents (1.4, 1.6) are components of their own that take the objects they relate, so object components do not change. Alignments (1.1) get their own parameter and an Alignments input and output at the end of Project, Modify Project and Deconstruct Project (IFC 4.3 aggregates them to the project); they are not elements.

## Samples

[samples/](samples/README.md) holds one Grasshopper definition per workflow (first building, reading, units and georeference, infrastructure, spaces, geometry, types and blocks, materials, property sets, classification, openings, attributes, editing in place, schema export, finding objects), with the IFC files they write. They are generated and checked by `tools/SampleBuilder`, which runs Rhino 8 headless; see the samples README to regenerate them.

## Reference: IFC 4.3 ADD2 specification

The [IFC 4.3 ADD2 specification](https://standards.buildingsmart.org/IFC/DEV/IFC4_3/HTML/content/introduction.html) by buildingSMART is the source of truth for entity, type, attribute and property set definitions.

The standard property and quantity set templates (Annex A) are embedded in `IfcHopper.Core`. They are owned by buildingSMART and not in this repository: the first build downloads them from [buildingSMART](https://standards.buildingsmart.org/IFC/DEV/IFC4_3/HTML/annex-a-psd.zip) into `IfcHopper.Core/obj/annex-a-psd/`.

## Icons

Icons are SVG + 24×24 PNG pairs embedded through `Properties/Resources.resx`. See [docs/ICONS.md](docs/ICONS.md) for conventions and the style reference.
