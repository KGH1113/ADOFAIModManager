import geometry from "@/assets/hand/hand-geometry.json";
import {
  createHandColours,
  deformHand,
  handAccentColour,
  handView,
  type Vector,
} from "@/models/onboarding/hand-rig";

const vertexSource = `
attribute vec3 position;
attribute vec3 normal;
attribute vec3 colour;
uniform vec3 horizontal;
uniform vec3 vertical;
uniform vec3 camera;
uniform vec3 target;
uniform float cameraDistanceToTarget;
uniform vec2 lensScale;
uniform vec2 clipDepth;
varying vec3 skinNormal;
varying vec3 skinColour;
void main() {
  vec3 relative = position - target;
  float x = dot(relative, horizontal);
  float y = dot(relative, vertical);
  float cameraDistance = cameraDistanceToTarget - dot(relative, camera);
  gl_Position = vec4(x * lensScale.x, -y * lensScale.y,
                    clipDepth.x * cameraDistance + clipDepth.y, cameraDistance);
  skinNormal = normal;
  skinColour = colour / 255.0;
}`;
const fragmentSource = `
precision mediump float;
varying vec3 skinNormal;
varying vec3 skinColour;
void main() {
  vec3 light = normalize(vec3(-0.35, -0.45, -0.82));
  float illumination = clamp((dot(normalize(skinNormal), light) + 0.1) / 0.6, 0.0, 1.0);
  float shade = 0.65 + 0.35 * smoothstep(0.0, 1.0, illumination);
  gl_FragColor = vec4(skinColour * shade, 1.0);
}`;

export function readHandAccent(element: HTMLElement): Vector {
  if (typeof CanvasRenderingContext2D === "undefined") return handAccentColour;
  const colour = getComputedStyle(element).getPropertyValue("--primary").trim();
  if (!colour) return handAccentColour;
  const probe = document.createElement("canvas");
  probe.width = probe.height = 1;
  const context = probe.getContext("2d", { willReadFrequently: true });
  if (!context) return handAccentColour;
  context.fillStyle = colour;
  context.fillRect(0, 0, 1, 1);
  const pixel = context.getImageData(0, 0, 1, 1).data;
  return [pixel[0], pixel[1], pixel[2]];
}

export function createHandRenderer(canvas: HTMLCanvasElement, accent: Vector = handAccentColour) {
  if (typeof WebGLRenderingContext === "undefined") return null;
  const gl = canvas.getContext("webgl", {
    alpha: true,
    antialias: true,
    premultipliedAlpha: false,
  });
  if (!gl) return null;
  const shaders: WebGLShader[] = [];
  const buffers: WebGLBuffer[] = [];
  const program = gl.createProgram();
  if (!program) return null;

  function dispose() {
    for (const buffer of buffers) gl?.deleteBuffer(buffer);
    for (const shader of shaders) gl?.deleteShader(shader);
    gl?.deleteProgram(program);
  }

  for (const [type, source] of [
    [gl.VERTEX_SHADER, vertexSource],
    [gl.FRAGMENT_SHADER, fragmentSource],
  ] as const) {
    const shader = gl.createShader(type);
    if (!shader) {
      dispose();
      return null;
    }
    shaders.push(shader);
    gl.shaderSource(shader, source);
    gl.compileShader(shader);
    if (!gl.getShaderParameter(shader, gl.COMPILE_STATUS)) {
      dispose();
      return null;
    }
    gl.attachShader(program, shader);
  }
  gl.linkProgram(program);
  if (!gl.getProgramParameter(program, gl.LINK_STATUS)) {
    dispose();
    return null;
  }
  // biome-ignore lint/correctness/useHookAtTopLevel: WebGL useProgram is not a React hook.
  gl.useProgram(program);
  for (const [name, value] of Object.entries({
    horizontal: handView.horizontal,
    vertical: handView.vertical,
    camera: handView.camera,
    target: handView.target,
  })) {
    gl.uniform3fv(gl.getUniformLocation(program, name), value);
  }
  gl.uniform1f(gl.getUniformLocation(program, "cameraDistanceToTarget"), handView.distance);
  gl.uniform2fv(gl.getUniformLocation(program, "lensScale"), handView.lensScale);
  gl.uniform2fv(gl.getUniformLocation(program, "clipDepth"), handView.clipDepth);

  function attribute(name: string, components: number) {
    const buffer = gl?.createBuffer();
    if (!buffer || !gl) return null;
    buffers.push(buffer);
    gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
    const location = gl.getAttribLocation(program, name);
    gl.enableVertexAttribArray(location);
    gl.vertexAttribPointer(location, components, gl.FLOAT, false, 0, 0);
    return buffer;
  }

  const positions = attribute("position", 3);
  const normals = attribute("normal", 3);
  const colours = attribute("colour", 3);
  const indices = gl.createBuffer();
  if (indices) buffers.push(indices);
  if (!positions || !normals || !colours || !indices) {
    dispose();
    return null;
  }
  gl.bindBuffer(gl.ELEMENT_ARRAY_BUFFER, indices);
  gl.bufferData(gl.ELEMENT_ARRAY_BUFFER, new Uint16Array(geometry.indices), gl.STATIC_DRAW);
  gl.enable(gl.DEPTH_TEST);
  gl.clearColor(0, 0, 0, 0);

  let previousHighlights: readonly number[] | null = null;
  function render(values: readonly number[], highlights: readonly number[] = []) {
    if (!gl) return;
    const ratio = Math.min(window.devicePixelRatio || 1, 2);
    const width = Math.round(canvas.clientWidth * ratio);
    const height = Math.round(canvas.clientHeight * ratio);
    if (!width || !height) return;
    if (canvas.width !== width || canvas.height !== height) {
      canvas.width = width;
      canvas.height = height;
    }
    gl.viewport(0, 0, width, height);
    // biome-ignore lint/correctness/useHookAtTopLevel: WebGL useProgram is not a React hook.
    gl.useProgram(program);
    const pose = deformHand(values);
    for (const [buffer, data] of [
      [positions, pose.positions.flat()],
      [normals, pose.normals.flat()],
    ] as const) {
      gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER, new Float32Array(data), gl.DYNAMIC_DRAW);
    }
    if (
      previousHighlights === null ||
      previousHighlights.length !== highlights.length ||
      highlights.some((value, index) => value !== previousHighlights?.[index])
    ) {
      gl.bindBuffer(gl.ARRAY_BUFFER, colours);
      gl.bufferData(
        gl.ARRAY_BUFFER,
        new Float32Array(createHandColours(highlights, accent).flat()),
        gl.DYNAMIC_DRAW,
      );
      previousHighlights = [...highlights];
    }
    gl.clear(gl.COLOR_BUFFER_BIT | gl.DEPTH_BUFFER_BIT);
    gl.drawElements(gl.TRIANGLES, geometry.indices.length, gl.UNSIGNED_SHORT, 0);
  }

  return { render, dispose };
}
