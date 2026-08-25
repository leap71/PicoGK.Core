# PicoGK Coding Conventions

These conventions apply to the managed C# code in PicoGK. The declared C# type remains authoritative; prefixes provide compact semantic context at call sites and across the native boundary.

## Semantic prefixes

| Prefix | Meaning | Examples |
| --- | --- | --- |
| `b` | Boolean | `bSuccess`, `bIsEmpty` |
| `n` | Integral number, count, index, or quantity | `nCount`, `nIndex` |
| `f` | Floating-point value | `fRadiusMM`, `fTolerance` |
| `str` | String | `strName`, `strError` |
| `o` | Object or value without a more specific prefix | `oBoundingBox`, `oSlice` |
| `e` | Variable, parameter, property, or result of an enum type | `eAxis`, `eState` |
| `h` | Native or managed handle | `hNative`, `hMesh` |
| `p` | Native pointer used in interop code | `pValues`, `pVertices` |
| `lib` | `Library` | `lib`, `libGeometry` |
| `vox` | `Voxels` | `voxPart`, `voxResult` |
| `msh` | `Mesh` | `mshSurface`, `mshResult` |
| `tri` | `Triangle` | `triFace`, `triCurrent` |
| `quad` | `Quad` | `quadFace`, `quadCurrent` |
| `vec` | `Vector2` or `Vector3` | `vecPosition`, `vecDirection` |

`f` describes floating-point semantics, not a particular CLR type.

`lib` is an intentional exception to the general `o` rule. A `Library` is the owning PicoGK runtime context and appears frequently enough that `lib` is clearer than `oLibrary`.

## Collections

Prefix a collection with `a`, then compose it with the element prefix when useful:

- `aItems` for a general collection;
- `anIndices` for integral values;
- `avecVertices` for vectors.

The prefix describes the collection API, not its storage. Changing an array to a list does not require a rename.

## Fields and constants

Scope or storage modifiers precede the semantic prefix:

| Prefix | Meaning | Examples |
| --- | --- | --- |
| `m_` | Instance field | `m_nCount`, `m_hNative`, `m_avecVertices` |
| `s_` | Static field | `s_oOptions`, `s_aItems` |
| `c_` | Constant or constant-like shared value | `c_nLimit`, `c_fTolerance` |

Use `readonly` for dependencies and state that should not be reassigned after construction.

## Properties and methods

Property and method names carry the semantic prefix of their value or result.

Use a **property** for stored state or trivial, inexpensive, side-effect-free computation:

```csharp
public float fVoxelSizeMM { get; }

public float fDiameterMM =>
    2f * fRadiusMM;
```

Use a **method** for native queries, substantive computation, mutation, or other work that should remain visible to the caller:

```csharp
public bool bIsInside(Vector3 vecPoint)
{
    // Query the native geometry.
}

public Mesh mshBuild(Library lib)
{
    // Build and return a mesh.
}
```

Other result-prefix examples include `nVertexCount()`, `voxFromMesh()`, `vecSurfaceNormal()`, and `oCreateSlice()`.

A method returning `void` uses an unprefixed action name such as `Clear()`, `Offset()`, or `Dispose()`.

## Types and enums

Use semantic PascalCase names for classes, structs, and other concrete types. Interfaces use the conventional `I` prefix.

Enum types use the `E...` form. Enum members use unprefixed PascalCase because they are normally qualified by the enum type. For example, use `EState.Ready`, not `EState.eReady`:

```csharp
public enum EState
{
    Ready,
    Running,
    Complete
}
```

## Mathematical notation

Conventional short names may omit semantic prefixes when they materially improve a small, math-dense expression. For example, local Cartesian components may use `x`, `y`, and `z` instead of `fX`, `fY`, and `fZ`.

Likewise, `A`, `B`, `C`, and `D` are acceptable for canonical vertices or indices when expanded names would reduce clarity.

Keep these exceptions narrow. Use descriptive prefixed names whenever scope, role, or units would otherwise be unclear, especially at public API and serialization boundaries.

## General C# practices

- Enable nullable reference types.
- Validate arguments explicitly at public API boundaries.
- Document public contracts where behavior is not self-evident.
- Put braces on separate lines.
- Use one parameter per line for nontrivial signatures and calls.
