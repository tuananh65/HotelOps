import * as THREE from "three";
import { createHotel } from "./building";

export type HotelOption = { id: number; name: string; active: boolean };
const posters = new Map<boolean, string>();

// Render real 3D geometry once at a fixed camera angle. Reuse the result across
// cards so a long hotel list never allocates one WebGL context per hotel.
export function hotelPoster(active: boolean) {
  if (posters.has(active)) return posters.get(active)!;
  const renderer = new THREE.WebGLRenderer({
    antialias: true,
    preserveDrawingBuffer: true,
  });
  const scene = new THREE.Scene();
  try {
    renderer.setSize(600, 440);
    renderer.setClearColor(0xe6eeea, 1);
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;
    const camera = new THREE.PerspectiveCamera(32, 600 / 440, 0.1, 60);
    camera.position.set(10, 8.5, 13);
    camera.lookAt(0, 2.8, 0);
    scene.add(createHotel(active));
    scene.add(new THREE.HemisphereLight(0xffffff, 0x67776c, 2.6));
    const sun = new THREE.DirectionalLight(0xfff2d5, 3);
    sun.position.set(5, 12, 8);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    Object.assign(sun.shadow.camera, {
      left: -9,
      right: 9,
      top: 9,
      bottom: -9,
    });
    scene.add(sun);
    const ground = new THREE.Mesh(
      new THREE.PlaneGeometry(100, 100),
      new THREE.MeshStandardMaterial({ color: 0xe6eeea, roughness: 1 }),
    );
    ground.rotation.x = -Math.PI / 2;
    ground.receiveShadow = true;
    scene.add(ground);
    renderer.render(scene, camera);
    const poster = renderer.domElement.toDataURL("image/png");
    posters.set(active, poster);
    return poster;
  } finally {
    const materials = new Set<THREE.Material>();
    scene.traverse((obj) => {
      if (obj instanceof THREE.Mesh) {
        obj.geometry.dispose();
        (Array.isArray(obj.material) ? obj.material : [obj.material]).forEach(
          (m) => materials.add(m),
        );
      }
    });
    materials.forEach((m) => m.dispose());
    renderer.dispose();
    renderer.forceContextLoss();
  }
}
