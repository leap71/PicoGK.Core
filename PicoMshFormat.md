# PicoMsh v1

PicoMsh (`.picomsh`) is a deterministic, lossless persistence format for one PicoGK `Mesh`. It stores the original shared vertex array and separate quad and triangle arrays. Quads are not triangulated.

“Lossless” means that vertex IEEE 754 binary32 bit patterns, unsigned indices, array ordering, and face type survive a write/read round trip. PicoMsh is not a serialization of the native runtime object and stores no triangulation cache.

## API

```csharp
using Mesh msh = PicoMsh.mshFromFile(lib, "input.picomsh");
PicoMsh.Write(msh, "output.picomsh");
```

Equivalent stream overloads leave the supplied stream open; input streams must be readable, seekable, and positioned at zero so physical length can be validated before allocation. A stream reader allocates managed arrays before the native `Mesh` copies them. On little-endian platforms, the file overload memory-maps each array and passes direct views to the native constructor, avoiding the intermediate managed arrays. The current immutable `Mesh` contract still copies the data once into native-owned storage; memory mapping does not make a `Mesh` retain the file mapping.

## Primitive representation

All integers and binary32 values are little-endian.

- vertex: three IEEE 754 binary32 values `X`, `Y`, `Z`; stride 12 bytes;
- quad: four unsigned 32-bit vertex indices `A`, `B`, `C`, `D`; stride 16 bytes;
- triangle: three unsigned 32-bit vertex indices `A`, `B`, `C`; stride 12 bytes.

Coordinates are PicoGK world-space millimetres. Components map directly to PicoGK `X`, `Y`, and `Z`; readers perform no scaling, axis exchange, or reflection. Face index order—and therefore winding—is preserved. The format does not require manifoldness, non-degeneracy, consistent winding, or an outward orientation.

Vertices must be finite. Every face index must be less than the vertex count.

## File layout

The file consists of a fixed 96-byte header followed by tightly packed arrays in this canonical order:

1. vertices;
2. quads;
3. triangles.

There is no padding or trailing data. Empty arrays occupy zero bytes. Header offsets are retained even though v1 counts and strides determine them: they support direct section lookup, provide redundant corruption checks, and leave a clear path for a future version with a different layout.

### Header

| Offset | Size | Type | Field | v1 value or meaning |
| ---: | ---: | --- | --- | --- |
| 0 | 8 | bytes | Magic | ASCII `PICOMSH` followed by NUL |
| 8 | 4 | `uint32` | Version | `1` |
| 12 | 4 | `uint32` | Header size | `96` |
| 16 | 4 | `uint32` | Flags | `0` |
| 20 | 4 | `uint32` | Vertex stride | `12` |
| 24 | 4 | `uint32` | Quad stride | `16` |
| 28 | 4 | `uint32` | Triangle stride | `12` |
| 32 | 8 | `uint64` | Vertex count | Number of vertices |
| 40 | 8 | `uint64` | Quad count | Number of quads |
| 48 | 8 | `uint64` | Triangle count | Number of triangles |
| 56 | 8 | `uint64` | Vertex offset | `96` |
| 64 | 8 | `uint64` | Quad offset | vertex offset + vertex count × 12 |
| 72 | 8 | `uint64` | Triangle offset | quad offset + quad count × 16 |
| 80 | 8 | `uint64` | File length | triangle offset + triangle count × 12 |
| 88 | 8 | `uint64` | Reserved | `0` |

Counts are 64-bit so malformed or future-scale files can be rejected without arithmetic ambiguity. The current managed `Mesh` API limits each count to `int.MaxValue` and the native ABI uses 32-bit counts; a v1 reader must reject values it cannot represent rather than truncate them.

## Validation and compatibility

A v1 reader rejects:

- a bad magic value, unsupported version, flags, strides, or nonzero reserved field;
- count/size arithmetic overflow;
- offsets inconsistent with the canonical contiguous layout;
- a physical file length different from the declared length;
- truncation or trailing data;
- non-finite coordinates; and
- out-of-range indices.

Topology properties are not format-validity requirements. PicoMsh v1 has no optional sections or metadata. Unknown flags are therefore required features and must be rejected. A layout or representation change requires a new format version.
