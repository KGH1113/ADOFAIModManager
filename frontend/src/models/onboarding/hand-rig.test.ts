import { describe, expect, it } from "vitest";
import {
  createHandColours,
  createHandPose,
  deformHand,
  handBaseColour,
  handView,
  projectHandPoint,
  restingJoints,
  type Vector,
} from "./hand-rig";

const distance = (a: readonly number[], b: readonly number[]) =>
  Math.hypot(...a.map((value, index) => value - b[index]));

describe("hand anatomy and key presses", () => {
  it("highlights the outer palm edge without colouring fingertips or moving the hand", () => {
    const values = [0, 0, 0, 0, 0];
    const neutral = deformHand(values);
    const highlights = [0, 0, 0, 0, 0, 1];
    const colours = createHandColours(highlights);
    const tinted = createHandPose(values, highlights);
    expect(tinted.positions).toEqual(neutral.positions);
    expect(tinted.joints).toEqual(neutral.joints);
    const closest = (point: Vector) =>
      neutral.positions.reduce(
        (best, position, index) =>
          distance(position, point) < distance(neutral.positions[best], point) ? index : best,
        0,
      );
    for (const joint of [0, 5, 10, 15, 20, 24])
      expect(colours[closest(neutral.joints[joint])]).toEqual(handBaseColour);
    const coloured = colours
      .map((colour, index) => ({ colour, position: neutral.positions[index] }))
      .filter(({ colour }) =>
        colour.some((channel, i) => Math.abs(channel - handBaseColour[i]) > 0.01),
      );
    expect(coloured.length).toBeGreaterThan(0);
    expect(
      coloured.every(({ colour }) => colour[1] > colour[0] && colour[2] < handBaseColour[2]),
    ).toBe(true);
  });
  it("tints only the hovered finger while preserving the wrist and the hand pose", () => {
    const values = [0, 0, 0, 0, 0];
    const neutral = deformHand(values);
    const closestVertex = (point: Vector) =>
      neutral.positions.reduce(
        (closest, position, index) =>
          distance(position, point) < distance(neutral.positions[closest], point) ? index : closest,
        0,
      );
    const tips = [0, 5, 10, 15, 20].map((joint) => closestVertex(neutral.joints[joint]));
    const wrist = closestVertex(neutral.joints[24]);
    for (let finger = 0; finger < 5; finger++) {
      const highlights = Array.from({ length: 5 }, (_, index) => (index === finger ? 1 : 0));
      const colours = createHandColours(highlights);
      expect(colours.length).toBe(neutral.positions.length);
      for (const [index, vertex] of tips.entries()) {
        if (index === finger) {
          expect(colours[vertex][1]).toBeGreaterThan(colours[vertex][0]);
          expect(colours[vertex][2]).toBeLessThan(handBaseColour[2]);
        } else expect(colours[vertex]).toEqual(handBaseColour);
      }
      expect(colours[wrist]).toEqual(handBaseColour);
      const tinted = createHandPose(values, highlights);
      expect(tinted.positions).toEqual(neutral.positions);
      expect(tinted.joints).toEqual(neutral.joints);
    }
  });

  it("anchors the palm and wrist while the first finger joint participates in a press", () => {
    for (let finger = 0; finger < 5; finger++) {
      for (const progress of [0, 0.25, 0.5, 0.75, 1]) {
        const values = Array.from({ length: 5 }, (_, index) => (index === finger ? progress : 0));
        const { joints } = deformHand(values);
        expect(joints[24]).toEqual(restingJoints[24]);
        const anchors = finger === 4 ? [23] : [finger * 5 + 4, finger * 5 + 3];
        for (const anchor of anchors) expect(joints[anchor]).toEqual(restingJoints[anchor]);
        if (progress > 0) {
          const firstKnuckle = finger === 4 ? 22 : finger * 5 + 2;
          expect(joints[firstKnuckle][2]).toBeGreaterThan(restingJoints[firstKnuckle][2]);
        }
      }
    }
  });

  it("preserves each phalanx length and the other fingers while bending", () => {
    for (let finger = 0; finger < 5; finger++) {
      const values = Array.from({ length: 5 }, (_, index) => (index === finger ? 1 : 0));
      const { joints } = deformHand(values);
      const chain =
        finger === 4
          ? [23, 22, 21, 20]
          : [finger * 5 + 4, finger * 5 + 3, finger * 5 + 2, finger * 5 + 1, finger * 5];
      for (let step = 1; step < chain.length; step++)
        expect(distance(joints[chain[step]], joints[chain[step - 1]])).toBeCloseTo(
          distance(restingJoints[chain[step]], restingJoints[chain[step - 1]]),
          8,
        );
      const tip = chain[chain.length - 1];
      expect(joints[tip][2]).toBeGreaterThan(restingJoints[tip][2]);
      expect(joints[tip][1]).toBeGreaterThan(restingJoints[tip][1]);
      const moving = new Set(chain);
      joints.forEach((joint, index) => {
        if (!moving.has(index)) expect(joint).toEqual(restingJoints[index]);
      });
    }
  });

  it("keeps the complete skin finite, with unit normals, during overlapping press and release", () => {
    for (const values of [
      [0, 0, 0, 0, 0],
      [1, 0, 0, 0, 0],
      [0.7, 0.3, 0, 0, 0],
      [0, 0, 1, 0, 1],
      [1, 1, 1, 1, 1],
    ]) {
      const pose = deformHand(values);
      for (const position of pose.positions)
        for (const value of position) expect(Number.isFinite(value)).toBe(true);
      for (const normal of pose.normals) expect(Math.hypot(...normal)).toBeCloseTo(1, 8);
    }
  });

  it("clamps animation overshoot and produces ordered visible surfaces", () => {
    expect(deformHand([-0.2, 0, 0, 0, 0])).toEqual(deformHand([0, 0, 0, 0, 0]));
    expect(deformHand([0, 0, 0, 1.2, 0])).toEqual(deformHand([0, 0, 0, 1, 0]));
    const { faces } = createHandPose([0, 0, 0, 1, 0]);
    expect(faces.length).toBeGreaterThan(0);
    let depth = -Infinity;
    for (const face of faces) {
      expect(face.depth).toBeGreaterThanOrEqual(depth);
      depth = face.depth;
      for (const point of face.points) {
        expect(Number.isFinite(point.x)).toBe(true);
        expect(Number.isFinite(point.y)).toBe(true);
      }
    }
  });

  it("keeps perspective subtle around the hand and centres the camera target", () => {
    const centre = projectHandPoint(handView.target);
    expect(centre.x).toBeCloseTo(handView.width / 2, 8);
    expect(centre.y).toBeCloseTo(handView.height / 2, 8);
    const offset = (depth: number): Vector => [
      handView.target[0] + handView.horizontal[0] * 30 + handView.camera[0] * depth,
      handView.target[1] + handView.horizontal[1] * 30 + handView.camera[1] * depth,
      handView.target[2] + handView.horizontal[2] * 30 + handView.camera[2] * depth,
    ];
    const closer = projectHandPoint(offset(50));
    const further = projectHandPoint(offset(-50));
    const sizeRatio = (closer.x - centre.x) / (further.x - centre.x);
    expect(sizeRatio).toBeGreaterThan(1);
    expect(sizeRatio).toBeLessThan(1.12);
    expect(closer.cameraDistance).toBeLessThan(further.cameraDistance);
  });

  it("projects relaxed and pressed skin inside the camera clipping planes", () => {
    for (const values of [
      [0, 0, 0, 0, 0],
      [0.5, 0.5, 0.5, 0.5, 0.5],
      [1, 1, 1, 1, 1],
    ]) {
      for (const position of deformHand(values).positions) {
        const projected = projectHandPoint(position);
        expect(Number.isFinite(projected.x)).toBe(true);
        expect(Number.isFinite(projected.y)).toBe(true);
        expect(projected.cameraDistance).toBeGreaterThan(handView.near);
        expect(projected.cameraDistance).toBeLessThan(handView.far);
      }
    }
  });
});
