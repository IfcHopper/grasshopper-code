# IfcHopper

**IfcHopper** (formerly **IfcHopperShell**) is a Grasshopper plugin for Rhino 8 for openBIM: it creates, reads, updates and deletes (CRUD) data directly in native IFC files, without converting them to another format.

> **Status: 1.0.0-beta.2.** IfcHopper 1.0 is in public testing. Feedback is welcome (see [Contributing](#contributing)); component inputs and outputs may still change until 1.0.

## Features

- **Read** IFC2X3, IFC4 and IFC4X3 files, loading only what you look at, and explore them in Grasshopper: spatial tree, elements, geometry, types, materials, property and quantity sets, classifications, openings.
- **Create** new IFC models from Grasshopper: projects, sites, buildings and IFC 4.3 infrastructure facilities (bridges, roads, railways, marine facilities), storeys, facility parts, spaces and any IFC element class, with geometry, types (as Rhino blocks), materials, property sets and classifications.
- **Update** existing files in place: edit names, types, placements, geometry, properties, materials and more; everything IfcHopper does not touch stays as it was, matched by GlobalId.
- **Delete** objects with everything under them.
- **Write** IFC4X3_ADD2, IFC4 or IFC2X3, with clear warnings for what an older schema cannot hold.
- **Find** objects by class, name, GlobalId or property values, and summarise models.

What IFC 4.3 covers and IfcHopper does not yet is listed in [IFC 4.3 coverage](docs/DEVELOPMENT.md#ifc-43-coverage) and scheduled in the [Release plan](docs/DEVELOPMENT.md#release-plan).

## Technology

| Part | What |
| --- | --- |
| IfcHopper Core | IfcHopper's own IFC model, its rules and algorithms (`IfcHopper.Core`, no Rhino dependency) |
| IFC read/write | [xBIM Essentials](https://github.com/xBimTeam/XbimEssentials), native .NET |
| Geometry and Grasshopper | [RhinoCommon](https://developer.rhino3d.com/api/rhinocommon/) and the Grasshopper SDK |

The IFC library sits behind IfcHopper's own interfaces, so other backends ([IfcOpenShell](https://ifcopenshell.org/), [ifc-lite](https://github.com/louistrue/ifc-lite)) can be added later.

## Installation

Target: Rhino 8.20+ and .NET 8 on Windows/macOS. Beta.2 was checked on Windows x64 with Rhino 8.37; Rhino 8.20 and macOS remain unverified. The Yak package requires Rhino 8.27+. See [testing limits](docs/RELEASE_NOTES.md).

1. Download the latest zip from [Releases](https://github.com/IfcHopper/grasshopper-code/releases).
2. On Windows, unblock the zip (right-click > Properties > Unblock).
3. Unzip it into the Grasshopper Libraries folder (in Grasshopper: File > Special Folders > Components Folder).
4. Restart Rhino. The components are in the **IfcHopper** tab.

**Breaking change:** IfcHopper 1.0 is not compatible with IfcHopperShell 0.1. Definitions made with 0.1 do not work with 1.0 and have to be rebuilt with the 1.0 components.

## Documentation

- User documentation: https://ifchopper.github.io/grasshopper-documentation/
- [Samples](samples/README.md): one Grasshopper definition per workflow, with the IFC files they write.
- [Release notes](docs/RELEASE_NOTES.md): changes and testing limits for beta.2.
- [Compact inputs](docs/ADVANCED_INPUTS.md): use the bottom arrow to show advanced component inputs.
- [Packaging and publishing](tools/Package/README.md): local food4Rhino ZIP and Yak builds, release checks, and the two publishing routes. Generated packages remain ignored; building does not publish them.

## Roadmap and feature discussion

- [Release plan](docs/DEVELOPMENT.md#release-plan): what comes after 1.0, in order.
- Feature discussion: [IfcHopper Discussions](https://github.com/orgs/IfcHopper/discussions).

## Contributing

During the beta, the best way to help is to use IfcHopper on real models and tell us what happens:

- **Bugs:** open an [issue](https://github.com/IfcHopper/grasshopper-code/issues) with your Rhino version, the steps to reproduce, the message on the component and, if you can share it, the IFC file or a small definition.
- **Ideas and missing IFC features:** use [IfcHopper Discussions](https://github.com/orgs/IfcHopper/discussions), so others can join.
- **Code:** pull requests are welcome. For larger changes, open an issue or discussion first, so we can agree on the approach.

For code contributions:

- Read [AGENTS.md](AGENTS.md) for shared contributor and coding-agent guidance, code practices and GitHub workflow conventions.
- Read the [Developer reference](docs/DEVELOPMENT.md#developer-reference), especially [Architecture](docs/DEVELOPMENT.md#architecture) and [Compatibility after 1.0](docs/DEVELOPMENT.md#compatibility-after-10).
- Keep Grasshopper components to input, output and validation; put algorithms in `IfcHopper.Core`, with xUnit tests in `IfcHopper.Tests`.
- After changing component inputs or outputs, regenerate the samples (see the [samples README](samples/README.md)).
- Icons follow [docs/ICONS.md](docs/ICONS.md).

See [Building and contributing](docs/BUILDING.md) for build, test and offline template setup.

## Authors

IfcHopper is developed by **Mattia Bressanelli** and **Luca Florio**.

## Licence

IfcHopper is licensed under the [GNU LGPL-3.0](LICENSE).

Third-party components:

- [xBIM Essentials](https://github.com/xBimTeam/XbimEssentials), shipped as DLLs under its own licence (CDDL-1.0).
- IFC 4.3 property and quantity set templates (Annex A) by [buildingSMART International](https://www.buildingsmart.org/), embedded unchanged in `IfcHopper.Core`.

## Background

IfcHopper is the successor of **IfcHopperShell** ([v0.1 documentation](https://ifchopper.github.io/grasshopper-documentation/0.1/), [v0.1 code](https://github.com/IfcHopper/grasshopper-code/tree/v0.1.2)), a Python/IfcOpenShell based Grasshopper plugin. IfcHopper is a C# rewrite, renamed because it no longer builds on IfcOpenShell. The old code is a loose reference only; it is not ported. The main plugin icon is kept from IfcHopperShell (`icons/ICON.svg` and `misc/icon.png` in the old repo).
