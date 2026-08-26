# C++ Coding Conventions

These conventions apply to first-party C++ code under `native/`. They do not apply to vendored dependencies under `third_party/`. Declared types remain authoritative; prefixes provide compact semantic context at call sites and across the C ABI.

## Semantic prefixes

| Prefix | Meaning | Examples |
| --- | --- | --- |
| `b` | Boolean | `bValid`, `bIsEmpty` |
| `n` | Integral number, count, index, or quantity | `nCount`, `nIndex` |
| `f` | Floating-point value | `fRadiusMM`, `fTolerance` |
| `str` | String | `strName`, `strError` |
| `o` | Object or value without a more specific prefix | `oBBox`, `oSlice` |
| `e` | Enum value | `eState`, `eAxis` |
| `h` | Opaque handle | `hInstance`, `hMesh` |
| `p` | Raw pointer | `pValues`, `pVertices` |
| `ro` | Smart pointer, regardless of ownership model | `roGrid`, `roImpl` |
| `vec` | Vector value | `vecPosition`, `vecDirection` |

`ro` applies to `std::unique_ptr`, `std::shared_ptr`, and library smart-pointer aliases. Do not use `po` as a competing smart-pointer prefix.

Raw pointers compose `p` with the pointee prefix when useful. For example, `poView` means “raw pointer to an object,” while `pvecNormal`, `pnCount`, and `pfDistanceMM` point to vector, integral, and floating-point values respectively. Thus `poView` remains valid and does not conflict with `ro`.

## Collections

Prefix collections with `a`, composing it with the element prefix when useful:

- `aItems` for a general collection;
- `anIndices` for integral values;
- `avecVertices` for vectors.

The prefix describes the collection role rather than its concrete container type.

## Scope, storage, and constants

Scope or storage modifiers precede the semantic prefix:

| Prefix | Meaning | Examples |
| --- | --- | --- |
| `m_` | Instance field | `m_nCount`, `m_roImpl` |
| `s_` | Static field or function-local static | `s_hNext`, `s_oState` |
| `g_` | Namespace or global mutable state | `g_strLastError` |
| `c_` | Constant | `c_nLimit`, `c_fTolerance` |

Prefix named `constexpr`, `consteval`, and immutable namespace-scope constants with `c_`, followed by the semantic prefix. Ordinary `const` locals and parameters retain their semantic prefix because `const` expresses immutability rather than a named constant.

Public C ABI macros and constants retain their established uppercase `PK...` names.

## Functions and methods

A function or method name carries the semantic prefix of its result:

- `bIsEmpty()` returns a Boolean;
- `nMemUsage()` returns an integral value;
- `fVoxelSizeMM()` returns a floating-point value;
- `strDiagnose()` returns a string;
- `roAsMesh()` returns a smart pointer.

A function returning `void` uses an unprefixed action name such as `Clear()`, `Offset()`, or `GetView()`.

Export C ABI functions as `Subsystem_ResultPrefixAction`, such as `Mesh_bGetView` and `Voxels_hCreate`.

## Ownership and pointers

- Use RAII and prefer smart pointers and standard containers for ownership.
- Use `ro` for all smart pointers and `p` only for raw pointers.
- Use `const T&` for non-owning structured inputs.
- Use raw pointers for optional buffers, output parameters, and C ABI data.
- State ownership and lifetime explicitly in public contracts when they are not self-evident.

## C ABI

- Never allow C++ exceptions to cross the C ABI.
- Initialize output parameters to deterministic fallback values before fallible work.
- Keep ABI structures standard-layout and verify relevant sizes and traits with `static_assert`.
- Use fixed-width integer types where range or ABI layout matters.
- Keep the public ABI independent of OpenVDB and other implementation dependencies.

## Structure and formatting

- Name project-authored Markdown documentation files in PascalCase, such as `CodingConventions.md`. Keep ecosystem-standard root names such as `README.md` and `LICENSE` unchanged.
- Use C++20.
- Put implementation details in the `PicoGK` namespace and translation-unit helpers in an anonymous namespace.
- Include the corresponding project header first, then other project, third-party, and standard-library headers in separate groups.
- Put braces on separate lines.
- Use one parameter per line for nontrivial signatures and calls.
- Use `///` documentation for public contracts, especially ownership, lifetime, units, and error behavior.

## Mathematical notation

Conventional short names may omit semantic prefixes when they materially improve a small, math-dense expression. Local Cartesian components may use `x`, `y`, and `z`; canonical vertices or indices may use `A`, `B`, `C`, and `D`.

Keep these exceptions narrow. Use descriptive prefixed names whenever scope, role, or units would otherwise be unclear, especially at public API and serialization boundaries.
