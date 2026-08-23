# PicoGK.Core

PicoGK.Core is the next-generation core geometry runtime for [PicoGK](https://picogk.org/).

It is a ground-up simplification of the PicoGK 2.x runtime, focused on a small and durable kernel around:

- sparse signed-distance voxels,
- immutable polygon meshes,
- efficient bulk geometry operations,
- a compact C ABI,
- and a managed .NET 10 / C# 14 API.

The native runtime currently uses OpenVDB internally, but the public PicoGK.Core API is intentionally designed to stay independent of OpenVDB-specific concepts.

## Status

**Early development — do not use yet.**

This repository is currently being assembled and the API, ABI, file formats, packaging, and behavior may change without notice.

There are no compatibility guarantees at this stage.

The first public packages will use the `3.0.0-alpha.*` version range. Even those releases should be considered experimental until explicitly stated otherwise.

## Direction

PicoGK.Core is intended to provide only the fundamental geometry kernel.

Higher-level geometry abstractions such as lattices, polylines, sweeps, sections, and other construction tools will live above Core rather than becoming part of the native runtime.

The project is the spiritual successor to PicoGKRuntime 2.x and is being developed as the foundation for PicoGK 3.x.

## License

PicoGK.Core is licensed under the Apache License 2.0.
