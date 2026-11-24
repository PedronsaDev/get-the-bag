MeshOutline (project fork of Quick Outline)
==========================================
Original Quick Outline developed by Chris Nolet (c) 2018.
Project extensions by PedronsaDev.

See Docs/MeshOutline.md for the rendering contract, authoring workflow,
runtime API, migration details and verification limits.

The inspector provides validation, readable-mesh baking, complete presets,
reusable styles and Play Mode effect controls. Rendering runs in Play Mode.
Use MeshOutline.SetHighlighted for hover, and Ping/Flash for temporary effects.

Imported mesh data is preserved. Bake outline data instead of enabling CPU
read/write on every source model. Outlined objects must not use static batching.
Stencil bit 0 is reserved for this system; overlapping masks merge.

Width mode value 1 preserves legacy centimeters. Pixel mode uses render-target
pixels; true world meters use mode value 2.
