# PicoVox v1

PicoVox (`.picovox`) is PicoGK.Core's deliberately narrow PicoGK Voxels exchange format. It transports one canonical PicoGK discretized narrow-band signed-distance volume in a simple and implementation agnostic format.

PicoVox is not a general volumetric scene, metadata, or extension format. Its core use is simple interchange and persistence of PicoGK Voxels data.

## Public API

```csharp
float fVoxelSizeMM = PicoVox.fReadVoxelSizeMM(oInputStream);
using Library lib = new(fVoxelSizeMM);
using Voxels vox = PicoVox.voxFromStream(lib, oInputStream);
PicoVox.Write(vox, oOutputStream);
```

Path-based helpers are also available:

```csharp
float fVoxelSizeMM = PicoVox.fReadVoxelSizeMM(strInputPath);
using Library lib = new(fVoxelSizeMM);
using Voxels vox = PicoVox.voxFromFile(lib, strInputPath);
PicoVox.Write(vox, strOutputPath);
```

Stream operations leave the supplied stream open. Reading requires a readable, seekable stream positioned at its beginning. `fReadVoxelSizeMM` rewinds the stream so it can immediately be passed to `voxFromStream`. The supplied Library voxel size must exactly match the manifest.

## ZIP container

A PicoVox file is a ZIP archive. Every entry must use ZIP compression method `0` (`NONE`/`STORE`). PNG already compresses each slice; a second ZIP compression pass is prohibited.

Entries occur in this exact order:

```text
manifest.txt
slices/src_0000000000.png
slices/src_0000000001.png
...
```

For a nonempty volume there are exactly `1 + SizeZ` entries. Slice index `z` uses ten zero-padded decimal digits and satisfies `0 <= z < SizeZ`. An empty volume contains only `manifest.txt`.

Names are case-sensitive ASCII paths using `/`. Directory entries, duplicate entries, unexpected entries, archive comments, multi-disk archives, and encrypted entries are invalid. ZIP64 records are permitted when required by archive sizes or entry counts.

The canonical Core writer sets entry timestamps to `1980-01-01T00:00:00Z`, clears external attributes, writes the manifest first, and then writes slices in increasing Z order.

## Manifest

`manifest.txt` is UTF-8 text restricted to ASCII in v1:

```text
PicoVoxVersion: 1
VoxelSizeMM: 0.5
OriginX: -20
OriginY: -40
OriginZ: 0
SizeX: 640
SizeY: 480
SizeZ: 3
```

The eight properties are required in that exact order. The separator is exactly a colon followed by one space. Blank lines, comments, quoting, escaping, leading or trailing whitespace, duplicate properties, and unknown properties are invalid.

The canonical writer uses LF line endings and a final LF. Readers accept LF or CRLF and may accept an omitted final line ending.

`VoxelSizeMM` is a finite positive IEEE 754 binary32 value written as invariant round-trip decimal text. Origins are canonical signed decimal `int32` values. Sizes are canonical nonnegative decimal `int32` values.

## Dimensions and coordinates

Dimensions must either all be zero or all be positive:

```text
SizeX = SizeY = SizeZ = 0
```

represents an empty volume and has no slice entries. All three origins must also be zero for an empty volume.

For each nonempty axis:

```text
0 < Size <= INT32_MAX
Origin + Size - 1 <= INT32_MAX
```

`OriginX`, `OriginY`, and `OriginZ` are voxel indices, not millimetre coordinates. A local sample `(x, y, z)` has index-space coordinate:

```text
(OriginX + x, OriginY + y, OriginZ + z)
```

and world-space position in millimetres:

```text
VoxelSizeMM * (OriginX + x, OriginY + y, OriginZ + z)
```

The managed implementation additionally requires one `SizeX * SizeY` signed-16-bit slice to fit in a CLR array. No aggregate `SizeX * SizeY * SizeZ` limit is imposed because slices are imported incrementally and the native volume is sparse.

## PNG slice profile

Each slice is a PNG image with:

| IHDR field | Required value |
| --- | --- |
| Width | `SizeX` |
| Height | `SizeY` |
| Bit depth | 16 |
| Colour type | 0, grayscale |
| Compression method | 0, zlib/Deflate |
| Filter method | 0 |
| Interlace method | 0, none |

The only permitted chunk sequence is:

```text
PNG signature
IHDR
one or more consecutive IDAT chunks
IEND
```

Ancillary chunks are not permitted. Every scanline uses PNG filter type `0` (`None`). Samples are unsigned 16-bit integers in PNG network byte order (most-significant byte first). Chunk CRC-32 values must be valid.

PNG row `r` and column `x` map to PicoGK index coordinates:

```text
X = OriginX + x
Y = OriginY + SizeY - 1 - r
Z = OriginZ + z
```

Thus columns increase in +X, rows increase in -Y, and files increase in +Z. This is the canonical orientation of PicoGK Z slices.

## SDF sample mapping

PicoGK's canonical signed-distance samples are signed 16-bit fixed-point values over the fixed three-voxel half-band:

| Meaning | Core sample | PNG sample |
| --- | ---: | ---: |
| Inside background | `-32767` | `1` |
| Zero isosurface | `0` | `32768` |
| Outside background | `32767` | `65535` |
| Reserved/invalid | `-32768` | `0` |

The exact conversion is:

```text
pngSample  = coreSample + 32768
coreSample = pngSample - 32768
```

PNG sample `0` is invalid. A valid Core sample `s` represents signed distance:

```text
s / 32767 * (3 * VoxelSizeMM)
```

Samples outside the finite volume are implicitly outside background. Boundary samples are not required to be outside background.

## Validation

A v1 reader rejects at least:

- malformed or noncanonical manifest properties;
- unsupported versions;
- invalid or partially empty dimensions;
- signed 32-bit coordinate overflow;
- a voxel size differing from the target `Library`;
- non-stored, encrypted, duplicate, missing, reordered, or unexpected ZIP entries;
- incorrect PNG dimensions or IHDR profile;
- unexpected PNG chunks, invalid CRC-32, interlacing, or nonzero scanline filters;
- truncated or excess decompressed scanline data;
- and reserved SDF samples.

Allocation failure remains possible for representable but impractically large slices or sparse volumes. PicoVox v1 does not define an arbitrary total-voxel or native-memory quota.

## Intentional limitations

PicoVox is deliberately simple and reduced, keeping in line with PicoGK's overall philosophy. It stores exactly one isotropic PicoGK SDF volume. It has no provision for multiple grids, anisotropic spacing, arbitrary transforms, materials, vector fields, provenance, previews, application metadata, or alternate compression and sample encodings.
