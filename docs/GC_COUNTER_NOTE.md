# GC counter interpretation note

`System.GC.CollectionCount()` in the Unity/Mono Boehm runtime must not be interpreted as a direct per-frame "GC work happened / did not happen" flag.

Mono's Boehm backend implements `mono_gc_collection_count(int generation)` by returning the single Boehm `GC_get_gc_no()` counter; the generation argument is ignored. Boehm increments that counter once per completed collection. During incremental marking, work may therefore occur before the counter changes.

Diagnostic consequence for `GK Frame Spike Probe`:

- `gc0/gc1/gc2=+1` confirms a collection counter advance during the sampled wall interval;
- `gc0/gc1/gc2=+0` does **not** by itself prove that no incremental GC work occurred;
- the three generation values are not independent on this backend.

Probe 0.3.0 adds Unity incremental-GC state to spike-only output so residual ~0.7 s CPU spikes can be classified without relying on `CollectionCount` alone.
