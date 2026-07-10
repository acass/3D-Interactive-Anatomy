// Derive a model-viewer camera-orbit ("theta phi radius") from a surface normal,
// so the camera looks at an anchor from along its outward normal. This is why
// the manifest only stores a normal, not a hand-authored camera angle.
//
// model-viewer orbit convention: camera position relative to target is
//   x = r*sin(phi)*sin(theta),  y = r*cos(phi),  z = r*sin(phi)*cos(theta)
// so for a unit direction (x,y,z): phi = acos(y), theta = atan2(x, z).
// The normals captured in ?author mode via positionAndNormalFromPoint are in
// this same frame, so they feed straight in (verified against SKULL.glb).

export function parseVec(s: string): [number, number, number] {
  const [x, y, z] = s.trim().split(/\s+/).map(Number);
  return [x, y, z];
}

export function orbitFromNormal(normal: [number, number, number], radiusMeters: number): string {
  const [nx, ny, nz] = normal;
  const len = Math.hypot(nx, ny, nz) || 1;
  const x = nx / len;
  const y = ny / len;
  const z = nz / len;
  const phi = Math.acos(Math.max(-1, Math.min(1, y)));
  const theta = Math.atan2(x, z);
  return `${theta}rad ${phi}rad ${radiusMeters}m`;
}
