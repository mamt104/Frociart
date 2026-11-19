Frociart Android — deterministic graph layout tests

Run from any directory with a JDK 17+:
  python3 tests/layout/run_layout_tests.py
Optionally pass --results /absolute/path (default: /tmp/frociart-layout-tests).
The compiler targets Java 8 so the same pure model/layout classes run on Android.
No Android SDK, Gradle, external library or network connection is needed.

FlowAutoLayout.apply(Document) changes only x/y. It returns false when the
target positions already match, including an empty document. It never replaces
nodes, reorders lists, edits content or changes edges. Invalid structural node
data and exceeded 500-node/1500-edge limits are rejected before any mutation.
Unresolved edges remain available to the app's validation UI.

Default gaps are 64 vertical, 88 horizontal, 160 between weak components, and
a 48-unit outer margin. Shapes use FlowModel.width/height. True branches lead
left and false branches right in normal decision graphs. Merges follow the
longest forward path and are centered using predecessor positions. DFS return
edges are excluded from layering only; the original loop edges remain intact.
Placement uses five fixed ordering passes, then packs disconnected components.
Extreme wide/tangled inputs are centered or placed on a defensive grid to stay
within the portable file format's +/-100000 coordinate range. This prioritizes
non-overlap and bounded work; it does not promise zero crossing edges for an
arbitrary nonplanar graph.

Tests verify content/object preservation, idempotence, independence from old
coordinates and edge order, branch/merge placement, countdown and large cycles,
self loops, unresolved edges, disconnected groups, coordinate limits, and
non-overlap for 500 nodes, 1500 edges and twelve seeded mixed graphs.

The log and JSON record exact source SHA-256 values and JVM timing scope.
Timing includes the whole apply() call, excludes Android rendering/animation,
and is not a promise about phone responsiveness or display frame rate.
