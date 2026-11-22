import * as THREE from "three";

// A shared architectural illustration, not a reconstruction of database floors.
export function createHotel(active: boolean) {
  const group = new THREE.Group();
  const stone = new THREE.MeshStandardMaterial({
    color: 0xe8e1cf,
    roughness: 0.85,
  });
  const trim = new THREE.MeshStandardMaterial({
    color: active ? 0x21665e : 0x78817f,
    roughness: 0.65,
  });
  const glass = new THREE.MeshStandardMaterial({
    color: 0x466568,
    metalness: 0.35,
    roughness: 0.25,
  });
  const brass = new THREE.MeshStandardMaterial({
    color: 0xb89959,
    metalness: 0.45,
    roughness: 0.5,
  });
  const box = (
    w: number,
    h: number,
    d: number,
    x: number,
    y: number,
    z: number,
    material: THREE.Material,
  ) => {
    const mesh = new THREE.Mesh(new THREE.BoxGeometry(w, h, d), material);
    mesh.position.set(x, y, z);
    mesh.castShadow = true;
    mesh.receiveShadow = true;
    group.add(mesh);
    return mesh;
  };
  box(4.8, 0.22, 3.7, 0, 0.11, 0, stone);
  box(3.8, 5.7, 2.6, 0, 3, 0, stone);
  for (let floor = 0; floor < 5; floor++) {
    const y = 1.2 + floor * 0.96;
    box(4, 0.12, 2.8, 0, y + 0.48, 0, trim);
    for (let col = 0; col < 4; col++) {
      const x = (col - 1.5) * 0.83;
      box(0.63, 0.64, 0.07, x, y, 1.33, glass);
      box(0.7, 0.06, 0.18, x, y - 0.34, 1.36, brass);
      box(0.035, 0.64, 0.09, x, y, 1.38, brass);
    }
    for (const x of [-1.93, 1.93])
      for (const z of [-0.7, 0.4]) box(0.06, 0.64, 0.65, x, y, z, glass);
  }
  box(4.15, 0.2, 2.95, 0, 5.95, 0, brass);
  box(2.4, 0.45, 1.9, 0, 6.25, -0.1, trim);
  box(1.15, 1.02, 0.12, 0, 0.68, 1.4, glass);
  box(0.06, 1.02, 0.16, 0, 0.68, 1.48, brass);
  box(2.1, 0.16, 1, 0, 1.38, 1.65, trim);
  for (const x of [-0.92, 0.92]) box(0.09, 1.2, 0.09, x, 0.7, 1.98, brass);
  for (const x of [-1.75, 1.75]) {
    box(0.45, 0.4, 0.45, x, 0.4, 1.57, trim);
    const shrub = new THREE.Mesh(new THREE.SphereGeometry(0.32, 10, 8), trim);
    shrub.position.set(x, 0.85, 1.57);
    group.add(shrub);
  }
  return group;
}
