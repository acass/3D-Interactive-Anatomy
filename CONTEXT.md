# 3D Interactive Explainer

A web app that displays a single 3D model and lets a user ask about its features by voice or text; the camera animates to frame the asked-about feature while a narrated explanation plays.

## Language

**Model**:
The single 3D object being explained, loaded as a GLB file. During early development this is a placeholder GLB; it will be replaced by the real subject later.
_Avoid_: mesh, asset, object

**Feature**:
A named, explainable part or aspect of the Model that a user can ask about (e.g. "the lens", "the exhaust port").
_Avoid_: part, component

**Callout**:
The on-screen label that marks a Feature, anchored to a fixed point on the Model's surface.
_Avoid_: hotspot, annotation, marker, pin, tag

**Idle Pose**:
The default resting state of the camera — a fixed framing of the whole Model with a gentle auto-rotate — that the camera departs from when a Feature is asked about and returns to when the conversation goes quiet.
_Avoid_: home, default view, rest state

**Anchor**:
The fixed spot on the Model's surface where a Callout attaches — a surface position plus its outward normal. The Callout sits at the position; the camera framing for the Feature is derived from the normal (the camera looks at the position from along the outward normal).
_Avoid_: position, target, point

**Author Mode**:
A built-in toggle (via URL flag) that turns the viewer into an anchor-capturing tool: clicking the Model prints the surface position and normal at that point, which are pasted into the Feature Manifest. How Anchors get authored for both the placeholder and the real Model.
_Avoid_: edit mode, admin, editor

**Feature Manifest**:
The hand-authored file that is the single source of truth for every Feature — its Callout label, its Anchor, and its authored Explanation. Drives the callouts, the camera targets, and the grounding given to the assistant. Replacing it (plus the GLB) is how the real subject swaps in for the placeholder.
_Avoid_: config, data, features file

**Explanation**:
The authored text describing a Feature, held in the Feature Manifest, that the assistant narrates when that Feature is asked about. The assistant does not invent facts about the Model beyond these.
_Avoid_: description, answer, script
