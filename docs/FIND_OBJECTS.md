# Find Objects filters and expressions

Find Objects is the plugin's general object-search component. It searches the model or an object's subtree. Deconstruct components expose relationships such as children, elements, parts and openings; Model Info summarises classes and the tree. Those components do not parse search selectors.

| Input | Matching and exclusion |
| --- | --- |
| Classes | IFC class names, including subclasses unless Exact Class is true. Prefix a class with `!` to exclude it. |
| Name | Case-insensitive .NET regex; use negative lookaheads for exclusion. |
| GlobalIds | Exact IFC GlobalIds or GUIDs, normalised to IFC IDs. Multiple entries mean any of those IDs; regex and `!` exclusions are not supported. |
| Properties | `Set.Property` for existence, or a comparison with `=`, `!=`, `<`, `<=`, `>` or `>=`. All supplied comparisons must match. Property names and text comparisons ignore case; regex is not supported. |

Every supplied input filter must match. Class/ID lists match any inclusion; exclusions take precedence.

## Class exclusions

Give Classes `IfcBuiltElement` and `!IfcWall` to find built elements except walls. Give only `!IfcWall` to search all object classes except walls, including spatial objects and openings. Multiple exclusions are allowed. Exclusions include subclasses by default: `!IfcBuiltElement` removes walls, slabs, doors and other built elements. With Exact Class true, only the literal IFC class is included or excluded. Exclusions do not prune descendants from the search.

Class selectors use IFC inheritance rather than regex on class-name strings. IDs also keep exact identifier semantics. For properties, `Pset_WallCommon.Status != DEMOLISHED` matches an existing property whose value differs from DEMOLISHED; it does not include objects missing that property. There is no general NOT/property-absence filter.

## Name expressions

Find Objects uses a case-insensitive .NET regular expression in **Name**. An empty or whitespace-only input accepts every name. Other filters (Classes, Exact Class, GlobalIds and Properties) must also match. Name matching does not load geometry.

| Name expression | Result |
| --- | --- |
| `wall` | Names containing wall, anywhere, ignoring case |
| `^Wall` | Names starting with Wall |
| `^Wall$` | Exactly Wall |
| `^(Wall|Slab)` | Names starting with Wall or Slab |
| `^(?!(Door|Window)$).*` | Every name except exactly Door or Window |
| `^(?!.*temporary).*wall` | Names containing wall but not temporary |
| `^Wall\[1\]$` | Exactly Wall[1]; brackets are escaped |

Exclusion only removes the matching object from the result. Its children, parts and openings are still searched. Result order, parents, paths, object identities and source links are preserved. Null names match as empty strings. Apart from blank input, spaces in the expression are significant.

Invalid expressions produce a Name error. Matching stops with an actionable error when a single name takes more than 100 ms; that search returns no partial results. Simplify the expression or restrict Classes/GlobalIds before retrying. This bounds matching time per object, not total search time or regex construction. See [Microsoft's regex timeout guidance](https://learn.microsoft.com/dotnet/standard/base-types/best-practices-regex#use-time-out-values).

## Beta compatibility change

Name previously used whole-name `*` and `?` wildcard matching. Existing definitions must update these inputs. Component GUIDs, input positions/types and outputs are unchanged.

| Previous wildcard | Regex equivalent |
| --- | --- |
| `*wall*` | `wall` |
| `South*` | `^South` |
| `?oor` | `^.oor$` |
| `Wall.1` | `^Wall\.1$` |

Use `.*` for any sequence and `.` for one character. Add `^` and `$` when preserving a whole-name match, and escape regex punctuation such as `.`, `(`, `[` and `+` when it is literal. There is no automatic conversion of saved expressions. Samples 13 and 15 use the new syntax; sample 15 demonstrates exclusion with a negative lookahead.
