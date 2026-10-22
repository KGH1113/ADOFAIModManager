import geometry from "@/assets/hand/hand-geometry.json";

export type Vector = readonly [number, number, number];
type Quaternion = readonly [number, number, number, number];
export interface HandFace {
  id: number;
  points: readonly { x: number; y: number }[];
  fill: string;
  depth: number;
}

export const handBaseColour: Vector = [248, 248, 250];
// Fallback sRGB value for --primary; the renderer reads the actual theme colour.
export const handAccentColour: Vector = [124, 207, 0];
export const handHighlightStrength = 0.6;

const identity: Quaternion = [0, 0, 0, 1];
const add = (a: Vector, b: Vector): Vector => [a[0] + b[0], a[1] + b[1], a[2] + b[2]];
const scale = (vector: Vector, amount: number): Vector => [
  vector[0] * amount,
  vector[1] * amount,
  vector[2] * amount,
];
const subtract = (a: Vector, b: Vector): Vector => [a[0] - b[0], a[1] - b[1], a[2] - b[2]];
const dot = (a: Vector, b: Vector) => a[0] * b[0] + a[1] * b[1] + a[2] * b[2];
const unit = (vector: Vector): Vector => {
  const length = Math.hypot(...vector);
  return [vector[0] / length, vector[1] / length, vector[2] / length];
};

function rotate([x, y, z, w]: Quaternion, [vx, vy, vz]: Vector): Vector {
  const tx = 2 * (y * vz - z * vy);
  const ty = 2 * (z * vx - x * vz);
  const tz = 2 * (x * vy - y * vx);
  return [
    vx + w * tx + y * tz - z * ty,
    vy + w * ty + z * tx - x * tz,
    vz + w * tz + x * ty - y * tx,
  ];
}

function multiply(a: Quaternion, b: Quaternion): Quaternion {
  return [
    a[3] * b[0] + a[0] * b[3] + a[1] * b[2] - a[2] * b[1],
    a[3] * b[1] - a[0] * b[2] + a[1] * b[3] + a[2] * b[0],
    a[3] * b[2] + a[0] * b[1] - a[1] * b[0] + a[2] * b[3],
    a[3] * b[3] - a[0] * b[0] - a[1] * b[1] - a[2] * b[2],
  ];
}

function turn(axis: Vector, degrees: number): Quaternion {
  const angle = (degrees * Math.PI) / 360;
  const sine = Math.sin(angle);
  return [axis[0] * sine, axis[1] * sine, axis[2] * sine, Math.cos(angle)];
}

const wrist = geometry.jointPositions[24];
const canonical = (position: readonly number[]): Vector => [
  112 - (position[2] - wrist[2]) * 1300,
  245 + (position[1] - wrist[1]) * 1300,
  (position[0] - wrist[0]) * 1300,
];
const bindJoints = geometry.jointPositions.map(canonical);
const thumbRoot = bindJoints[23];
const thumbDirection = subtract(bindJoints[20], thumbRoot);
// Degrees toward the index finger in the relaxed pose.
const thumbRestAdduction = 12;
const thumbRestRotation = turn(unit([thumbDirection[1], -thumbDirection[0], 0]), -24);
const thumbRestPosition = (position: Vector) =>
  add(thumbRoot, rotate(thumbRestRotation, subtract(position, thumbRoot)));
const skinBindJoints = bindJoints.map((position, joint) =>
  joint >= 20 && joint < 24 ? thumbRestPosition(position) : position,
);
const vertices: Vector[] = [];
const framingVertices: Vector[] = [];
const normals: Vector[] = [];
const highlightWeights: number[][] = [];
for (let index = 0; index < geometry.positions.length; index += 3) {
  const position = canonical(
    geometry.positions.slice(index, index + 3).map((value) => value / geometry.positionScale),
  );
  const normal = unit([
    -geometry.normals[index + 2],
    geometry.normals[index + 1],
    geometry.normals[index],
  ]);
  let thumbWeight = 0;
  let totalWeight = 0;
  const weights = [0, 0, 0, 0, 0];
  for (let influence = 0; influence < 4; influence++) {
    const skinIndex = (index / 3) * 4 + influence;
    const joint = geometry.skinJoints[skinIndex];
    const weight = geometry.skinWeights[skinIndex];
    if (joint >= 20 && joint < 24) thumbWeight += weight;
    // Metacarpals and the wrist stay white; skin weights soften the knuckle boundary.
    if (joint < 23 && joint % 5 < 4) weights[Math.floor(joint / 5)] += weight;
    totalWeight += weight;
  }
  highlightWeights.push(weights.map((weight) => weight / totalWeight));
  thumbWeight /= totalWeight;
  const posed = add(
    scale(position, 1 - thumbWeight),
    scale(thumbRestPosition(position), thumbWeight),
  );
  const posedNormal = add(
    scale(normal, 1 - thumbWeight),
    scale(rotate(thumbRestRotation, normal), thumbWeight),
  );
  framingVertices.push(posed);
  // Continue the forearm beyond the frame, as in the video, instead of showing its cut end.
  const extension = Math.max(0, posed[1] - 230) * 5;
  vertices.push([posed[0], posed[1] + extension, posed[2]]);
  normals.push(unit([posedNormal[0], posedNormal[1] / (extension > 0 ? 6 : 1), posedNormal[2]]));
}

// Follow the little-finger side of the palm without moving any joints.
const edgeStart = skinBindJoints[3][1] + 8;
const edgeEnd = skinBindJoints[24][1];
const smooth = (value: number) => {
  const amount = Math.max(0, Math.min(1, value));
  return amount * amount * (3 - 2 * amount);
};
for (const [index, position] of framingVertices.entries()) {
  let edge = 0;
  if (position[1] > edgeStart && position[1] < edgeEnd) {
    const outer = Math.min(
      ...framingVertices
        .filter((point) => Math.abs(point[1] - position[1]) < 6)
        .map((point) => point[0]),
    );
    edge =
      smooth(1 - (position[0] - outer) / 20) *
      smooth((position[1] - edgeStart) / 12) *
      smooth((edgeEnd - position[1]) / 16);
  }
  highlightWeights[index].push(edge);
}

// Match the reference's diagonal, elevated view of the back of the hand.
// Positive depth points into the palm, away from the viewer when a finger presses.
const azimuth = (-30 * Math.PI) / 180;
const elevation = (23 * Math.PI) / 180;
const horizontalAxis: Vector = [Math.cos(azimuth), Math.sin(azimuth), 0];
const verticalAxis: Vector = [
  -Math.sin(azimuth) * Math.cos(elevation),
  Math.cos(azimuth) * Math.cos(elevation),
  Math.sin(elevation),
];
const camera: Vector = [
  -Math.sin(azimuth) * Math.sin(elevation),
  Math.cos(azimuth) * Math.sin(elevation),
  -Math.cos(elevation),
];
const light = unit([-0.35, -0.45, -0.82]);
const relaxedPose = makeJointPose([0, 0, 0, 0, 0]);
export const restingJoints = relaxedPose.joints;
const framingPositions = skinHand(relaxedPose, framingVertices).positions;
const xs = framingPositions.map((position) => dot(position, horizontalAxis));
const ys = framingPositions.map((position) => dot(position, verticalAxis));
const minX = Math.min(...xs);
const maxX = Math.max(...xs);
const minY = Math.min(...ys);
const maxY = Math.max(...ys);
const depths = framingPositions.map((position) => dot(position, camera));
const target = add(
  add(scale(horizontalAxis, (minX + maxX) / 2), scale(verticalAxis, (minY + maxY) / 2)),
  scale(camera, (Math.min(...depths) + Math.max(...depths)) / 2),
);
const verticalFieldOfView = (1 * Math.PI) / 180;
const focalLength = 140 / Math.tan(verticalFieldOfView / 2);
// A narrow field of view and a distant camera keep perspective subtle. Fit once
// to the relaxed pose so the framing remains steady while fingers move.
const distance = Math.max(
  ...framingPositions.map((position) => {
    const relative = subtract(position, target);
    const depth = dot(relative, camera);
    return Math.max(
      depth + (focalLength * Math.abs(dot(relative, horizontalAxis))) / 109,
      depth + (focalLength * Math.abs(dot(relative, verticalAxis))) / 124,
    );
  }),
);
const near = distance / 4;
const far = distance * 4;
export const handView = {
  horizontal: horizontalAxis,
  vertical: verticalAxis,
  camera,
  target,
  distance,
  focalLength,
  verticalFieldOfView,
  near,
  far,
  lensScale: [focalLength / 125, focalLength / 140] as const,
  clipDepth: [(far + near) / (far - near), (-2 * far * near) / (far - near)] as const,
  width: 250,
  height: 280,
};

export function projectHandPoint(position: Vector) {
  const relative = subtract(position, target);
  const depth = dot(relative, camera);
  const cameraDistance = distance - depth;
  const magnification = focalLength / cameraDistance;
  return {
    x: 125 + dot(relative, horizontalAxis) * magnification,
    y: 140 + dot(relative, verticalAxis) * magnification,
    depth,
    cameraDistance,
  };
}

function makeJointPose(values: readonly number[]) {
  const progress = Array.from({ length: 5 }, (_, index) =>
    Math.max(0, Math.min(1, values[index] ?? 0)),
  );
  const joints = [...skinBindJoints];
  const rotations: Quaternion[] = skinBindJoints.map(() => identity);

  function flexionAxis(joint: number, next: number) {
    const direction = subtract(skinBindJoints[next], skinBindJoints[joint]);
    return unit([direction[1], -direction[0], 0]);
  }

  function bendChain(chain: number[], turns: Quaternion[]) {
    for (let step = 0; step < chain.length; step++) {
      const joint = chain[step];
      const parent = chain[step - 1];
      const parentRotation = step === 0 ? identity : rotations[parent];
      if (step > 0)
        joints[joint] = add(
          joints[parent],
          rotate(parentRotation, subtract(skinBindJoints[joint], skinBindJoints[parent])),
        );
      rotations[joint] = multiply(parentRotation, turns[step] ?? identity);
    }
  }

  // Begin in a relaxed typing pose; pressing adds to all three joint angles.
  const relaxedAngles = [
    [17, 18, 9],
    [15, 17, 8],
    [13, 16, 8],
    [12, 16, 8],
  ];
  for (let finger = 0; finger < 4; finger++) {
    const [base, middle, distal] = relaxedAngles[finger];
    const press = progress[finger];
    const axis = flexionAxis(finger * 5 + 3, finger * 5 + 2);
    bendChain(
      [finger * 5 + 3, finger * 5 + 2, finger * 5 + 1, finger * 5],
      [base + 12 * press, middle + 7 * press, distal + 3 * press, 0].map((angle) =>
        turn(axis, angle),
      ),
    );
  }
  // The thumb presses mostly from its saddle joint. Opposition turns the whole
  // thumb toward the palm while the MCP and IP joints make much smaller bends.
  const thumbPress = progress[4];
  const thumbLongAxis = unit(subtract(skinBindJoints[22], skinBindJoints[23]));
  const opposition = multiply(
    turn(thumbLongAxis, 6 * thumbPress),
    turn([0, 0, 1], -thumbRestAdduction - 6 * thumbPress),
  );
  bendChain(
    [23, 22, 21, 20],
    [
      multiply(opposition, turn(flexionAxis(23, 22), 8 + 18 * thumbPress)),
      turn(flexionAxis(22, 21), 6 + 6 * thumbPress),
      turn(flexionAxis(21, 20), 4 + 5 * thumbPress),
      identity,
    ],
  );
  return { joints, rotations };
}

function skinHand(
  { joints, rotations }: ReturnType<typeof makeJointPose>,
  source: readonly Vector[] = vertices,
) {
  const positions: Vector[] = [];
  const posedNormals: Vector[] = [];
  for (let vertex = 0; vertex < source.length; vertex++) {
    let position: Vector = [0, 0, 0];
    let normal: Vector = [0, 0, 0];
    let total = 0;
    for (let influence = 0; influence < 4; influence++) {
      const index = vertex * 4 + influence;
      const weight = geometry.skinWeights[index];
      if (weight === 0) continue;
      const joint = geometry.skinJoints[index];
      const moved = add(
        joints[joint],
        rotate(rotations[joint], subtract(source[vertex], skinBindJoints[joint])),
      );
      const movedNormal = rotate(rotations[joint], normals[vertex]);
      position = add(position, [moved[0] * weight, moved[1] * weight, moved[2] * weight]);
      normal = add(normal, [
        movedNormal[0] * weight,
        movedNormal[1] * weight,
        movedNormal[2] * weight,
      ]);
      total += weight;
    }
    positions.push([position[0] / total, position[1] / total, position[2] / total]);
    posedNormals.push(unit(normal));
  }

  return { joints, positions, normals: posedNormals };
}

export function deformHand(values: readonly number[]) {
  return skinHand(makeJointPose(values));
}

export function createHandColours(
  highlights: readonly number[] = [],
  accent: Vector = handAccentColour,
) {
  const amounts = Array.from({ length: 6 }, (_, index) =>
    Math.max(0, Math.min(1, highlights[index] ?? 0)),
  );
  const offset = subtract(accent, handBaseColour);
  return highlightWeights.map((weights) => {
    const amount = Math.min(
      1,
      weights.reduce((total, weight, index) => total + weight * amounts[index], 0),
    );
    return add(handBaseColour, scale(offset, amount * handHighlightStrength));
  });
}

export function createHandPose(
  values: readonly number[],
  highlights: readonly number[] = [],
  accent: Vector = handAccentColour,
) {
  const { joints, positions, normals: posedNormals } = deformHand(values);
  const colours = createHandColours(highlights, accent);
  const projected = positions.map(projectHandPoint);
  const faces: HandFace[] = [];
  for (let index = 0; index < geometry.indices.length; index += 3) {
    const a = geometry.indices[index];
    const b = geometry.indices[index + 1];
    const c = geometry.indices[index + 2];
    const normal = unit(add(add(posedNormals[a], posedNormals[b]), posedNormals[c]));
    const centre = scale(add(add(positions[a], positions[b]), positions[c]), 1 / 3);
    const toCamera = subtract(add(target, scale(camera, distance)), centre);
    if (dot(normal, toCamera) <= 0) continue;
    const illumination = Math.max(0, Math.min(1, (dot(normal, light) + 0.1) / 0.6));
    const shade = 0.78 + 0.22 * illumination * illumination * (3 - 2 * illumination);
    const colour = scale(add(add(colours[a], colours[b]), colours[c]), 1 / 3).map((channel) =>
      Math.round(channel * shade),
    );
    faces.push({
      id: index / 3,
      points: [projected[a], projected[b], projected[c]],
      fill: `rgb(${colour.join(" ")})`,
      depth:
        (dot(positions[a], camera) + dot(positions[b], camera) + dot(positions[c], camera)) / 3,
    });
  }
  faces.sort((a, b) => a.depth - b.depth);
  return { joints, positions, faces };
}
